namespace AmoSave.Kite.API.Models;

public class Alert
{
    public int Id { get; set; }
    public string AlertId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public decimal TriggerValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriggeredAt { get; set; }
}

public class AlertWebhookPayload
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ExchangeOrderId { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string Variety { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TriggerPrice { get; set; }
    public DateTime OrderTimestamp { get; set; }
    public DateTime ExchangeTimestamp { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class CreateAlertRequest
{
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public decimal TriggerValue { get; set; }
    public string Notes { get; set; } = string.Empty;
}
