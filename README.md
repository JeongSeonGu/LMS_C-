# LMS_C-

LMS연동 윈도우 프로그램

Windows 로그인 시 자동 실행되어 트레이(작업 표시줄 알림 영역)에 상주하며,
학교의 **WorkSupport(교무업무 지원) 웹 서비스**와 통신해 학사 일정을 등록·조회하고,
복무(출장/연가) 변동사항을 안내하며, 요청사항·학교기본정보·공통계정·협의사항·길라잡이
문서함까지 조회하고, 서버로부터 작업을 요청받아 처리하는 상시 실행형 클라이언트입니다.

이번 갱신은 실제 WorkSupport 서비스 코드(PHP + MySQL, `SchoolWork/WorkSupport`)를
분석해서 얻은 진짜 API 스펙을 기준으로 구현했습니다. 로그인·학사일정·복무 기본 기능에 더해,
구글 캘린더 스타일의 달력 UI, 담당업무 색상의 DB 연동, 우클릭 메뉴의 "기본정보" 5종
화면, 오버레이 투명도 설정, 로그인 직후 공지 요약 팝업을 추가했습니다.

## 기술 스택

- .NET 8 (Windows Forms), C# 12
- `System.Net.WebSockets.ClientWebSocket` — node2.future-class.kr 실시간 연결(작업 요청/응답)
- `HttpClient` + 쿠키 세션(`WSSESSID`) — WorkSupport PHP API 연동
- 트레이 아이콘 상주 방식 (메인 창 없이 `ApplicationContext`로 실행)

## 프로젝트 구조

```
src/LmsAgent/
  Program.cs                     진입점: 중복 실행 방지, 업데이트 확인, 트레이 실행
  Resources/AppIcon.ico          프로그램 기본 아이콘 / 트레이 아이콘
                                  (WorkSupport 코드의 viewer/externalLectureViewer.ico)
  App/TrayApplicationContext.cs  트레이 아이콘과 전체 메뉴, 백그라운드 서비스 구동
  Configuration/                 AppSettings(환경설정 항목), 로컬 설정 파일(JSON) 저장/로드
  Networking/
    WebSocketClientService.cs    node2.future-class.kr 웹소켓 연결(작업 요청/응답, 재연결)
    WorkSupportApiClient.cs      WorkSupport PHP API 클라이언트 (쿠키 세션 유지, 파일 다운로드)
    WsEnvelope.cs / MessageTypes.cs  웹소켓 메시지 봉투/타입
  Models/
    WorkSupport/                 서버 API 요청/응답 모델
                                  (User, Department, Event, Duty, SchoolInfo, SharedAccount,
                                   Request, Meetings, GuideCatalog, Training/요약용 모델)
    TaskExchange.cs               웹소켓 작업 요청/응답 페이로드
  Services/
    SessionManager.cs            로그인 세션, 담당업무(부서) 목록·색상, 권한 판정
    AutoStartManager.cs          Windows 시작 프로그램 등록(레지스트리)
    UpdateService.cs             업데이트 확인/다운로드/적용
    DutyNotificationService.cs   복무(출장/연가) 알림 배너(투명도 적용)
    ScheduleOverlayService.cs    학사달력 배경화면형 오버레이(DB 색상·투명도 적용)
    AutoPrintService.cs          평일 08:30~10:00 일정 자동 인쇄
    PrintingService.cs           프린터 출력 렌더링
    DisplayHelper.cs             다중 모니터 열거
    ColorHelper.cs                "#rrggbb" 문자열 → Color 안전 변환
    AppIconProvider.cs           임베디드 아이콘 로드
    UiTheme.cs                   화면 전체에 적용하는 하늘색·오렌지색 테마(팔레트·글꼴·버튼/그리드/트리/메뉴 스타일)
  Forms/
    LoginForm.cs / UserInfoForm.cs             로그인 / 개인정보 수정
    ScheduleRegisterForm.cs                    학사 일정 등록·수정(구글 캘린더 스타일 종일/시간 입력)
    ScheduleListForm.cs / MonthCalendarView.cs / EventDetailForm.cs
                                                학사 일정 월간 달력 보기, 일정 칩, 상세 팝업
    OptionsForm.cs / OptionsPages/*.cs         환경설정(Visual Studio 옵션 창 스타일)
    DutyBannerForm.cs / ScheduleOverlayForm.cs 복무 알림 배너 / 학사달력 오버레이 창
    SchoolInfoForm.cs                          기본정보 &gt; 학교기본정보(조회 전용 모달)
    SharedAccountsForm.cs                      기본정보 &gt; 공통계정(조회 전용 + 비밀번호 보기)
    RequestsForm.cs / RequestEditForm.cs       기본정보 &gt; 요청사항(조회/등록/본인 것 수정·삭제)
    MeetingsForm.cs                            기본정보 &gt; 협의사항(조회 전용)
    GuideDocsForm.cs                           기본정보 &gt; 길라잡이 조회(문서함 트리 + 다운로드)
    StartupSummaryForm.cs                      로그인 직후 공지 요약 팝업
    TaskRequestForm.cs           웹소켓으로 들어온 작업 요청 수락/거절 창
  Interop/NativeMethods.cs       오버레이 창을 배경으로 보내기 위한 최소 P/Invoke
```

