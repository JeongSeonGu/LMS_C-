using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LmsAgent.Models.WorkSupport;

namespace LmsAgent.Networking;

/// <summary>
/// WorkSupport(교무업무 지원) PHP 웹 서비스의 HTTP API 클라이언트.
///
/// 서버는 순수 세션 쿠키(WSSESSID) 기반 인증을 사용하므로(JWT 아님),
/// 로그인 이후 발급되는 쿠키를 <see cref="CookieContainer"/>로 계속 유지해서
/// 이후 모든 요청에 자동으로 실어 보낸다.
///
/// 기준 경로는 서버의 WS_COOKIE_PATH 상수와 동일한 "/SchoolWork/WorkSupport" 이며,
/// 호스트는 환경설정의 "웹소켓 서버" 주소에서 스킴만 http(s)로 바꿔 그대로 사용한다
/// (같은 서버가 웹소켓과 웹 서비스를 함께 제공하는 구성을 전제로 한다).
/// </summary>
public sealed class WorkSupportApiClient : IDisposable
{
    private const string BasePath = "/SchoolWork/WorkSupport";

    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Uri BaseUri { get; }

    /// <param name="serverUrl">웹소켓 서버 주소. apiBaseUrlOverride가 없으면 이 주소에서 API 기준 주소를 유도한다.</param>
    /// <param name="apiBaseUrlOverride">
    /// WorkSupport 웹 서비스의 실제 접속 주소(예: "https://school.example.com/SchoolWork/WorkSupport").
    /// 웹소켓 서버와 호스트/경로가 다를 때 환경설정 &gt; 네트워크에서 직접 지정할 수 있다.
    /// </param>
    public WorkSupportApiClient(string serverUrl, string? apiBaseUrlOverride = null)
    {
        BaseUri = !string.IsNullOrWhiteSpace(apiBaseUrlOverride)
            ? new Uri(apiBaseUrlOverride.EndsWith('/') ? apiBaseUrlOverride : apiBaseUrlOverride + "/")
            : ComputeApiBaseUri(serverUrl);

        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    /// <summary>
    /// "ws://host:port/ws" / "wss://host/ws" 형태의 웹소켓 주소를
    /// "http://host:port" / "https://host" 형태의 API 기준 주소로 변환한다.
    /// </summary>
    public static Uri ComputeApiBaseUri(string serverUrl)
    {
        var wsUri = new Uri(serverUrl);
        var scheme = wsUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        var builder = new UriBuilder(wsUri) { Scheme = scheme, Path = "", Query = "" };

        // UriBuilder는 스킴 변경 시 기본 포트를 다시 지정해줘야 http/https 표준 포트로 정리된다.
        if (wsUri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return new Uri(builder.Uri, BasePath + "/");
    }

    private Uri Resolve(string relativePath) => new(BaseUri, relativePath);

    /* =========================================================
     * 인증
     * ========================================================= */

    public async Task<ApiEnvelope<LoginResultData>> LoginAsync(string loginId, string password)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["login_id"] = loginId,
            ["login_pw"] = password,
        });

