using System.Text.Json;
using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/market")]
[Produces("application/json")]
public class MarketController : ControllerBase
{
    private readonly IKiteConnectService _kite;
    private readonly KiteDbContext _db;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<MarketController> _logger;

    public MarketController(IKiteConnectService kite, KiteDbContext db, IOptions<KiteConnectSettings> settings, ILogger<MarketController> logger)
    {
        _kite = kite;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns full market quote (OHLC, depth, etc.) for one or more instruments.
    /// Instruments format: "NSE:INFY", "BSE:RELIANCE", etc.
    /// </summary>
    [HttpGet("quote")]
    public async Task<ActionResult<ApiResponse<object>>> GetQuote(
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromQuery] string instruments)
    {
        try
        {
            var instrumentList = instruments.Split(',', StringSplitOptions.RemoveEmptyEntries);

            var expiry = DateTime.UtcNow.AddMinutes(-_settings.CacheExpiryMinutes);
            var allCached = true;
            var cachedResults = new List<MarketQuote>();

            foreach (var instrument in instrumentList)
            {
                var parts = instrument.Split(':');
                if (parts.Length == 2)
                {
                    var cached = await _db.MarketQuotes.FirstOrDefaultAsync(q =>
                        q.Exchange == parts[0] && q.TradingSymbol == parts[1] && q.CachedAt > expiry);
                    if (cached != null) cachedResults.Add(cached);
                    else allCached = false;
                }
                else allCached = false;
            }

            if (allCached && cachedResults.Count == instrumentList.Length)
                return Ok(ApiResponse<object>.Success(cachedResults));

            var result = await _kite.GetQuoteAsync(accessToken, instrumentList);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            await SyncQuotesAsync(data);
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get quotes");
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Returns OHLC and last traded price for one or more instruments.</summary>
    [HttpGet("ohlc")]
    public async Task<ActionResult<ApiResponse<object>>> GetOhlc(
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromQuery] string instruments)
    {
        try
        {
            var instrumentList = instruments.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var result = await _kite.GetOhlcAsync(accessToken, instrumentList);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OHLC");
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Returns last traded price for one or more instruments.</summary>
    [HttpGet("ltp")]
    public async Task<ActionResult<ApiResponse<object>>> GetLtp(
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromQuery] string instruments)
    {
        try
        {
            var instrumentList = instruments.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var result = await _kite.GetLtpAsync(accessToken, instrumentList);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get LTP");
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Returns the full list of instruments (tradeable contracts).
    /// The response is cached for 24 hours as instruments rarely change intraday.
    /// </summary>
    [HttpGet("instruments")]
    public async Task<ActionResult<ApiResponse<object>>> GetInstruments([FromQuery] string? exchange = null)
    {
        try
        {
            var expiry = DateTime.UtcNow.AddHours(-_settings.InstrumentCacheHours);
            var query = _db.Instruments.AsQueryable();

            if (!string.IsNullOrEmpty(exchange))
                query = query.Where(i => i.Exchange == exchange);

            var cachedInstruments = await query.Where(i => i.CachedAt > expiry).ToListAsync();
            if (cachedInstruments.Count > 0)
                return Ok(ApiResponse<object>.Success(cachedInstruments));

            var result = await _kite.GetInstrumentsAsync(exchange);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            // The instrument data from Kite comes as CSV; pass it through as-is
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get instruments");
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    private async Task SyncQuotesAsync(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return;

        foreach (var prop in data.EnumerateObject())
        {
            var parts = prop.Name.Split(':');
            if (parts.Length != 2) continue;

            var existing = await _db.MarketQuotes.FirstOrDefaultAsync(q =>
                q.Exchange == parts[0] && q.TradingSymbol == parts[1]);

            if (existing != null) _db.MarketQuotes.Remove(existing);

            var item = prop.Value;
            _db.MarketQuotes.Add(new MarketQuote
            {
                InstrumentToken = GetLong(item, "instrument_token"),
                TradingSymbol = parts[1],
                Exchange = parts[0],
                LastPrice = GetDecimal(item, "last_price"),
                LastQuantity = GetDecimal(item, "last_quantity"),
                AveragePrice = GetDecimal(item, "average_price"),
                Volume = GetDecimal(item, "volume"),
                BuyQuantity = GetDecimal(item, "buy_quantity"),
                SellQuantity = GetDecimal(item, "sell_quantity"),
                OhlcData = item.TryGetProperty("ohlc", out var ohlc) ? ohlc.ToString() : "",
                DepthData = item.TryGetProperty("depth", out var depth) ? depth.ToString() : "",
                CachedAt = DateTime.UtcNow
            });
        }
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

    private static long GetLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt64() : 0;

    private static decimal GetDecimal(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetDecimal() : 0m;
}
