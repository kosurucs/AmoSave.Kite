using System.Text.Json;
using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/historical")]
[Produces("application/json")]
public class HistoricalController : ControllerBase
{
    private readonly IKiteConnectService _kite;
    private readonly KiteDbContext _db;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<HistoricalController> _logger;

    private static readonly HashSet<string> ValidIntervals = new(StringComparer.OrdinalIgnoreCase)
    {
        "minute", "3minute", "5minute", "10minute", "15minute", "30minute", "60minute", "day"
    };

    public HistoricalController(IKiteConnectService kite, KiteDbContext db, IOptions<KiteConnectSettings> settings, ILogger<HistoricalController> logger)
    {
        _kite = kite;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns historical OHLCV candle data for an instrument.
    /// Data is cached in the database — previously fetched candles are served from cache.
    /// </summary>
    /// <param name="instrumentToken">Kite instrument token (e.g. 408065 for INFY)</param>
    /// <param name="interval">Candle interval: minute, 3minute, 5minute, 10minute, 15minute, 30minute, 60minute, day</param>
    /// <param name="from">Start date/time (ISO 8601, e.g. 2024-01-01 or 2024-01-01T09:15:00)</param>
    /// <param name="to">End date/time (ISO 8601)</param>
    /// <param name="continuous">Set to true for continuous futures data (default: false)</param>
    /// <param name="oi">Set to true to include open interest (default: false)</param>
    [HttpGet("{instrumentToken:long}/{interval}")]
    public async Task<ActionResult<ApiResponse<object>>> GetHistoricalData(
        long instrumentToken,
        string interval,
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] bool continuous = false,
        [FromQuery] bool oi = false)
    {
        if (!ValidIntervals.Contains(interval))
            return BadRequest(ApiResponse<object>.Error(
                $"Invalid interval '{interval}'. Valid intervals: {string.Join(", ", ValidIntervals)}"));

        if (from >= to)
            return BadRequest(ApiResponse<object>.Error("'from' must be before 'to'"));

        try
        {
            // Check which candles are already cached
            var cachedCandles = await _db.HistoricalCandles
                .Where(c => c.InstrumentToken == instrumentToken
                         && c.Interval == interval
                         && c.Timestamp >= from
                         && c.Timestamp <= to)
                .OrderBy(c => c.Timestamp)
                .ToListAsync();

            if (cachedCandles.Count > 0)
            {
                // Find any gaps and fetch only missing data if needed
                var firstCached = cachedCandles.First().Timestamp;
                var lastCached = cachedCandles.Last().Timestamp;

                // If full range covered (approximately), return cached data.
                // We allow up to one interval's worth of tolerance at each boundary
                // to handle the fact that the first/last candle may land slightly
                // inside the requested range rather than exactly on from/to.
                if (firstCached <= from.AddMinutes(GetIntervalMinutes(interval)) &&
                    lastCached >= to.AddMinutes(-GetIntervalMinutes(interval)))
                {
                    _logger.LogDebug("Historical data served from cache: {Token}/{Interval}", instrumentToken, interval);
                    return Ok(ApiResponse<object>.Success(cachedCandles));
                }
            }

            // Fetch from Kite
            var result = await _kite.GetHistoricalDataAsync(accessToken, instrumentToken, interval, from, to, continuous, oi);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            // Parse and cache candles
            if (data.TryGetProperty("candles", out var candles) && candles.ValueKind == JsonValueKind.Array)
            {
                var newCandles = ParseAndCacheCandles(instrumentToken, interval, candles);
                await UpsertCandlesAsync(newCandles);
                _logger.LogDebug("Cached {Count} historical candles for {Token}/{Interval}", newCandles.Count, instrumentToken, interval);
            }

            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get historical data for {Token}/{Interval}", instrumentToken, interval);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    private List<HistoricalCandle> ParseAndCacheCandles(long instrumentToken, string interval, JsonElement candles)
    {
        var result = new List<HistoricalCandle>();
        foreach (var candle in candles.EnumerateArray())
        {
            if (candle.ValueKind != JsonValueKind.Array) continue;
            var arr = candle.EnumerateArray().ToList();
            if (arr.Count < 6) continue;

            result.Add(new HistoricalCandle
            {
                InstrumentToken = instrumentToken,
                Interval = interval,
                Timestamp = arr[0].GetDateTime(),
                Open = arr[1].GetDecimal(),
                High = arr[2].GetDecimal(),
                Low = arr[3].GetDecimal(),
                Close = arr[4].GetDecimal(),
                Volume = arr[5].GetInt64(),
                Oi = arr.Count > 6 && arr[6].ValueKind == JsonValueKind.Number ? arr[6].GetInt64() : null,
                CachedAt = DateTime.UtcNow
            });
        }
        return result;
    }

    private async Task UpsertCandlesAsync(List<HistoricalCandle> candles)
    {
        foreach (var candle in candles)
        {
            var existing = await _db.HistoricalCandles.FirstOrDefaultAsync(c =>
                c.InstrumentToken == candle.InstrumentToken &&
                c.Interval == candle.Interval &&
                c.Timestamp == candle.Timestamp);

            if (existing != null)
            {
                existing.Open = candle.Open;
                existing.High = candle.High;
                existing.Low = candle.Low;
                existing.Close = candle.Close;
                existing.Volume = candle.Volume;
                existing.Oi = candle.Oi;
                existing.CachedAt = candle.CachedAt;
            }
            else
            {
                _db.HistoricalCandles.Add(candle);
            }
        }
        await _db.SaveChangesAsync();
    }

    private static int GetIntervalMinutes(string interval) => interval switch
    {
        "minute" => 1,
        "3minute" => 3,
        "5minute" => 5,
        "10minute" => 10,
        "15minute" => 15,
        "30minute" => 30,
        "60minute" => 60,
        "day" => 1440,
        _ => 1
    };

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
}
