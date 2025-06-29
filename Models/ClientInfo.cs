namespace JwtApi.Models;

/// <summary>
/// Client information for audit logging
/// </summary>
public class ClientInfo
{
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}