## 메뉴 구성

프로그램은 실행 후 별도 창 없이 트레이 아이콘 상태로 최소화되어 상주합니다.
트레이 아이콘을 우클릭하면 아래 메뉴가 나타납니다.

- 연결 상태 표시 (읽기 전용, 웹소켓 연결 상태)
- **학사 일정**
  - 일정 등록...
  - 일정 목록... (구글 캘린더 스타일 월간 보기, 등록/수정/삭제)
- **사용자 정보**
  - 로그인...
  - 정보 수정... (로그인 후 활성화)
- **기본정보**
  - 요청사항
  - 학교기본정보
  - 공통계정
  - 협의사항
  - 길라잡이 조회
- 환경설정... (Visual Studio 옵션 창 스타일 — 아래 참고)
- 업데이트 확인...
- 종료

## WorkSupport API 분석 및 연동

첨부해 주신 `WorkSupport.zip`(교무업무 지원 웹 서비스, PHP + MySQL)을 분석해
아래 실제 엔드포인트를 그대로 사용합니다. 인증은 JWT가 아니라 **세션 쿠키(`WSSESSID`)**
방식이라, `WorkSupportApiClient`가 `CookieContainer`로 로그인 쿠키를 계속 유지하며
이후 모든 요청에 함께 실어 보냅니다.

**웹소켓 서버와 API 서버는 서로 다른 호스트입니다.** 웹소켓(`node2.future-class.kr`)은
서버가 클라이언트에 작업을 요청/응답하는 실시간 연동 전용이고, 학사 일정·로그인 등
실제 데이터 API는 별도의 웹 서버(`https://future-class.kr/SchoolWork/WorkSupport`)를
통해 이루어집니다. 이 주소는 환경설정 &gt; 네트워크의 "WorkSupport 서버 주소"에
기본값으로 들어 있으며(`AppSettings.ApiBaseUrlOverride`), 값을 비우면 예전처럼
웹소켓 서버 주소에서 스킴만 바꿔 유도하는 방식으로 대체됩니다.

### 학사 일정 / 로그인 / 복무

| 기능 | 메서드/경로 | 설명 |
|---|---|---|
| 로그인 | `POST php/auth/ws_login.php` (`login_id`,`login_pw`) | 성공 시 `WSSESSID` 쿠키 발급, 통합 프로필 반환 |
| 로그아웃 | `GET/POST php/auth/ws_logout.php` | 세션 파기 |
| 개인정보 조회 | `GET php/auth/profile.php?action=get` | 이름/직위/담당업무/아이디/연락처 |
| 개인정보 수정 | `POST php/auth/profile.php` (`action=update`, `contact`,`login_id`,`current_pw`,`new_pw`) | 현재 비밀번호 확인 필요 |
| 담당업무 목록 | `GET SchoolCalendar/php/api/departments.php?action=list` | 학사 일정에 연결할 "업무" 목록과 **색상**(`color`) |
| 교사 상세(담당업무 다건) | `GET SchoolCalendar/php/api/teachers.php?action=get&id=` | school_teacher_departments(N:M) 기준 본인 담당업무 전체 조회 |
| 학사 일정 목록/등록/수정/삭제 | `SchoolCalendar/php/api/events.php` (`action=list\|add\|update\|delete`) | 학사 일정 CRUD, `deptId`가 담당업무 |
| 복무(연가/출장/조퇴) 목록 | `GET SchoolCalendar/php/api/duty_status.php?action=list` | 교장/교감 등 복무 변동사항 |

### 기본정보 메뉴 (신규)

