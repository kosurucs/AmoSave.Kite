namespace AmoSave.Kite.API.Models;

public class Instrument
{
    public int Id { get; set; }
    public long InstrumentToken { get; set; }
    public long ExchangeToken { get; set; }
    public string TradingSymbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public DateTime? Expiry { get; set; }
    public decimal? Strike { get; set; }
    public decimal TickSize { get; set; }
    public int LotSize { get; set; }
    public string InstrumentType { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class MarketQuote
{
    public int Id { get; set; }
    public long InstrumentToken { get; set; }
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal LastQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal Volume { get; set; }
    public decimal BuyQuantity { get; set; }
    public decimal SellQuantity { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Change { get; set; }
    public string OhlcData { get; set; } = string.Empty;
    public string DepthData { get; set; } = string.Empty;
    public DateTime LastTradeTime { get; set; }
    public DateTime OhlcTimestamp { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class HistoricalCandle
{
    public int Id { get; set; }
    public long InstrumentToken { get; set; }
    public string Interval { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public long? Oi { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
