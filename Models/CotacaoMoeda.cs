namespace ProjetoMOL.Models;

public class CotacaoMoeda
{
    public DateTime Data { get; set; }
    public string Moeda { get; set; } = string.Empty;
    public decimal CotacaoCompra { get; set; }
    public decimal CotacaoVenda { get; set; }
    public decimal ParidadeCompra { get; set; }
    public decimal ParidadeVenda { get; set; }
}