| 화면 | 메서드/경로 | 비고 |
|---|---|---|
| 학교기본정보 | `GET php/features/school.php?action=get` (+`&action=logo`) | 조회 전용. 등록/수정은 관리자 웹 화면(`admin/school.html`)에서만 |
| 공통계정 | `GET php/features/accounts.php?action=list`, `?action=reveal&id=` | 목록·비밀번호 "보기"만 제공. 등록/수정/삭제는 관리자 전용(서버 정책) |
| 요청사항 | `php/features/requests.php` (`action=list\|detail\|save\|delete\|meta`) | 조회·등록 자유, **본인 작성 글만** 수정/삭제. 대상은 항상 "전체 공개"로 등록(개별 수신자 지정 UI 없음) |
| 협의사항 | `GET php/features/meetings.php?action=list&source=recent\|archive`, `?action=todo` | **조회 전용** — 서버가 Google 시트를 그대로 읽어오는 구조라 등록/수정/삭제 API 자체가 없습니다. "안건 등록" 버튼은 관리자가 설정해 둔 외부(Apps Script) 링크를 기본 브라우저로 엽니다 |
| 길라잡이 조회 | `GET php/public/catalog.php`, `GET php/public/file.php?version_id=` | 대분류 &gt; 소분류 &gt; 문서 트리, 로그인 세션으로 인증된 다운로드 |

> **협의사항은 명세와 달리 조회만 가능합니다.** 요청하신 스펙은 "조회·등록·본인 글 수정/삭제"였지만,
> 실제 서버(`meetings.php`)에는 Google 시트 설정을 관리자가 저장하는 API만 있고 안건 자체를
> 쓰는 API가 없습니다(정책 주석에도 "본 서비스에서는 조회만 합니다"라고 명시되어 있습니다).
> 서버에 쓰기 API가 추가되면 클라이언트도 맞춰 확장할 수 있습니다.

## 학사 일정 화면

### 구글 캘린더 스타일 입력

일정 등록/수정 창에서 **"종일" 체크박스**로 시간 입력 여부를 전환합니다.
- 체크 시: 날짜만 입력합니다(시간 입력란이 숨겨짐).
- 해제 시: 시작/종료 각각 날짜와 시간을 따로 입력하며, 시작을 종료보다 늦게 바꾸면
  구글 캘린더처럼 종료가 자동으로 1시간 뒤로 조정됩니다.

### 월간 달력 보기

기존의 목록(리스트) 방식 대신 **구글 캘린더 스타일 월간 달력**(`MonthCalendarView`)으로 바뀌었습니다.
- 담당업무 색상은 `departments.php`가 내려주는 실제 DB 색상(`color`)을 그대로 사용합니다(하드코딩 없음).
- 빈 날짜를 클릭하면 그 날짜로 새 일정 등록 창이, 일정 칩을 클릭하면 상세 팝업(제목/일시/담당업무/장소/메모)이 열립니다.
- 상세 팝업의 "수정"/"삭제"는 그 일정의 담당업무가 로그인한 사용자의 담당업무와 일치할 때만 활성화됩니다
  (관리자는 항상 가능).

## 학사 일정 권한 규칙

요청하신 규칙을 클라이언트에서 강제합니다(서버 API 자체는 별도 인증 검사가 없었습니다):

- **등록은 누구나 가능**합니다. 다만 담당업무(`deptId`)는 로그인한 사용자가 실제로 맡고 있는
  업무 중에서만 고를 수 있고, "관련 업무 없음"을 선택(=값을 비움)하면 담당업무 없이 등록됩니다.
- **수정/삭제는 그 일정의 담당업무가 자신의 담당업무와 일치할 때만** 가능합니다.
  담당업무가 없는(관련 업무 없음) 일정이나 남의 담당업무로 등록된 일정은 일반 사용자가
  건드릴 수 없습니다. **관리자(role=admin) 계정은 모든 일정을 등록/수정/삭제**할 수 있습니다.
- 담당업무는 `school_teacher_departments`(N:M) 기준으로 여러 개일 수 있어, 로그인 직후
  `teachers.php?action=get`으로 본인의 전체 담당업무 목록을 다시 불러와 판정합니다
  (교사 레코드가 없으면 로그인 프로필의 대표 담당업무 하나만 사용).

## 환경설정 (Visual Studio 옵션 창 스타일)

트레이 메뉴의 "환경설정..."을 열면 왼쪽에 대분류 트리, 오른쪽에 선택한 대분류의
설정 항목이 나타나는 옵션 창이 뜹니다(확인/취소/적용).

