using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AmoSave.Kite.API.Models;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Services;

public interface IKiteConnectService
{
    string GetLoginUrl(string? redirectUrl = null);
    Task<JsonElement> GenerateSessionAsync(string requestToken);
    Task<JsonElement> InvalidateSessionAsync(string accessToken);
    Task<JsonElement> GetProfileAsync(string accessToken);
    Task<JsonElement> GetMarginsAsync(string accessToken, string? segment = null);
    Task<JsonElement> GetOrdersAsync(string accessToken);
    Task<JsonElement> GetOrderHistoryAsync(string accessToken, string orderId);
    Task<JsonElement> GetOrderTradesAsync(string accessToken, string orderId);
    Task<JsonElement> GetTradesAsync(string accessToken);
    Task<JsonElement> PlaceOrderAsync(string accessToken, string variety, Dictionary<string, string> orderParams);
    Task<JsonElement> ModifyOrderAsync(string accessToken, string variety, string orderId, Dictionary<string, string> orderParams);
    Task<JsonElement> CancelOrderAsync(string accessToken, string variety, string orderId, string? parentOrderId = null);
    Task<JsonElement> GetGttOrdersAsync(string accessToken);
    Task<JsonElement> GetGttOrderAsync(string accessToken, int triggerId);
    Task<JsonElement> PlaceGttOrderAsync(string accessToken, Dictionary<string, string> gttParams);
    Task<JsonElement> ModifyGttOrderAsync(string accessToken, int triggerId, Dictionary<string, string> gttParams);
    Task<JsonElement> DeleteGttOrderAsync(string accessToken, int triggerId);
    Task<JsonElement> GetHoldingsAsync(string accessToken);
    Task<JsonElement> GetPositionsAsync(string accessToken);
    Task<JsonElement> ConvertPositionAsync(string accessToken, Dictionary<string, string> positionParams);
    Task<JsonElement> GetQuoteAsync(string accessToken, IEnumerable<string> instruments);
    Task<JsonElement> GetOhlcAsync(string accessToken, IEnumerable<string> instruments);
    Task<JsonElement> GetLtpAsync(string accessToken, IEnumerable<string> instruments);
    Task<JsonElement> GetInstrumentsAsync(string? exchange = null);
    Task<JsonElement> GetHistoricalDataAsync(string accessToken, long instrumentToken, string interval, DateTime from, DateTime to, bool continuous = false, bool oi = false);
}

public class KiteConnectService : IKiteConnectService
{
    private readonly HttpClient _httpClient;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<KiteConnectService> _logger;

