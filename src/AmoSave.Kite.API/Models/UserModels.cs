namespace AmoSave.Kite.API.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserShortname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Broker { get; set; } = string.Empty;
    public List<string> Exchanges { get; set; } = new();
    public List<string> Products { get; set; } = new();
    public List<string> OrderTypes { get; set; } = new();
    public string AvatarUrl { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class UserMargin
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public decimal Net { get; set; }
    public decimal Available { get; set; }
    public decimal Utilised { get; set; }
    public string RawData { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