| 대분류 | 항목 |
|---|---|
| 일반 | 학교명(로그인 시 서버 값으로 최초 자동 채움), Windows 시작 시 자동 실행, **첫 시작 시 공지사항 알림**(기본 미체크) |
| 학사일정 | 출력 모니터, 출력 단위(주 단위/월 단위), 배경화면 출력 체크박스, **투명도(10~100%)** |
| 복무 | 출력 모니터, 교감 체크박스, 교장 체크박스, **투명도(30~100%)** |
| 출력 | 프린터 선택, 나의 일간 일정 자동 출력 체크박스 |
| 네트워크 | 웹소켓 서버, 업데이트 서버, WorkSupport 서버 주소(API, 선택), 프로그램 버전(읽기 전용) |

## 배경화면형 학사달력

학사일정 설정에서 "배경화면 출력"을 켜면 선택한 모니터에 학사달력을 상시 표시합니다.

- **주 단위**: 선택한 모니터 하단에 얇은 띠로 이번 주 학사달력(주간일정)을 표시합니다.
- **월 단위**: 선택한 모니터 화면 전체에 이번 달 학사달력(월간일정)을 표시합니다.
- 담당업무 색상은 실제 DB 색상을 사용하며, **투명도**를 환경설정에서 조절할 수 있습니다.

이 오버레이 창은 포커스를 가져가지 않고(`WS_EX_NOACTIVATE`) 다른 창들의 Z-order 최하단으로
내려갑니다(`SetWindowPos(HWND_BOTTOM)`). **실제 바탕화면(WorkerW)에 자식으로 삽입하는 방식이
아니라 다른 창들 뒤로 보내 배경처럼 보이게 하는 근사적인 구현**이며, 30분 주기로 최신 일정을
다시 불러옵니다.

## 복무 알림 배너

복무 설정에서 교감/교장 체크박스를 켜면 15분마다 해당 직위의 출장·연가 기록을 확인해서

- **하루 전**: 화면 우측 상단에 "내일은 OOO선생님이 출장/연가 예정입니다" 안내 배너를,
- **당일**: "OOO선생님이 오늘 출장/연가로 부재중입니다" 배너를(강조색)

항상 위(`TopMost`, 포커스는 가져가지 않음)로, 설정한 **투명도**로 표시합니다. 조퇴는
"하루 전 예고"의 성격이 아니라 당일 알림 대상에서 제외했습니다.

## 학사 일정 자동 인쇄

출력 설정에서 프린터를 고르고 "나의 일간 일정 자동 출력"을 켜면, **평일 08:30~10:00 사이
그날 처음으로 조건이 충족되는 시점**에 로그인한 사용자의 담당업무 학사 일정(오늘자)을
선택한 프린터로 자동 인쇄합니다. 하루 한 번만 동작하도록 마지막 인쇄일을 로컬에 기록합니다.

## 첫 시작 시 공지사항 알림

환경설정 &gt; 일반의 "첫 시작 시 공지사항 알림"을 켜면(기본값 미체크), 로그인에 성공한 직후
아래 6가지를 모아 보여주는 요약 팝업(`StartupSummaryForm`)이 뜹니다.

1. 오늘의 일정 — 이번 달 학사 일정 중 오늘 날짜에 걸치는 항목
2. 요청사항(내가 등록) — 요청사항 중 내가 작성했고 아직 열려 있는 것
3. 안내 및 공지 — 요청사항 중 분류가 "공지"인 것
4. 내가 해야 할 일 — 요청사항 중 나에게 온(대상이 전체 공개인) 열린 항목
5. 요청된 법정연수 이수 등록 — 아직 제출하지 않은 연수 과정
6. 처리 중인 협의사항 — 협의사항(최근) 시트에서 완료로 표시되지 않은 안건

각 항목은 해당 API가 실패해도 그 섹션만 비워두고 나머지는 정상적으로 보여줍니다.

## 웹소켓(작업 요청) 기능

`node2.future-class.kr` 웹소켓 서버는 WorkSupport HTTP API와 별개로, 서버가 클라이언트에게
작업을 요청하고(`task.request`) 클라이언트가 수락/거절로 응답(`task.response`)하는 실시간
채널로 계속 사용합니다. 연결이 끊기면 지수 백오프(2초~30초)로 자동 재연결합니다.

## UI 디자인 개편 (하늘색·오렌지 테마)

기본 WinForms 회색조 인터페이스가 사용자 친화적이지 않다는 피드백에 따라, 밝은 하늘색과
오렌지색 계열을 중심으로 전체 화면을 다시 스타일링했습니다. 레이아웃(컨트롤 위치·크기)은
바꾸지 않고 색상·글꼴·버튼/그리드/트리/메뉴의 그리기 방식만 손봐서, 기존 화면 구성과
동작은 그대로 유지하면서 현대적인 느낌을 내도록 했습니다.

