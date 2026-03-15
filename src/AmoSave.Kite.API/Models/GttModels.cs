namespace AmoSave.Kite.API.Models;

public class GttOrder
{
    public int Id { get; set; }
    public int TriggerId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string InstrumentToken { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Orders { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class GttCondition
{
    public string Exchange { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public List<double> TriggerValues { get; set; } = new();
    public double LastPrice { get; set; }
}

public class GttOrderItem
{
    public string TransactionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Product { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public double Price { get; set; }
}

public class PlaceGttRequest
{
    public string TriggerType { get; set; } = "single";
    public GttCondition Condition { get; set; } = new();
    public List<GttOrderItem> Orders { get; set; } = new();
}