        return await PostFormAsync<LoginResultData>("php/auth/ws_login.php", form).ConfigureAwait(false);
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await _http.GetAsync(Resolve("php/auth/ws_logout.php")).ConfigureAwait(false);
        }
        catch
        {
            // 로그아웃 통신 실패는 클라이언트 쪽 세션 정리를 막지 않는다.
        }
    }

    public async Task<ApiEnvelope<MeResult>> GetMeAsync()
    {
        return await GetJsonAsync<MeResult>("php/auth/ws_me.php?peek=1").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<ProfileInfo>> GetProfileAsync()
    {
        return await GetJsonAsync<ProfileInfo>("php/auth/profile.php?action=get").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<object>> UpdateProfileAsync(
        string contact, string loginId, string currentPassword, string newPassword)
    {
        var fields = new Dictionary<string, string>
        {
            ["action"] = "update",
            ["contact"] = contact,
            ["login_id"] = loginId,
            ["current_pw"] = currentPassword,
            ["new_pw"] = newPassword,
        };

        return await PostFormAsync<object>("php/auth/profile.php", new FormUrlEncodedContent(fields))
            .ConfigureAwait(false);
    }

    /* =========================================================
     * 담당업무 / 교사
     * ========================================================= */

    public async Task<ApiEnvelope<List<SchoolDepartment>>> GetDepartmentsAsync()
    {
        return await GetJsonAsync<List<SchoolDepartment>>(
            "SchoolCalendar/php/api/departments.php?action=list").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<TeacherSummary>> GetTeacherAsync(int teacherId)
    {
        return await GetJsonAsync<TeacherSummary>(
            $"SchoolCalendar/php/api/teachers.php?action=get&id={teacherId}").ConfigureAwait(false);
    }

    /* =========================================================
     * 학사 일정
     * ========================================================= */

    public async Task<ApiEnvelope<List<SchoolEvent>>> GetEventsAsync(int year, int month)
    {
        return await GetJsonAsync<List<SchoolEvent>>(
            $"SchoolCalendar/php/api/events.php?action=list&year={year}&month={month}").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<EventWriteResult>> AddEventAsync(SchoolEvent ev)
    {
        var request = ToWriteRequest("add", ev);
        return await PostJsonAsync<EventWriteRequest, EventWriteResult>(
            "SchoolCalendar/php/api/events.php", request).ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<EventWriteResult>> UpdateEventAsync(SchoolEvent ev)
    {
        var request = ToWriteRequest("update", ev);
        return await PostJsonAsync<EventWriteRequest, EventWriteResult>(
            "SchoolCalendar/php/api/events.php", request).ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<EventWriteResult>> DeleteEventAsync(int id)
    {
        var request = new EventDeleteRequest { Action = "delete", Id = id };
        return await PostJsonAsync<EventDeleteRequest, EventWriteResult>(
            "SchoolCalendar/php/api/events.php", request).ConfigureAwait(false);
    }

    private static EventWriteRequest ToWriteRequest(string action, SchoolEvent ev) => new()
    {
        Action = action,
        Id = ev.Id > 0 ? ev.Id : null,
        Title = ev.Title,
        Start = ev.Start,
        End = ev.End,
        AllDay = ev.AllDay,
        DeptId = ev.DeptId,
        Location = ev.Location,
        Note = ev.Note,
        NotifyBefore = ev.NotifyBefore,
        // events.php의 addEvent()는 createdBy가 비어 있으면 미리 초기화되지 않은 $pdo를
        // 참조하는 서버측 결함이 있어(관리자 계정 fallback 조회), 항상 값을 채워 보낸다.
        CreatedBy = ev.CreatedBy,
    };

    /* =========================================================
     * 복무 (연가/출장/조퇴)
     * ========================================================= */

    public async Task<ApiEnvelope<List<DutyRecord>>> GetDutyStatusAsync(int year, int month)
    {
        return await GetJsonAsync<List<DutyRecord>>(
            $"SchoolCalendar/php/api/duty_status.php?action=list&year={year}&month={month}").ConfigureAwait(false);
    }

    /// <summary>현재 로그인 계정이 복무사항을 기록할 수 있는지(role=admin 또는 교장/교감/교무부장/행정실장) 확인합니다.</summary>
    public async Task<ApiEnvelope<DutyManageInfo>> GetDutyCanManageAsync()
    {
        return await GetJsonAsync<DutyManageInfo>(
            "SchoolCalendar/php/api/duty_status.php?action=can_manage").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<DutyWriteResult>> AddDutyAsync(DutyRecord record)
    {
        var request = ToDutyWriteRequest("add", record);
        return await PostJsonAsync<DutyWriteRequest, DutyWriteResult>(
            "SchoolCalendar/php/api/duty_status.php", request).ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<DutyWriteResult>> UpdateDutyAsync(DutyRecord record)
    {
        var request = ToDutyWriteRequest("update", record);
        return await PostJsonAsync<DutyWriteRequest, DutyWriteResult>(
            "SchoolCalendar/php/api/duty_status.php", request).ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<DutyWriteResult>> DeleteDutyAsync(int id)
    {
        var request = new DutyDeleteRequest { Action = "delete", Id = id };
        return await PostJsonAsync<DutyDeleteRequest, DutyWriteResult>(
            "SchoolCalendar/php/api/duty_status.php", request).ConfigureAwait(false);
    }

    private static DutyWriteRequest ToDutyWriteRequest(string action, DutyRecord r) => new()
    {
        Action = action,
        Id = r.Id > 0 ? r.Id : null,
        Date = r.Date,
        Position = r.Position,
        DutyType = r.DutyType,
        TimeStart = r.TimeStart,
        TimeEnd = r.TimeEnd,
        Note = r.Note,
    };

    /* =========================================================
     * 기본정보 > 학교기본정보 (조회 전용)
     * ========================================================= */

    public async Task<ApiEnvelope<SchoolInfoResult>> GetSchoolInfoAsync()
    {
        return await GetJsonAsync<SchoolInfoResult>("php/features/school.php?action=get").ConfigureAwait(false);
    }

    /* =========================================================
     * 기본정보 > 공통계정 (조회 전용, 비밀번호는 "보기"로만 확인)
     * ========================================================= */

    public async Task<ApiEnvelope<SharedAccountListResult>> GetSharedAccountsAsync()
    {
        return await GetJsonAsync<SharedAccountListResult>("php/features/accounts.php?action=list").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<SharedAccountSecret>> RevealSharedAccountAsync(int id)
    {
        return await GetJsonAsync<SharedAccountSecret>($"php/features/accounts.php?action=reveal&id={id}")
            .ConfigureAwait(false);
    }

    /* =========================================================
     * 기본정보 > 요청사항 (조회/등록, 본인 것만 수정/삭제)
     * ========================================================= */

    public async Task<ApiEnvelope<RequestMeta>> GetRequestMetaAsync()
    {
        return await GetJsonAsync<RequestMeta>("php/features/requests.php?action=meta").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<RequestListResult>> GetRequestsAsync(string scope = "all", string? category = null)
    {
        var path = $"php/features/requests.php?action=list&scope={Uri.EscapeDataString(scope)}";
        if (!string.IsNullOrWhiteSpace(category))
        {
            path += $"&category={Uri.EscapeDataString(category)}";
        }

        return await GetJsonAsync<RequestListResult>(path).ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<RequestDetailResult>> GetRequestDetailAsync(int id)
    {
        return await GetJsonAsync<RequestDetailResult>($"php/features/requests.php?action=detail&id={id}")
            .ConfigureAwait(false);
    }

    /// <summary>대상은 항상 "전체 공개"로 등록합니다(개별 수신자 지정 UI는 제공하지 않습니다).</summary>
    public async Task<ApiEnvelope<object>> SaveRequestAsync(
        int id, string title, string body, string category, string priority, string status, string? dueDate)
    {
        var fields = new Dictionary<string, string>
        {
            ["action"] = "save",
            ["id"] = id > 0 ? id.ToString() : "0",
            ["title"] = title,
            ["body"] = body,
            ["category"] = category,
            ["priority"] = priority,
            ["status"] = status,
            ["target_type"] = "all",
            ["notify_app"] = "0",
            ["notify_sms"] = "0",
        };
        if (!string.IsNullOrWhiteSpace(dueDate))
        {
            fields["due_date"] = dueDate;
        }

        return await PostFormAsync<object>("php/features/requests.php", new FormUrlEncodedContent(fields))
            .ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<object>> DeleteRequestAsync(int id)
    {
        var fields = new Dictionary<string, string> { ["action"] = "delete", ["id"] = id.ToString() };
        return await PostFormAsync<object>("php/features/requests.php", new FormUrlEncodedContent(fields))
            .ConfigureAwait(false);
    }

    /* =========================================================
     * 기본정보 > 협의사항 (조회 전용 — 서버가 Google 시트를 그대로 읽어오는 구조라
     * 등록/수정/삭제 API가 없습니다. 새 안건 등록은 설정된 외부 링크로 안내합니다.)
     * ========================================================= */

    public async Task<ApiEnvelope<MeetingsListResult>> GetMeetingsAsync(string source = "recent")
    {
        return await GetJsonAsync<MeetingsListResult>(
            $"php/features/meetings.php?action=list&source={Uri.EscapeDataString(source)}").ConfigureAwait(false);
    }

    public async Task<ApiEnvelope<MeetingsTodoResult>> GetMeetingsTodoAsync()
    {
        return await GetJsonAsync<MeetingsTodoResult>("php/features/meetings.php?action=todo").ConfigureAwait(false);
    }

    /* =========================================================
     * 기본정보 > 길라잡이 (문서 카탈로그 조회 + 다운로드)
     * ========================================================= */

    public async Task<ApiEnvelope<GuideCatalogResult>> GetGuideCatalogAsync()
    {
        return await GetJsonAsync<GuideCatalogResult>("php/public/catalog.php").ConfigureAwait(false);
    }

    /* =========================================================
     * 첫 화면 공지사항 요약용
     * ========================================================= */

    public async Task<ApiEnvelope<TrainingMyResult>> GetMyTrainingAsync()
    {
        return await GetJsonAsync<TrainingMyResult>("php/features/training.php?action=my").ConfigureAwait(false);
    }

    /* =========================================================
     * 인증이 필요한 파일 다운로드(길라잡이 문서, 학교 로고 등)
     * 서버가 내려주는 경로는 "/SchoolWork/WorkSupport/..." 형태의 호스트 기준 절대 경로입니다.
     * ========================================================= */

    /// <summary>서버가 내려준 호스트 기준 절대 경로를 현재 접속 호스트의 완전한 URL로 변환합니다.</summary>
    public Uri ResolveServerPath(string absoluteServerPath) => new(BaseUri, absoluteServerPath);

    public async Task<(byte[] Bytes, string? ContentType, string? FileName)> DownloadFileAsync(Uri uri)
    {
        using var response = await _http.GetAsync(uri).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName;

        return (bytes, contentType, fileName?.Trim('"'));
    }

    /* =========================================================
     * 공통 HTTP 헬퍼
     * ========================================================= */

    private async Task<ApiEnvelope<T>> GetJsonAsync<T>(string relativePath)
    {
        using var response = await _http.GetAsync(Resolve(relativePath)).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return Deserialize<T>(json, response.StatusCode, Resolve(relativePath));
    }

    private async Task<ApiEnvelope<T>> PostFormAsync<T>(string relativePath, FormUrlEncodedContent content)
    {
        using var response = await _http.PostAsync(Resolve(relativePath), content).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return Deserialize<T>(json, response.StatusCode, Resolve(relativePath));
    }

    private async Task<ApiEnvelope<TRes>> PostJsonAsync<TReq, TRes>(string relativePath, TReq body)
    {
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(Resolve(relativePath), content).ConfigureAwait(false);
        var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return Deserialize<TRes>(responseJson, response.StatusCode, Resolve(relativePath));
    }

    /// <summary>
    /// 응답이 유효한 JSON 봉투가 아닐 때, 원인을 바로 알 수 있도록 요청 주소·HTTP 상태·
    /// 응답 앞부분을 그대로 메시지에 담아 반환한다(서버 주소 오설정, PHP 경고 혼입, 404 HTML 등 진단용).
    /// </summary>
    private static ApiEnvelope<T> Deserialize<T>(string json, HttpStatusCode statusCode, Uri requestUri)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(json, JsonOptions);
            if (envelope is not null)
            {
                return envelope;
            }
        }
        catch (JsonException)
        {
            // 아래에서 진단 메시지로 대체한다.
        }

        var snippet = json.Length > 300 ? json[..300] + "…" : json;
        if (string.IsNullOrWhiteSpace(snippet))
        {
            snippet = "(빈 응답)";
        }

        return new ApiEnvelope<T>
        {
            Ok = false,
            Message =
                $"서버 응답 형식이 올바르지 않습니다. (HTTP {(int)statusCode}, 요청 주소: {requestUri})\n" +
                $"환경설정 > 네트워크의 서버 주소가 올바른지 확인하세요.\n응답 내용: {snippet}",
        };
    }

    public void Dispose() => _http.Dispose();

    private sealed class EventWriteRequest
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("start")] public string Start { get; set; } = "";
        [JsonPropertyName("end")] public string End { get; set; } = "";
        [JsonPropertyName("allDay")] public bool AllDay { get; set; }
        [JsonPropertyName("deptId")] public int? DeptId { get; set; }
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
        [JsonPropertyName("notifyBefore")] public int NotifyBefore { get; set; }
        [JsonPropertyName("createdBy")] public int? CreatedBy { get; set; }
    }

    private sealed class EventDeleteRequest
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("id")] public int Id { get; set; }
    }

    private sealed class DutyWriteRequest
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("id")] public int? Id { get; set; }
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("position")] public string Position { get; set; } = "";
        [JsonPropertyName("dutyType")] public string DutyType { get; set; } = "";
        [JsonPropertyName("timeStart")] public string? TimeStart { get; set; }
        [JsonPropertyName("timeEnd")] public string? TimeEnd { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
    }

    private sealed class DutyDeleteRequest
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "";
        [JsonPropertyName("id")] public int Id { get; set; }
    }
}

public sealed class EventWriteResult
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
}

public sealed class DutyWriteResult
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
}

/// <summary>php/auth/ws_me.php 응답의 data 필드.</summary>
public sealed class MeResult
{
    [JsonPropertyName("user")] public WorkSupportUser? User { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}
