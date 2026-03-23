using ProjetoMOL.Models;
using ProjetoMOL.Services;

namespace ProjetoMOL;

// Automação RPA - Consulta de cotações de fechamento do Euro no PTAX/BCB.
//
// Libs externas:
// - Selenium.WebDriver + Selenium.Support: automação do navegador (requisito do teste)
// - WebDriverManager: gerencia o ChromeDriver automaticamente, sem precisar instalar manual
class Program
{
    private const string DataInicial = "01/10/2025";
    private const string DataFinal = "17/11/2025";

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   CONSULTA DE COTAÇÕES - BANCO CENTRAL DO BRASIL (PTAX)     ║");
        Console.WriteLine("║   Moeda: EURO | Período: 01/10/2025 a 17/11/2025            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        List<CotacaoMoeda> cotacoes;

        try
        {
            using var scraper = new BcbScraperService();
            cotacoes = scraper.ObterCotacoesEuro(DataInicial, DataFinal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERRO FATAL] Não foi possível obter as cotações: {ex.Message}");
            AguardarSaida();
            return;
        }

        if (cotacoes.Count == 0)
        {
            Console.WriteLine("\n[AVISO] Nenhuma cotação encontrada para o período informado.");
            AguardarSaida();
            return;
        }

        ExibirResultados(cotacoes);
        AguardarSaida();
    }

    private static void ExibirResultados(List<CotacaoMoeda> cotacoes)
    {
        Console.WriteLine();
        Console.WriteLine($"  Total de cotações obtidas: {cotacoes.Count}");
        Console.WriteLine();

        var sep = new string('─', 93);
        Console.WriteLine($"  ┌{sep}┐");
        Console.WriteLine($"  │ {"Data",-12}│ {"Moeda",-8}│ {"Cotação Compra",15}│ {"Cotação Venda",14}│ {"Parid. Compra",14}│ {"Parid. Venda",13}│");
        Console.WriteLine($"  ├{sep}┤");

        foreach (var c in cotacoes)
        {
            Console.WriteLine(
                $"  │ {c.Data:dd/MM/yyyy}  │ {c.Moeda,-8}│ {c.CotacaoCompra,15:N4}│ {c.CotacaoVenda,14:N4}│ {c.ParidadeCompra,14:N4}│ {c.ParidadeVenda,13:N4}│");
        }

        Console.WriteLine($"  └{sep}┘");

        // Resumo
        Console.WriteLine();
        Console.WriteLine("  ── Resumo do Período ──");
        Console.WriteLine($"  Maior cotação de compra: {cotacoes.Max(c => c.CotacaoCompra):N4} em {cotacoes.OrderByDescending(c => c.CotacaoCompra).First().Data:dd/MM/yyyy}");
        Console.WriteLine($"  Menor cotação de compra: {cotacoes.Min(c => c.CotacaoCompra):N4} em {cotacoes.OrderBy(c => c.CotacaoCompra).First().Data:dd/MM/yyyy}");
        Console.WriteLine($"  Média cotação de compra: {cotacoes.Average(c => c.CotacaoCompra):N4}");
        Console.WriteLine($"  Média cotação de venda:  {cotacoes.Average(c => c.CotacaoVenda):N4}");
    }

    private static void AguardarSaida()
    {
        Console.WriteLine("\nPressione qualquer tecla para sair...");
        try { Console.ReadKey(); }
        catch (InvalidOperationException) { }
    }
}