    public KiteConnectService(HttpClient httpClient, IOptions<KiteConnectSettings> settings, ILogger<KiteConnectService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public string GetLoginUrl(string? redirectUrl = null)
    {
        var url = $"{_settings.LoginUrl}?api_key={_settings.ApiKey}&v=3";
        if (!string.IsNullOrWhiteSpace(redirectUrl))
            url += $"&redirect_params={Uri.EscapeDataString(redirectUrl)}";
        return url;
    }

    public async Task<JsonElement> GenerateSessionAsync(string requestToken)
    {
        var checksum = ComputeSha256($"{_settings.ApiKey}{requestToken}{_settings.ApiSecret}");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = _settings.ApiKey,
            ["request_token"] = requestToken,
            ["checksum"] = checksum
        });
        return await PostAsync("/session/token", content);
    }

    public async Task<JsonElement> InvalidateSessionAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_settings.BaseUrl}/session/token?api_key={_settings.ApiKey}&access_token={accessToken}");
        SetAuthHeader(request, accessToken);
        return await SendAsync(request);
    }

    public async Task<JsonElement> GetProfileAsync(string accessToken) =>
        await GetAsync("/user/profile", accessToken);

    public async Task<JsonElement> GetMarginsAsync(string accessToken, string? segment = null)
    {
        var path = segment != null ? $"/user/margins/{segment}" : "/user/margins";
        return await GetAsync(path, accessToken);
    }

    public async Task<JsonElement> GetOrdersAsync(string accessToken) =>
        await GetAsync("/orders", accessToken);

    public async Task<JsonElement> GetOrderHistoryAsync(string accessToken, string orderId) =>
        await GetAsync($"/orders/{orderId}", accessToken);

    public async Task<JsonElement> GetOrderTradesAsync(string accessToken, string orderId) =>
        await GetAsync($"/orders/{orderId}/trades", accessToken);

    public async Task<JsonElement> GetTradesAsync(string accessToken) =>
        await GetAsync("/trades", accessToken);

    public async Task<JsonElement> PlaceOrderAsync(string accessToken, string variety, Dictionary<string, string> orderParams)
    {
        var content = new FormUrlEncodedContent(orderParams);
        return await PostAsync($"/orders/{variety}", content, accessToken);
    }

    public async Task<JsonElement> ModifyOrderAsync(string accessToken, string variety, string orderId, Dictionary<string, string> orderParams)
    {
        var content = new FormUrlEncodedContent(orderParams);
        return await PutAsync($"/orders/{variety}/{orderId}", content, accessToken);
    }

    public async Task<JsonElement> CancelOrderAsync(string accessToken, string variety, string orderId, string? parentOrderId = null)
    {
        var path = $"/orders/{variety}/{orderId}";
        if (!string.IsNullOrEmpty(parentOrderId))
            path += $"?parent_order_id={parentOrderId}";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_settings.BaseUrl}{path}");
        SetAuthHeader(request, accessToken);
        return await SendAsync(request);
    }

    public async Task<JsonElement> GetGttOrdersAsync(string accessToken) =>
        await GetAsync("/gtt/triggers", accessToken);

    public async Task<JsonElement> GetGttOrderAsync(string accessToken, int triggerId) =>
        await GetAsync($"/gtt/triggers/{triggerId}", accessToken);

    public async Task<JsonElement> PlaceGttOrderAsync(string accessToken, Dictionary<string, string> gttParams)
    {
        var content = new FormUrlEncodedContent(gttParams);
        return await PostAsync("/gtt/triggers", content, accessToken);
    }

    public async Task<JsonElement> ModifyGttOrderAsync(string accessToken, int triggerId, Dictionary<string, string> gttParams)
    {
        var content = new FormUrlEncodedContent(gttParams);
        return await PutAsync($"/gtt/triggers/{triggerId}", content, accessToken);
    }

    public async Task<JsonElement> DeleteGttOrderAsync(string accessToken, int triggerId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_settings.BaseUrl}/gtt/triggers/{triggerId}");
        SetAuthHeader(request, accessToken);
        return await SendAsync(request);
    }

    public async Task<JsonElement> GetHoldingsAsync(string accessToken) =>
        await GetAsync("/portfolio/holdings", accessToken);

    public async Task<JsonElement> GetPositionsAsync(string accessToken) =>
        await GetAsync("/portfolio/positions", accessToken);

    public async Task<JsonElement> ConvertPositionAsync(string accessToken, Dictionary<string, string> positionParams)
    {
        var content = new FormUrlEncodedContent(positionParams);
        return await PutAsync("/portfolio/positions", content, accessToken);
    }

    public async Task<JsonElement> GetQuoteAsync(string accessToken, IEnumerable<string> instruments)
    {
        var query = string.Join("&", instruments.Select(i => $"i={Uri.EscapeDataString(i)}"));
        return await GetAsync($"/quote?{query}", accessToken);
    }

    public async Task<JsonElement> GetOhlcAsync(string accessToken, IEnumerable<string> instruments)
    {
        var query = string.Join("&", instruments.Select(i => $"i={Uri.EscapeDataString(i)}"));
        return await GetAsync($"/quote/ohlc?{query}", accessToken);
    }

    public async Task<JsonElement> GetLtpAsync(string accessToken, IEnumerable<string> instruments)
    {
        var query = string.Join("&", instruments.Select(i => $"i={Uri.EscapeDataString(i)}"));
        return await GetAsync($"/quote/ltp?{query}", accessToken);
    }

    public async Task<JsonElement> GetInstrumentsAsync(string? exchange = null)
    {
        var path = exchange != null ? $"/instruments/{exchange}" : "/instruments";
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl}{path}");
        request.Headers.Add("X-Kite-Version", "3");
        request.Headers.Add("X-Kite-Apikey", _settings.ApiKey);
        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(JsonSerializer.Serialize(new { status = "success", data = body })).RootElement;
    }

    public async Task<JsonElement> GetHistoricalDataAsync(string accessToken, long instrumentToken, string interval, DateTime from, DateTime to, bool continuous = false, bool oi = false)
    {
        var fromStr = from.ToString("yyyy-MM-dd HH:mm:ss");
        var toStr = to.ToString("yyyy-MM-dd HH:mm:ss");
        var path = $"/instruments/historical/{instrumentToken}/{interval}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}&continuous={(continuous ? 1 : 0)}&oi={(oi ? 1 : 0)}";
        return await GetAsync(path, accessToken);
    }

    private async Task<JsonElement> GetAsync(string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl}{path}");
        if (accessToken != null) SetAuthHeader(request, accessToken);
        return await SendAsync(request);
    }

    private async Task<JsonElement> PostAsync(string path, HttpContent content, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}{path}") { Content = content };
        if (accessToken != null) SetAuthHeader(request, accessToken);
        return await SendAsync(request);
    }

    private async Task<JsonElement> PutAsync(string path, HttpContent content, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{_settings.BaseUrl}{path}") { Content = content };
        if (accessToken != null) SetAuthHeader(request, accessToken);
        return await SendAsync(request);
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request)
    {
        request.Headers.Add("X-Kite-Version", "3");
        try
        {
            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Kite API [{Method}] {Url} → {Status}", request.Method, request.RequestUri, response.StatusCode);
            return JsonDocument.Parse(body).RootElement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kite API call failed: {Url}", request.RequestUri);
            throw;
        }
    }

    private void SetAuthHeader(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("token", $"{_settings.ApiKey}:{accessToken}");
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
