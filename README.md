# Consulta de Cotações - Banco Central do Brasil (PTAX)

Programa de automação (RPA) em C# que consulta cotações de fechamento do **Euro** no sistema PTAX do Banco Central do Brasil, utilizando **Selenium WebDriver**.

## Objetivo

Acessar o site do BCB, navegar até a seção de cotações e extrair os valores de cotação de compra/venda e paridade de compra/venda do Euro no período de **01/10/2025 a 17/11/2025**, exibindo os resultados de forma estruturada no console.

## Tecnologias

- **.NET 8** (C#)
- **Selenium WebDriver 4.41** + **Selenium.Support** — automação do navegador para interação com o site do BCB (requisito do teste)
- **WebDriverManager** — gerencia automaticamente o download e versionamento do ChromeDriver, eliminando a necessidade de configuração manual. Escolhi essa lib porque o ChromeDriver precisa estar na mesma versão do Chrome instalado na máquina, e gerenciar isso manualmente é propenso a erro

## Pré-requisitos

- .NET 8 SDK instalado
- Google Chrome instalado

## Como executar

```bash
dotnet restore
dotnet run
```

O programa abre o Chrome automaticamente, navega pelo site do BCB, preenche o formulário e exibe os resultados no console. Nenhuma interação humana é necessária.

## Estrutura do projeto

```
projetoMOL/
├── Program.cs                 # Ponto de entrada + exibição dos resultados
├── Models/
│   └── CotacaoMoeda.cs        # Modelo de dados (data, moeda, cotações, paridades)
├── Services/
│   └── BcbScraperService.cs   # Toda a lógica de automação Selenium
├── projetoMOL.csproj
├── .gitignore
└── README.md
```

## Fluxo da automação

1. Acessa `https://www.bcb.gov.br/`
2. Fecha o banner de cookies
3. Navega pelo menu: **Estabilidade Financeira** → **Câmbio e Capitais Internacionais** → **Consulta de cotações e boletins**
4. No formulário PTAX, seleciona a opção "Cotações de fechamento de uma moeda em um período"
5. Preenche: moeda **Euro**, data inicial **01/10/2025**, data final **17/11/2025**
6. Submete a consulta e aguarda os resultados
7. Extrai as 34 cotações da tabela (cada dia útil do período)
8. Exibe no console em tabela formatada com resumo estatístico

## Tratamento de exceções

- **Banner de cookies**: tenta fechar, segue normalmente se não aparecer
- **Menu do BCB (SPA Angular)**: navegação multinível com fallback para URL direta caso o menu não carregue a tempo
- **Formulário**: preenchimento via JavaScript para evitar conflito com auto-preenchimento do form legado
- **Parsing de dados**: cada linha é tratada individualmente, erros em uma linha não impedem as demais
- **Console não-interativo**: trata `InvalidOperationException` no `ReadKey` para ambientes sem terminal

## Sobre a opção de consulta

O enunciado pede para selecionar **"Cotações de fechamento de todas as moedas em um período"**, porém o formulário do PTAX oferece apenas:

1. Cotações de fechamento de **uma** moeda em um **período**
2. Cotações de fechamento de **todas** as moedas em uma **data**
3. Boletins intermediários de taxas de câmbio em uma data

A opção exata descrita no enunciado não existe no formulário atual. Optei pela **opção 1** (uma moeda em um período) por ser a que melhor atende ao objetivo, já que o próprio teste especifica a moeda Euro e um intervalo de datas.

Caso fosse necessário obter todas as moedas no período, as alternativas seriam:
- Iterar por cada moeda disponível no dropdown usando a opção 1
- Iterar por cada dia útil do período usando a opção 2

## Exemplo de saída

```
╔══════════════════════════════════════════════════════════════╗
║   CONSULTA DE COTAÇÕES - BANCO CENTRAL DO BRASIL (PTAX)     ║
║   Moeda: EURO | Período: 01/10/2025 a 17/11/2025            ║
╚══════════════════════════════════════════════════════════════╝

  Total de cotações obtidas: 34

  ┌─────────────────────────────────────────────────────────────────────────────────────────────┐
  │ Data        │ Moeda   │  Cotação Compra│  Cotação Venda│  Parid. Compra│  Parid. Venda│
  ├─────────────────────────────────────────────────────────────────────────────────────────────┤
  │ 01/10/2025  │ EURO    │          6,2358│         6,2376│         1,1721│        1,1723│
  │ ...         │ ...     │            ... │           ... │           ... │          ... │
  │ 17/11/2025  │ EURO    │          6,1582│         6,1595│         1,1597│        1,1598│
  └─────────────────────────────────────────────────────────────────────────────────────────────┘

  ── Resumo do Período ──
  Maior cotação de compra: 6,3816 em 14/10/2025
  Menor cotação de compra: 6,1127 em 11/11/2025
  Média cotação de compra: 6,2308
  Média cotação de venda:  6,2324
```