### 팔레트 (`Services/UiTheme.cs`)

| 용도 | 색상 |
|---|---|
| 주 색상(하늘색) | `Sky` `#29ABE2`, 강조 시 `SkyDark` `#0F85BA`, 옅은 배경 `SkyLight`/`SkyPale` |
| 포인트 색상(오렌지) | `Orange` `#FF8D3C`, 강조 시 `OrangeDark` `#E86F1A`, 옅은 배경 `OrangeLight` |
| 배경/표면 | 창 배경 `SkyPale`(옅은 하늘색), 카드/입력 영역 `White` |
| 텍스트 | 기본 `TextPrimary`(짙은 남회색), 보조 설명 `TextSecondary`(회색조) |
| 상태 | 성공 `Success`(녹색), 위험/오류 `Danger`(붉은 오렌지) |
| 글꼴 | 본문 "맑은 고딕" 9.5pt, 제목 13pt(굵게), 소제목 10pt(굵게) |

`UiTheme`는 위 팔레트를 바탕으로 아래와 같은 재사용 가능한 스타일 함수를 제공하고,
모든 창과 사용자 컨트롤이 생성자에서 이 함수들을 호출해 스타일을 적용합니다.

- `ApplyForm` — 창 배경색·기본 글꼴 지정
- `StylePrimaryButton`/`StyleSecondaryButton`/`StyleDangerButton`/`StyleFlatToolButton` —
  오렌지색 강조 버튼(등록/저장 등 주요 동작), 하늘색 테두리의 보조 버튼(취소/닫기 등),
  삭제류의 위험 버튼, 테두리만 있는 툴바형 버튼(달력 이전/오늘/다음 등)을 각각 평평한
  플랫 스타일로 그립니다.
- `StyleHeaderLabel`/`StyleSubHeaderLabel`/`StyleHintLabel` — 제목·소제목·보조 설명용 라벨 색상/글꼴
- `StyleGrid`/`StyleGridButtonColumn` — `DataGridView`의 헤더를 하늘색 배경/흰 글자로,
  선택 행은 옅은 오렌지색으로, 홀짝 행은 옅은 하늘색으로 표시
- `StyleTree` — `TreeView`(길라잡이 문서함 트리 등)를 오너 드로우로 다시 그려 선택 항목을
  오렌지색으로 표시(윈도우 기본 파란색 선택 대신)
- `CreateMenuRenderer` — 트레이 아이콘 우클릭 메뉴에 적용하는 `ToolStripProfessionalRenderer`.
  메뉴 항목에 마우스를 올리면 옅은 오렌지색으로 강조됩니다.

### 적용 범위

- **로그인/사용자정보/학사일정 등록·목록·상세** 창 전체
- **환경설정(Options) 창**과 대분류(일반/학사일정/복무/출력/네트워크) 5개 페이지 전체
- **기본정보** 5종 화면(요청사항 목록/등록, 학교기본정보, 공통계정, 협의사항, 길라잡이 조회)
- **트레이 아이콘 우클릭 메뉴** 전체(하늘색·오렌지 강조 렌더러 적용)
- **배경화면형 학사달력 오버레이**와 **복무 알림 배너**는 바탕화면 위에 항상 떠 있는
  화면이라 어두운 배경에 흰 글자를 유지해 가독성을 지키되, "오늘" 강조색과 배너 색상을
  테마의 하늘색(`SkyDark`)·오렌지(`Orange`)·위험색(`Danger`)으로 맞췄습니다.

### 알려진 제약

일부 요소는 윈도우 표준 컨트롤을 그대로 사용해서 이번 테마로는 색을 바꿀 수 없습니다.

- `CheckBox`/`RadioButton`의 체크·라디오 표시(글리프)는 운영체제 기본 모양 그대로입니다.
- `ComboBox`의 드롭다운 목록, `DateTimePicker`의 달력 팝업은 운영체제 기본 렌더링을 따릅니다.
- `MessageBox`, `SaveFileDialog` 등 시스템 공용 대화상자는 윈도우 테마를 따르며 앱에서
  색을 바꿀 수 없습니다.

## 아이콘

