namespace LmsAgent.Networking;

/// <summary>
/// LMS 서버와 합의된 웹소켓 메시지 타입 상수. 실제 서버 프로토콜에 맞춰 조정하세요.
/// </summary>
public static class MessageTypes
{
    // 인증 / 사용자 정보
    public const string AuthLogin = "auth.login";
    public const string AuthLoginResult = "auth.login.result";
    public const string UserUpdateProfile = "user.updateProfile";
    public const string UserUpdateProfileResult = "user.updateProfile.result";

    // 학사 일정
    public const string ScheduleRegister = "schedule.register";
    public const string ScheduleRegisterResult = "schedule.register.result";

    // 서버가 클라이언트에게 작업을 요청하고, 클라이언트가 결과를 회신하는 채널
    public const string TaskRequest = "task.request";
    public const string TaskResponse = "task.response";

    // 연결 유지
    public const string Ping = "ping";
    public const string Pong = "pong";

    public const string Error = "error";
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
}
