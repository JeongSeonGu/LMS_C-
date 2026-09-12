namespace LmsAgent.Models;

public sealed class LoginRequest
{
    public string LoginId { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DeviceId { get; set; }
}

public sealed class LoginResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Token { get; set; }
    public UserProfile? Profile { get; set; }
}

public sealed class UpdateProfileRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public sealed class GenericResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
