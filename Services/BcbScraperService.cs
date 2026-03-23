using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using ProjetoMOL.Models;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace ProjetoMOL.Services;

/// <summary>
/// Serviço de automação para consulta de cotações no site do Banco Central (PTAX).
/// </summary>
public class BcbScraperService : IDisposable
{
    private const string BcbHomeUrl = "https://www.bcb.gov.br/";
    private const string BcbCotacoesUrl = "https://www.bcb.gov.br/estabilidadefinanceira/historicocotacoes";
    private const string PtaxFormUrl = "https://ptax.bcb.gov.br/ptax_internet/consultaBoletim.do?method=consultarBoletim";
    private const string EuroDropdownValue = "222";
    private const int TimeoutPadrao = 20;

    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private bool _disposed;

    public BcbScraperService()
    {
        // Gerencia o download do ChromeDriver automaticamente
        new DriverManager().SetUpDriver(new ChromeConfig());

        var options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-extensions");

        _driver = new ChromeDriver(options);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(TimeoutPadrao));
    }

    /// <summary>
    /// Acessa o BCB, navega até o formulário PTAX, preenche os parâmetros
    /// e retorna as cotações do Euro no período informado.
    /// </summary>
    public List<CotacaoMoeda> ObterCotacoesEuro(string dataInicial, string dataFinal)
    {
        try
        {
            NavegarParaFormularioCotacoes();
            PreencherFormulario(dataInicial, dataFinal);
            return ExtrairCotacoes();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha no scraping: {ex.Message}");
            throw;
        }
    }

    // Navega pelo site do BCB até chegar no formulário de cotações.
    // O BCB é uma SPA Angular, então a navegação pelo menu pode falhar por timing.
    // Se falhar, usa URL direta como fallback.
    private void NavegarParaFormularioCotacoes()
    {
        Console.WriteLine("[INFO] Acessando o site do Banco Central do Brasil...");
        _driver.Navigate().GoToUrl(BcbHomeUrl);
        AguardarCarregamento();

        FecharBannerCookies();

        Console.WriteLine("[INFO] Navegando até 'Cotações e boletins' no menu superior...");
        try
        {
            AbrirMenuNavegacao();

            // Procura "Estabilidade financeira" no menu principal
            var menuEstabilidade = _wait.Until(d =>
            {
                var elementos = d.FindElements(By.CssSelector("button, a"));
                return elementos.FirstOrDefault(e =>
                    e.Displayed && e.Text.Contains("Estabilidade", StringComparison.OrdinalIgnoreCase));
            });

            if (menuEstabilidade != null)
            {
                Console.WriteLine("[INFO] Encontrado menu 'Estabilidade financeira'. Clicando...");
                menuEstabilidade.Click();
                Thread.Sleep(1000);

                // Menu multinível: Estabilidade > Câmbio > Cotações e boletins
                var linkCotacoes = BuscarLinkSubmenu("cotações e boletins", "historicocotacoes");

                if (linkCotacoes == null)
                {
                    // Tenta pelo nível intermediário "Câmbio e Capitais internacionais"
                    var linkCambio = BuscarLinkSubmenu("Câmbio", "cambio");
                    if (linkCambio != null)
                    {
                        Console.WriteLine("[INFO] Encontrado submenu 'Câmbio'. Expandindo...");
                        linkCambio.Click();
                        Thread.Sleep(1000);
                        linkCotacoes = BuscarLinkSubmenu("cotações e boletins", "historicocotacoes");
                    }
                }

                if (linkCotacoes != null)
                {
                    Console.WriteLine("[INFO] Encontrado 'Consulta de cotações e boletins'. Navegando...");
                    linkCotacoes.Click();
                    AguardarCarregamento();
                    Thread.Sleep(2000);
                }
            }

            if (!FormularioPresente())
            {
                Console.WriteLine("[INFO] Formulário não encontrado na SPA. Tentando URL direta...");
                _driver.Navigate().GoToUrl(BcbCotacoesUrl);
                AguardarCarregamento();
                Thread.Sleep(2000);
            }

            if (!FormularioPresente())
            {
                Console.WriteLine("[INFO] Acessando PTAX diretamente...");
                _driver.Navigate().GoToUrl(PtaxFormUrl);
                AguardarCarregamento();
            }
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("[WARN] Menu não carregou a tempo. Acessando URL direta...");
            _driver.Navigate().GoToUrl(BcbCotacoesUrl);
            AguardarCarregamento();
            Thread.Sleep(2000);

            if (!FormularioPresente())
            {
                _driver.Navigate().GoToUrl(PtaxFormUrl);
                AguardarCarregamento();
            }
        }

        Console.WriteLine($"[INFO] Página atual: {_driver.Url}");
    }

    private void FecharBannerCookies()
    {
        try
        {
            var btnRejeitar = new WebDriverWait(_driver, TimeSpan.FromSeconds(5))
                .Until(d =>
                {
                    var botoes = d.FindElements(By.TagName("button"));
                    return botoes.FirstOrDefault(b =>
                        b.Displayed && b.Text.Contains("Rejeitar cookies", StringComparison.OrdinalIgnoreCase));
                });

            if (btnRejeitar != null)
            {
                btnRejeitar.Click();
                Console.WriteLine("[INFO] Banner de cookies fechado.");
                Thread.Sleep(500);
            }
        }
        catch (WebDriverTimeoutException)
        {
            // Sem banner, segue normalmente
        }
    }

    // Em telas menores o menu fica escondido atrás de um hamburger (≡)
    private void AbrirMenuNavegacao()
    {
        try
        {
            var hamburger = _driver.FindElements(By.CssSelector("button.navbar-toggler, button[aria-label='Menu'], .hamburger, .menu-toggle"))
                .FirstOrDefault(e => e.Displayed);

            if (hamburger != null)
            {
                hamburger.Click();
                Thread.Sleep(500);
            }
        }
        catch
        {
            // Já está visível
        }
    }

    // Busca um link no submenu por texto ou href parcial (timeout curto pra não travar)
    private IWebElement? BuscarLinkSubmenu(string texto, string hrefParcial)
    {
        try
        {
            return new WebDriverWait(_driver, TimeSpan.FromSeconds(3)).Until(d =>
            {
                var links = d.FindElements(By.CssSelector("a, button"));
                return links.FirstOrDefault(e =>
                    e.Displayed && (
                        e.Text.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                        e.GetAttribute("href")?.Contains(hrefParcial, StringComparison.OrdinalIgnoreCase) == true));
            });
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
    }

    private bool FormularioPresente()
    {
        try
        {
            return _driver.FindElements(By.Name("RadOpcao")).Count > 0 ||
                   _driver.FindElements(By.Name("ChkMoeda")).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private void PreencherFormulario(string dataInicial, string dataFinal)
    {
        Console.WriteLine("[INFO] Preenchendo formulário de consulta...");
        _wait.Until(d => d.FindElement(By.Name("RadOpcao")));

        var js = (IJavaScriptExecutor)_driver;

        // Seleciona opção 1: "Cotações de fechamento de uma moeda em um período"
        var radioOpcao1 = _driver.FindElement(By.CssSelector("input[name='RadOpcao'][value='1']"));
        radioOpcao1.Click();
        Thread.Sleep(500); // Espera o JS do formulário processar a troca de opção

        // Seta datas via JS pra evitar conflito com o auto-preenchimento do form
        js.ExecuteScript(
            "document.getElementsByName('DATAINI')[0].value = arguments[0];" +
            "document.getElementsByName('DATAFIM')[0].value = arguments[1];",
            dataInicial, dataFinal);

        // Seleciona Euro
        var selectMoeda = new SelectElement(_driver.FindElement(By.Name("ChkMoeda")));
        selectMoeda.SelectByValue(EuroDropdownValue);

        Console.WriteLine($"[INFO] Parâmetros: Moeda=EURO | Período={dataInicial} a {dataFinal}");

        // Submit via JS pra garantir o envio (mais confiável que clicar no botão)
        js.ExecuteScript("document.forms[0].submit();");

        Console.WriteLine("[INFO] Consulta enviada. Aguardando resultados...");

        // Espera a página de resultados carregar
        var waitResultados = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        waitResultados.Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");

        // Caso abra em nova aba
        if (_driver.WindowHandles.Count > 1)
            _driver.SwitchTo().Window(_driver.WindowHandles.Last());

        Console.WriteLine($"[INFO] Resultados carregados: {_driver.Url}");
    }

    private List<CotacaoMoeda> ExtrairCotacoes()
    {
        var cotacoes = new List<CotacaoMoeda>();
        var waitTabela = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        IWebElement? tabela = null;
        try
        {
            // Procura a tabela que tem bastante <td> (a de resultados, não a do form)
            tabela = waitTabela.Until(d =>
            {
                var tabelas = d.FindElements(By.TagName("table"));
                foreach (var t in tabelas)
                {
                    if (t.FindElements(By.TagName("td")).Count > 10)
                        return t;
                }
                return null;
            });
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("[WARN] Timeout aguardando tabela de resultados.");
            Console.WriteLine($"[DEBUG] URL: {_driver.Url}");
            return cotacoes;
        }

        var linhas = tabela.FindElements(By.TagName("tr"));
        Console.WriteLine($"[INFO] {linhas.Count} linhas encontradas na tabela.");

        foreach (var linha in linhas)
        {
            var celulas = linha.FindElements(By.TagName("td"));
            if (celulas.Count < 5) continue;

            var textoData = celulas[0].Text.Trim();
            if (!DateTime.TryParseExact(textoData, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var data))
                continue;

            try
            {
                // Colunas: [0] Data | [1] Tipo (A/B) | [2] Compra | [3] Venda | [4] Parid.Compra | [5] Parid.Venda
                cotacoes.Add(new CotacaoMoeda
                {
                    Data = data,
                    Moeda = "EURO",
                    CotacaoCompra = ParseDecimal(celulas[2].Text),
                    CotacaoVenda = ParseDecimal(celulas[3].Text),
                    ParidadeCompra = ParseDecimal(celulas[4].Text),
                    ParidadeVenda = celulas.Count > 5 ? ParseDecimal(celulas[5].Text) : 0
                });
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"[WARN] Erro ao processar cotação de {textoData}: {ex.Message}");
            }
        }

        Console.WriteLine($"[INFO] {cotacoes.Count} cotações extraídas com sucesso.");
        return cotacoes;
    }

    // Converte formato BR (vírgula decimal) pra decimal
    private static decimal ParseDecimal(string texto)
    {
        return decimal.Parse(texto.Trim().Replace(".", "").Replace(",", "."), CultureInfo.InvariantCulture);
    }

    private void AguardarCarregamento()
    {
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState")?.ToString() == "complete");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _driver.Quit();
        _driver.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
