using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using LmsAgent.Models.WorkSupport;
using LmsAgent.Networking;

namespace LmsAgent.Services;

/// <summary>
/// 현재 로그인한 사용자 세션과, 학사 일정 권한 판단에 필요한
/// 담당업무(부서) 목록·본인 담당업무 집합을 함께 관리합니다.
/// </summary>
public sealed class SessionManager
{
    public bool IsLoggedIn => Profile is not null;

    public WorkSupportUser? Profile { get; private set; }

    public List<SchoolDepartment> Departments { get; private set; } = new();

    /// <summary>로그인한 교사의 담당업무(여러 개 가능) 상세. 교사 레코드가 없으면 null.</summary>
    public TeacherSummary? MyTeacher { get; private set; }

    /// <summary>본인이 담당하는 업무(부서) id 집합. 학사 일정 등록/수정/삭제 권한 판단에 사용합니다.</summary>
    public HashSet<int> MyDeptIds { get; private set; } = new();

    public bool IsAdmin => Profile?.IsAdmin ?? false;

    public event Action? SessionChanged;

    /// <summary>학사 일정이 등록/수정/삭제된 직후 발생합니다. 배경화면 오버레이 즉시 갱신,
    /// 웹소켓을 통한 웹페이지 새로고침 알림 등에 사용합니다.</summary>
    public event Action? ScheduleChanged;

    public void NotifyScheduleChanged() => ScheduleChanged?.Invoke();

    public void SetSession(WorkSupportUser profile)
    {
        Profile = profile;
        MyDeptIds = profile.DeptId.HasValue ? new HashSet<int> { profile.DeptId.Value } : new HashSet<int>();
        SessionChanged?.Invoke();
    }

    /// <summary>
    /// 로그인 직후 호출: 담당업무 전체 목록과, 본인이 여러 업무를 맡고 있을 경우를 대비해
    /// school_teacher_departments(N:M) 기준의 정확한 담당업무 집합을 다시 채웁니다.
    ///
    /// ⚠ 반드시 ConfigureAwait(false)를 쓰지 않아야 한다(원래는 썼었다) — 이 메서드가 끝에서
    /// 부르는 SessionChanged는 UI를 직접 건드리는 구독자(WorkJournalService의 쪽지 자동
    /// 복원 등)를 갖고 있는데, ConfigureAwait(false)로 스레드풀 스레드에서 계속 실행되다가
    /// 마지막에 SessionChanged를 호출하면 그 구독자들도 스레드풀 스레드에서 실행돼
    /// "크로스 스레드 작업이 잘못되었습니다" 예외가 난다(로그인 화면에서 실제로 겪은 버그 —
    /// LoginForm.OnLoginClicked가 이 메서드를 await하고 있어서 그 예외가 로그인 오류로
    /// 그대로 올라와 보였다). 이 메서드는 항상 UI 스레드(로그인 화면)에서 호출되므로,
    /// ConfigureAwait 기본값(호출한 스레드로 복귀)을 그대로 써야 SessionChanged도 UI
    /// 스레드에서 안전하게 실행된다.
    /// </summary>
    public async Task RefreshDepartmentContextAsync(WorkSupportApiClient api)
    {
        if (Profile is null)
        {
            return;
        }

        var deptResult = await api.GetDepartmentsAsync();
        if (deptResult.Ok && deptResult.Data is not null)
        {
            Departments = deptResult.Data;
        }

        if (Profile.TeacherId is int teacherId)
        {
            var teacherResult = await api.GetTeacherAsync(teacherId);
            if (teacherResult.Ok && teacherResult.Data is not null)
            {
                MyTeacher = teacherResult.Data;
            }
        }

        // ws_me.php의 dept_ids가 "내 담당업무 전체"의 정확한 출처다(웹소켓_데이터통신규칙.md §7-A).
        // teachers.php는 teacher_id가 있는 계정만 조회되므로, 행정실 등 교사가 아닌 계정은
        // dept_id 하나뿐인 것으로 잘못 판단될 수 있다. dept_ids를 최우선으로 쓰고,
        // 값이 없을 때만 teachers.php → 대표 dept_id 순으로 대체한다.
        var meResult = await api.GetMeAsync();
        if (meResult.Ok && meResult.Data?.DeptIds is { Count: > 0 } deptIds)
        {
            MyDeptIds = new HashSet<int>(deptIds);
        }
        else if (MyTeacher?.DeptIds is { Count: > 0 } teacherDeptIds)
        {
            MyDeptIds = new HashSet<int>(teacherDeptIds);
        }
        else if (Profile.DeptId.HasValue)
        {
            MyDeptIds = new HashSet<int> { Profile.DeptId.Value };
        }

        SessionChanged?.Invoke();
    }

    /// <summary>지정한 담당업무로 학사 일정을 등록/수정/삭제할 수 있는지 여부. 관리자는 항상 가능하고,
    /// deptId가 null(관련 업무 없음)이면 누구나 등록은 가능하되 수정/삭제는 작성 화면에서 별도로 판단합니다.</summary>
    public bool CanUseDept(int? deptId)
    {
        if (IsAdmin)
        {
            return true;
        }

        return deptId is int id && MyDeptIds.Contains(id);
    }

    public IReadOnlyList<SchoolDepartment> GetSelectableDepartmentsForNewEvent()
    {
        if (IsAdmin)
        {
            return Departments;
        }

        return Departments.Where(d => MyDeptIds.Contains(d.Id)).ToList();
    }

    /// <summary>로그인 여부와 무관하게 담당업무(부서) 목록만 채웁니다. 이미 불러왔으면 다시 요청하지 않습니다.
    /// 학사달력 배경화면 오버레이 등 로그인 전에도 동작해야 하는 기능에서 사용합니다.</summary>
    public async Task EnsureDepartmentsLoadedAsync(WorkSupportApiClient api)
    {
        if (Departments.Count > 0)
        {
            return;
        }

        var result = await api.GetDepartmentsAsync().ConfigureAwait(false);
        if (result.Ok && result.Data is not null)
        {
            Departments = result.Data;
        }
    }

    private static readonly Color DefaultDeptColor = Color.FromArgb(154, 160, 166);

    /// <summary>담당업무 id → 서버에 등록된 실제 색상. DB에 저장된 값과 화면 표시 색상을 일치시킵니다.</summary>
    public Dictionary<int, Color> GetDepartmentColorMap()
    {
        var map = new Dictionary<int, Color>();
        foreach (var dept in Departments)
        {
            map[dept.Id] = ColorHelper.ParseHexOrDefault(dept.Color, DefaultDeptColor);
        }

        return map;
    }

    public void Clear()
    {
        Profile = null;
        Departments = new List<SchoolDepartment>();
        MyTeacher = null;
        MyDeptIds = new HashSet<int>();
        SessionChanged?.Invoke();
    }
}
