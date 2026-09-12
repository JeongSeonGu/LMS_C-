using System;
using LmsAgent.Models;

namespace LmsAgent.Services;

/// <summary>현재 로그인한 사용자 세션 상태를 보관합니다.</summary>
public sealed class SessionManager
{
    public bool IsLoggedIn => Profile is not null;

    public string? Token { get; private set; }

    public UserProfile? Profile { get; private set; }

    public event Action? SessionChanged;

    public void SetSession(string token, UserProfile profile)
    {
        Token = token;
        Profile = profile;
        SessionChanged?.Invoke();
    }

    public void UpdateProfile(UserProfile profile)
    {
        Profile = profile;
        SessionChanged?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        Profile = null;
        SessionChanged?.Invoke();
    }
}
