namespace AmoSave.Kite.API.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ExchangeOrderId { get; set; } = string.Empty;
    public string ParentOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string InstrumentToken { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string Validity { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int PendingQuantity { get; set; }
    public int FilledQuantity { get; set; }
    public int DisclosedQuantity { get; set; }
    public decimal Price { get; set; }
    public decimal TriggerPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Variety { get; set; } = string.Empty;
    public DateTime OrderTimestamp { get; set; }
    public DateTime ExchangeTimestamp { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class Trade
{
    public int Id { get; set; }
    public string TradeId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ExchangeOrderId { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string InstrumentToken { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal AveragePrice { get; set; }
    public DateTime FillTimestamp { get; set; }
    public DateTime ExchangeTimestamp { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class PlaceOrderRequest
{
    public string Exchange { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Product { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public decimal? TriggerPrice { get; set; }
    public int? DisclosedQuantity { get; set; }
    public string Validity { get; set; } = "DAY";
    public string? Tag { get; set; }
    public string Variety { get; set; } = "regular";
}

public class ModifyOrderRequest
{
    public int? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? TriggerPrice { get; set; }
    public int? DisclosedQuantity { get; set; }
    public string? Validity { get; set; }
    public string? OrderType { get; set; }
    public string Variety { get; set; } = "regular";
}
