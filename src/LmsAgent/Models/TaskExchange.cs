namespace LmsAgent.Models;

/// <summary>서버가 클라이언트에게 작업을 요청할 때 보내는 내용입니다.</summary>
public sealed class TaskRequestPayload
{
    public string TaskId { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string? Title { get; set; }
    public string? Description { get; set; }
}

/// <summary>클라이언트가 작업 요청에 대해 회신하는 내용입니다.</summary>
public sealed class TaskResponsePayload
{
    public string TaskId { get; set; } = "";
    public bool Accepted { get; set; }
    public string? ResultMessage { get; set; }
}
