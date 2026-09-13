using LmsAgent.Configuration;

namespace LmsAgent.Forms.OptionsPages;

/// <summary>OptionsForm 오른쪽 영역에 표시되는 설정 소분류 페이지의 공통 계약입니다.</summary>
public interface IOptionsPage
{
    string CategoryName { get; }

    void LoadFrom(AppSettings settings);

    /// <summary>사용자가 화면에서 고른 값을 settings 객체에 반영합니다. 유효성 오류가 있으면 예외 대신
    /// <see cref="Validate"/>에서 먼저 걸러집니다.</summary>
    void SaveTo(AppSettings settings);

    /// <summary>저장 전 검증. 문제가 있으면 사용자에게 보여줄 메시지를 반환하고, 문제 없으면 null을 반환합니다.</summary>
    string? Validate() => null;
}
