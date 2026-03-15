using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Services;

public interface ISessionService
{
    Task<KiteSession?> GetActiveSessionAsync(string userId);
    Task<KiteSession> SaveSessionAsync(string userId, string accessToken, string publicToken, string refreshToken, DateTime loginTime);
    Task InvalidateSessionAsync(string userId);
    Task<bool> IsSessionValidAsync(string userId);
}

public class SessionService : ISessionService
{
    private readonly KiteDbContext _db;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<SessionService> _logger;

    public SessionService(KiteDbContext db, IOptions<KiteConnectSettings> settings, ILogger<SessionService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<KiteSession?> GetActiveSessionAsync(string userId)
    {
        return await _db.Sessions
            .Where(s => s.UserId == userId && s.IsActive && s.TokenExpiry > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<KiteSession> SaveSessionAsync(string userId, string accessToken, string publicToken, string refreshToken, DateTime loginTime)
    {
        // Deactivate any existing sessions for this user
        var existingSessions = await _db.Sessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in existingSessions)
        {
            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
        }

        // Kite access tokens expire at midnight IST (end of trading day)
        var expiry = GetNextMidnightIst();

        var newSession = new KiteSession
        {
            UserId = userId,
            AccessToken = accessToken,
            PublicToken = publicToken,
            RefreshToken = refreshToken,
            ApiKey = _settings.ApiKey,
            LoginTime = loginTime,
            TokenExpiry = expiry,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Sessions.Add(newSession);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Session saved for user {UserId}, expires {Expiry}", userId, expiry);
        return newSession;
    }

    public async Task InvalidateSessionAsync(string userId)
    {
        var sessions = await _db.Sessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Sessions invalidated for user {UserId}", userId);
    }

    public async Task<bool> IsSessionValidAsync(string userId)
    {
        return await _db.Sessions
            .AnyAsync(s => s.UserId == userId && s.IsActive && s.TokenExpiry > DateTime.UtcNow);
    }

    private static readonly TimeZoneInfo IstZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    private static DateTime GetNextMidnightIst()
    {
        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
        var midnightIst = nowIst.Date.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(midnightIst, IstZone);
    }
}
