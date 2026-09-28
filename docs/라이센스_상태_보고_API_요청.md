# 라이센스 인증 상태 보고 API — 확정본 (서버 팀 반영 완료)

> 이 문서는 원래 클라이언트(C#) 쪽에서 서버 팀에 새 API를 요청한 제안 문서였습니다.
> 서버 팀이 검토 후 구현을 완료하고 확정 계약(§10-5)을 회신해 주셨고, 아래 내용은
> 그 확정본과 C# 쪽 구현 상태를 반영해 갱신한 것입니다. 다른 문서들(`웹소켓_데이터통신
> 규칙.md` 등)처럼 서버가 정한 규약을 그대로 따르는 문서가 되었습니다.

## 1. 배경 및 목적

환경설정 &gt; 라이센스에서 각 PC는 자체적으로 인증키를 입력하고, 로그인 직후
(`LicenseGuardService.CheckAsync`) 그 값을 학교 정보의 `auth_key`와 **클라이언트 쪽에서만**
대조합니다. 불일치하면 안내 후 3분 뒤 프로그램이 스스로 종료합니다.

"정답"인 `auth_key`는 이미 서버가 쥐고 있어서 마스터가 웹에서 그 값을 바꾸면 모든 PC의
검증 기준이 즉시 바뀌지만, 그 반대 방향 — 어느 PC가 지금 어떤 키를 입력해 두었는지,
검증에 통과했는지 — 은 서버가 전혀 알 수 없었습니다. 이 API는 각 PC가 검사 결과를 서버로
보고해서, 관리자(`admin/rt-devices.html`)가 어느 PC/계정이 왜 인증에 실패했는지 화면에서
바로 확인할 수 있게 합니다.

## 2. 서버 쪽 반영 내용 (요약)

- `schoolwork_school_info`에 `auth_key` 컬럼이 **이번에 새로 추가**되었습니다(요청안
  작성 당시엔 이 컬럼 자체가 없었다는 것이 서버 팀 확인 결과 드러났습니다) —
  `sql/schoolwork_license.sql` 실행이 선행되어야 하며, 실행 전에는 아래 API가
  `{"ok":false,"message":"라이센스 상태 테이블이 없습니다..."}`를 돌려줍니다.
- 각 PC의 최신 보고를 담는 `schoolwork_license_status` 테이블이 새로 생겼습니다.
- `admin/rt-devices.html`("로컬연동 PC 관리")에 **🔑 라이센스 키 발급/변경 카드**와
  **📋 PC 별 라이센스 인증 상태 표**가 추가되었습니다. 라이센스 키 자체를 웹에서
  발급·저장하는 화면이 이제 생겼으므로, 마스터가 "키를 만들고 → 선생님들께 안내하고 →
  각 PC가 그 키로 통과했는지 표에서 확인"하는 흐름이 전부 웹에서 가능해졌습니다.
- 키 저장은 `school.php?action=license_save`라는 전용 액션으로 분리되었습니다(기존
  `action=save`는 학교정보 전체 폼 대상이라 이 화면에서 그대로 쓰면 다른 필드가 비워질
  위험이 있었기 때문). 이 액션은 C# 쪽에서 호출하지 않으므로(웹 관리자 전용) 이 문서의
  범위 밖입니다.

## 3. 확정된 요청/응답 계약

```
POST {BASE}/php/features/license_status.php
Content-Type: application/x-www-form-urlencoded
Cookie: WSSESSID=...  (기존 로그인 세션 쿠키 그대로)
```

이 서버는 파일 하나 안에서 `action` 파라미터로 기능을 나누는 방식을 쓰므로,
**`action=report`를 반드시 함께 보낸다** (요청안 원문에서 빠져 있던 부분).

### 요청 필드

| 필드 | 필수 | 값/제약 | 비고 |
|---|---|---|---|
| `action` | 필수 | 고정값 `report` | |
| `status` | 필수 | `valid` \| `invalid` \| `not_configured` | 그 외 값은 오류 응답 |
| `device_name` | 필수 | 최대 120자(UTF-8 기준, 서버가 자름) | `Environment.MachineName` 그대로 |
| `entered_key` | 선택 | 최대 120자(서버가 자름) | 그 PC에 입력돼 있던 키 |
| `app_version` | 선택 | 최대 40자(서버가 자름) | |
| `checked_at` | 선택 | ISO 8601 등 PHP `strtotime`이 읽을 수 있는 형식 | 해석 안 되면 조용히 무시(오류 아님). 서버 수신 시각은 `reported_at`으로 별도 저장 |

### 응답

```json
{ "ok": true,  "data": null, "message": "ok" }
{ "ok": false, "data": null, "message": "오류 설명" }
```

### 동작 방식

- 인증은 `WSSESSID` 세션 쿠키만 확인합니다 — 관리자 권한 불필요, 로그인된 일반
  계정이면 누구나 자기 보고를 올릴 수 있습니다.
- 같은 **(로그인 계정, device_name)** 조합으로 다시 보내면 새 값으로 덮어씁니다 —
  이력이 아니라 "그 PC의 최신 상태 1건"만 유지합니다. 재시도 부담 없이 자유롭게 다시
  호출해도 됩니다.
- 라이센스 키 값(`auth_key`) 자체를 조회하는 API는 이 범위에 없습니다 — 키 비교는
  지금처럼 C# 쪽에서 계속 자체적으로 하고, 이 API는 그 **결과만** 보고받습니다.

## 4. C# 쪽 구현 상태 — 완료

- `Networking/WorkSupportApiClient.cs`의 `ReportLicenseStatusAsync(status, deviceName,
  enteredKey, appVersion, checkedAt)` — 위 계약 그대로 `action=report` 폼 요청을 보낸다.
- `Services/LicenseGuardService.cs`의 `SetStatus(...)`가 상태를 바꿀 때마다(=매 판정
  직후) `ReportStatusAsync`를 함께 호출한다 — 로그인 직후, 그리고 환경설정에서 저장 시
  재검사할 때마다 자동으로 보고된다. `device_name`은 `Environment.MachineName`,
  `entered_key`는 `_settings.LicenseKey`, `app_version`은 실행 파일 버전, `checked_at`은
  `DateTime.Now`를 그대로 보낸다.
- 보고 실패(네트워크 오류, 서버의 SQL 미적용 등)는 `RealtimeLog`에만 남기고 조용히
  무시한다 — 부가 기능이 실제 인증 판정·3분 자동 종료 로직에 영향을 주면 안 되기 때문.
- 환경설정 &gt; 라이센스 화면의 "지금 확인" 버튼(`LicenseOptionsPage`)은 **보고하지
  않는다** — 저장 전 미리보기용 비교일 뿐이라, 입력 중인 값을 매번 서버에 보고하면
  관리자 화면에 아직 저장하지 않은 시험 삼아 눌러본 값까지 남을 수 있기 때문이다.
  실제 보고는 `LicenseGuardService`가 판정하는 시점(로그인 직후·환경설정 저장 후
  재검사)에만 이루어진다.
