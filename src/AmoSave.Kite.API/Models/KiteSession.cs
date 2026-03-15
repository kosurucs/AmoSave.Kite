namespace AmoSave.Kite.API.Models;

public class KiteSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string PublicToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public DateTime TokenExpiry { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