프로그램 기본 아이콘과 트레이 아이콘은 첨부해 주신 WorkSupport 코드 안의
`viewer/externalLectureViewer.ico`를 그대로 사용합니다(`Resources/AppIcon.ico`로 복사,
빌드 시 실행 파일 아이콘 및 임베디드 리소스로 포함). 새로 추가한 모든 창에도 동일한 아이콘을 적용했습니다.

## 업데이트 방식 (기존과 동일)

프로그램 시작 시 매번 `UpdateManifestUrl`(환경설정 &gt; 네트워크의 업데이트 서버 주소)에서
버전 매니페스트를 조회해 새 버전이 있으면 내려받고, 실행 중인 exe 자신은 스스로 덮어쓸 수
없으므로 임시 배치 스크립트로 (1) 현재 프로세스 종료 대기 → (2) 새 파일 교체 → (3) 재시작
순서로 적용합니다.

## Windows 자동 시작

`Services/AutoStartManager.cs`가 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
레지스트리 키에 실행 파일 경로를 등록/해제합니다(관리자 권한 불필요). 환경설정 &gt; 일반에서
켜고 끌 수 있습니다.

## 빌드 및 실행 (Windows, .NET 8 SDK 필요)

```
dotnet build LMS_C-.sln
dotnet run --project src/LmsAgent/LmsAgent.csproj
```

> Windows Forms는 Windows 데스크톱 런타임이 필요하므로 macOS/Linux에서는 빌드/실행할 수
> 없습니다. Windows 환경(또는 Windows용 CI)에서 빌드하세요. 이번 세션은 Linux 컨테이너라
> `dotnet` SDK가 없어 실제 빌드 검증은 하지 못했습니다 — 코드 리뷰와 API 스펙 대조로
> 정합성을 확인했으니, 빌드 후 에러가 있다면 알려주세요.

## 알려진 한계 / 근사 구현

- **배경화면 오버레이**는 진짜 바탕화면(Progman/WorkerW)에 붙이는 방식이 아니라, 창을
  다른 창들보다 Z-order 최하단에 두는 근사 구현입니다.
- **자동 로그인**은 구현하지 않았습니다. 저장된 아이디는 로그인 창에 자동으로 채워지지만
  비밀번호는 저장하지 않으므로, 프로그램을 새로 시작할 때마다 비밀번호 입력이 필요합니다.
- **협의사항은 조회 전용**입니다(위 API 표 참고) — 서버에 안건 등록/수정/삭제 API가 없습니다.
- **요청사항 등록은 항상 "전체 공개"** 대상으로만 등록됩니다. 서버는 특정 교사를 지정해
  안내하는 기능도 지원하지만, 대상자 선택 UI는 이번 범위에 포함하지 않았습니다.
- **요청사항/협의사항 첨부파일**은 지원하지 않습니다(서버는 지원하나 업로드 UI 미구현).
- `events.php`의 `addEvent()`는 `createdBy`가 비어 있으면 초기화되지 않은 `$pdo`를 참조하는
  서버측 결함이 있어(별도 수정하지 않음), 클라이언트가 항상 로그인한 사용자의 `user_id`를
  `createdBy`로 채워 보내 이 문제를 피합니다.
- 담당업무가 없는("관련 업무 없음") 일정은 관리자만 수정/삭제할 수 있습니다(작성자 구분이
  없는 공용 일정이라 임의로 아무나 편집하지 못하도록 보수적으로 처리했습니다).
- 학교기본정보/공통계정의 등록·수정은 서버 정책상 관리자 전용이라, 이 프로그램에서는
  조회(및 공통계정 비밀번호 "보기")만 제공합니다.
- 이미 한 번 실행해서 `%AppData%\LmsAgent\settings.json`이 만들어진 PC에서는 새 기본값이
  자동으로 적용되지 않습니다(파일에 예전 기본값이 그대로 저장돼 있기 때문). 환경설정 &gt;
  네트워크에서 "WorkSupport 서버 주소"를 직접 입력하거나, 그 설정 파일을 지우고 다시
  실행하세요.

## 다음 작업 제안

- 실제 서버 환경에서 빌드/로그인/일정 등록 전 과정 통합 테스트
- 배경화면 오버레이를 실제 WorkerW에 삽입하는 방식으로 고도화
- 복무 배너에 담당자 이름(현재는 직위만 표시)까지 노출하려면 서버 API에 이름 필드 추가 필요
- 요청사항 대상자 지정 UI, 요청사항/협의사항 첨부파일 업로드 UI 추가
- 서버에 협의사항 쓰기 API가 추가되면 조회 전용 화면을 등록/수정/삭제까지 확장
