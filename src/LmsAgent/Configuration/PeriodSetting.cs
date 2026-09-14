namespace LmsAgent.Configuration;

/// <summary>
/// 하루 시간표의 한 블록(교시 또는 점심시간)입니다. 시작/종료는 "HH:mm" 문자열로 저장합니다.
/// 쉬는 시간은 별도로 저장하지 않고, 정렬된 블록들 사이의 빈 시간으로 계산합니다.
/// </summary>
public sealed class PeriodSetting
{
    /// <summary>점심시간이 아닌 교시에서의 순번(1교시, 2교시, ...). 점심시간 항목은 사용하지 않습니다.</summary>
    public int Index { get; set; }

    public string Start { get; set; } = "";

    public string End { get; set; } = "";

    public bool IsLunch { get; set; }

    public string Label => IsLunch ? "점심시간" : $"{Index}교시";
}
