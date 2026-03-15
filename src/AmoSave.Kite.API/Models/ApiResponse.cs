namespace AmoSave.Kite.API.Models;

public class ApiResponse<T>
{
    public string Status { get; set; } = "success";
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? ErrorType { get; set; }

    public static ApiResponse<T> Success(T data) => new() { Status = "success", Data = data };
    public static ApiResponse<T> Error(string message, string errorType = "GeneralException") =>
        new() { Status = "error", Message = message, ErrorType = errorType };
}

public class KiteConnectSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.kite.trade";
    public string LoginUrl { get; set; } = "https://kite.zerodha.com/connect/login";
    public int CacheExpiryMinutes { get; set; } = 5;
    public int InstrumentCacheHours { get; set; } = 24;
    public string WebSocketUrl { get; set; } = "wss://ws.kite.trade";
}
