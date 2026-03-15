namespace AmoSave.Kite.API.Models;

public class Holding
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public long InstrumentToken { get; set; }
    public string Isin { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int T1Quantity { get; set; }
    public int RealisedQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal Pnl { get; set; }
    public decimal DayChange { get; set; }
    public decimal DayChangePct { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class Position
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public long InstrumentToken { get; set; }
    public string Product { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int OvernightQuantity { get; set; }
    public int MultiplierFactor { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal LastPrice { get; set; }
    public decimal Value { get; set; }
    public decimal Pnl { get; set; }
    public decimal M2m { get; set; }
    public decimal Unrealised { get; set; }
    public decimal Realised { get; set; }
    public int BuyQuantity { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal BuyValue { get; set; }
    public decimal BuyM2mValue { get; set; }
    public int SellQuantity { get; set; }
    public decimal SellPrice { get; set; }
    public decimal SellValue { get; set; }
    public decimal SellM2mValue { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
