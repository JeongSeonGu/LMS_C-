namespace LmsAgent.Models;

public sealed class UserProfile
{
    public string LoginId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Department { get; set; }
}
