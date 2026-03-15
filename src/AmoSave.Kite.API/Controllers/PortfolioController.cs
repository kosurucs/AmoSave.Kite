using System.Text.Json;
using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PortfolioController : ControllerBase
{
    private readonly IKiteConnectService _kite;
    private readonly KiteDbContext _db;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<PortfolioController> _logger;

    public PortfolioController(IKiteConnectService kite, KiteDbContext db, IOptions<KiteConnectSettings> settings, ILogger<PortfolioController> logger)
    {
        _kite = kite;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Returns the user's equity and MF holdings. Served from cache if fresh.</summary>
    [HttpGet("holdings")]
    public async Task<ActionResult<ApiResponse<object>>> GetHoldings(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            var expiry = DateTime.UtcNow.AddMinutes(-_settings.CacheExpiryMinutes);
            var cached = await _db.Holdings
                .Where(h => h.UserId == userId && h.CachedAt > expiry)
                .ToListAsync();

            if (cached.Count > 0)
                return Ok(ApiResponse<object>.Success(cached));

            var result = await _kite.GetHoldingsAsync(accessToken);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            await SyncHoldingsAsync(userId, data);
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get holdings for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Returns current day and overnight positions. Served from cache if fresh.</summary>
    [HttpGet("positions")]
    public async Task<ActionResult<ApiResponse<object>>> GetPositions(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            var expiry = DateTime.UtcNow.AddMinutes(-_settings.CacheExpiryMinutes);
            var cached = await _db.Positions
                .Where(p => p.UserId == userId && p.CachedAt > expiry)
                .ToListAsync();

            if (cached.Count > 0)
                return Ok(ApiResponse<object>.Success(cached));

            var result = await _kite.GetPositionsAsync(accessToken);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            await SyncPositionsAsync(userId, data);
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get positions for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Converts a position from one product type to another (e.g., MIS to CNC).</summary>
    [HttpPut("positions")]
    public async Task<ActionResult<ApiResponse<object>>> ConvertPosition(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromBody] ConvertPositionRequest request)
    {
        try
        {
            var positionParams = new Dictionary<string, string>
            {
                ["exchange"] = request.Exchange,
                ["tradingsymbol"] = request.TradingSymbol,
                ["transaction_type"] = request.TransactionType,
                ["position_type"] = request.PositionType,
                ["quantity"] = request.Quantity.ToString(),
                ["old_product"] = request.OldProduct,
                ["new_product"] = request.NewProduct
            };

            var result = await _kite.ConvertPositionAsync(accessToken, positionParams);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            // Invalidate positions cache so it refreshes on next call
            await _db.Positions.Where(p => p.UserId == userId).ExecuteDeleteAsync();
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert position for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    private async Task SyncHoldingsAsync(string userId, JsonElement data)
    {
        await _db.Holdings.Where(h => h.UserId == userId).ExecuteDeleteAsync();
        if (data.ValueKind != JsonValueKind.Array) return;

        foreach (var item in data.EnumerateArray())
        {
            _db.Holdings.Add(new Holding
            {
                UserId = userId,
                TradingSymbol = GetString(item, "tradingsymbol"),
                Exchange = GetString(item, "exchange"),
                InstrumentToken = GetLong(item, "instrument_token"),
                Isin = GetString(item, "isin"),
                Product = GetString(item, "product"),
                Quantity = GetInt(item, "quantity"),
                T1Quantity = GetInt(item, "t1_quantity"),
                RealisedQuantity = GetInt(item, "realised_quantity"),
                AveragePrice = GetDecimal(item, "average_price"),
                LastPrice = GetDecimal(item, "last_price"),
                ClosePrice = GetDecimal(item, "close_price"),
                Pnl = GetDecimal(item, "pnl"),
                DayChange = GetDecimal(item, "day_change"),
                DayChangePct = GetDecimal(item, "day_change_percentage"),
                CachedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task SyncPositionsAsync(string userId, JsonElement data)
    {
        await _db.Positions.Where(p => p.UserId == userId).ExecuteDeleteAsync();

        void AddPositions(JsonElement items, string posType)
        {
            if (items.ValueKind != JsonValueKind.Array) return;
            foreach (var item in items.EnumerateArray())
            {
                _db.Positions.Add(new Position
                {
                    UserId = userId,
                    PositionType = posType,
                    TradingSymbol = GetString(item, "tradingsymbol"),
                    Exchange = GetString(item, "exchange"),
                    InstrumentToken = GetLong(item, "instrument_token"),
                    Product = GetString(item, "product"),
                    Quantity = GetInt(item, "quantity"),
                    OvernightQuantity = GetInt(item, "overnight_quantity"),
                    AveragePrice = GetDecimal(item, "average_price"),
                    ClosePrice = GetDecimal(item, "close_price"),
                    LastPrice = GetDecimal(item, "last_price"),
                    Value = GetDecimal(item, "value"),
                    Pnl = GetDecimal(item, "pnl"),
                    M2m = GetDecimal(item, "m2m"),
                    Unrealised = GetDecimal(item, "unrealised"),
                    Realised = GetDecimal(item, "realised"),
                    BuyQuantity = GetInt(item, "buy_quantity"),
                    BuyPrice = GetDecimal(item, "buy_price"),
                    SellQuantity = GetInt(item, "sell_quantity"),
                    SellPrice = GetDecimal(item, "sell_price"),
                    CachedAt = DateTime.UtcNow
                });
            }
        }

        if (data.TryGetProperty("day", out var day)) AddPositions(day, "day");
        if (data.TryGetProperty("net", out var net)) AddPositions(net, "net");
        await _db.SaveChangesAsync();
    }

    private static bool IsSuccess(JsonElement element, out JsonElement data)
    {
        if (element.TryGetProperty("status", out var status) && status.GetString() == "success"
            && element.TryGetProperty("data", out data))
            return true;
        data = default;
        return false;
    }

    private static string GetErrorMessage(JsonElement element) =>
        element.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error";

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.GetString() ?? "" : "";

    private static int GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32() : 0;

    private static long GetLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt64() : 0;

    private static decimal GetDecimal(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetDecimal() : 0m;
}

public class ConvertPositionRequest
{
    public string Exchange { get; set; } = string.Empty;
    public string TradingSymbol { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string OldProduct { get; set; } = string.Empty;
    public string NewProduct { get; set; } = string.Empty;
}
