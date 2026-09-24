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
- `System.Net.WebSockets.ClientWebSocket` — node2.future-class.kr/ws-lms 실시간 연동(raw
  WebSocket, Socket.IO 아님 — 웹 브라우저만 Socket.IO(`/ws-work`)로 접속합니다)
- `System.Security.Cryptography.ProtectedData`(NuGet) — 실시간 연동용 기기 토큰을 DPAPI로
  암호화해서 저장(`Configuration/DeviceTokenProtector.cs`)
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
                                  DeviceTokenProtector.cs — 실시간 연동 기기 토큰 DPAPI 암·복호화
  Networking/
    WebSocketClientService.cs    node2.future-class.kr/ws-lms에 raw WebSocket으로 접속(티켓 발급,
                                  구독, domain.event 수신 시 리비전 검사 후 재조회 신호, 지수 백오프 재접속)
    WorkSupportApiClient.cs      WorkSupport PHP API 클라이언트 (쿠키 세션 유지, 파일 다운로드,
                                  실시간 연동용 rt_ticket.php 발급 포함)
    ConnectionState.cs           연결 상태 열거형(Disconnected/Connecting/Connected)
  Models/
    WorkSupport/                 서버 API 요청/응답 모델
                                  (User, Department, Event, Duty, SchoolInfo, SharedAccount,
                                   Request, Meetings, GuideCatalog, Training/요약용 모델)
    Realtime/RealtimeModels.cs   실시간 연동 티켓/도메인 이벤트 모델(DeviceTicket, DomainEventData 등)
  Services/
    SessionManager.cs            로그인 세션, 담당업무(부서) 목록·색상, 권한 판정
    AutoStartManager.cs          Windows 시작 프로그램 등록(레지스트리)
    UpdateService.cs             업데이트 확인/다운로드/적용
    DutyNotificationService.cs   복무(출장/연가) 알림 배너(투명도 적용)
    ScheduleOverlayService.cs    학사달력 배경화면형 오버레이(DB 색상·투명도 적용)
    WorkJournalService.cs        업무 일지 데이터 조회·필터링(담당업무/할일/알림 대상)
    GlobalHotkeyService.cs       Win32 RegisterHotKey 기반 전역 단축키 등록/해제
    RealtimeLog.cs                실시간 연동 연결/오류 로그 파일 기록
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
    DutyRegisterForm.cs / DutyEditForm.cs      학사 일정 &gt; 복무등록(교장/교감/교무부장/행정실장의 연가·출장·조퇴 기록)
    TodoRegisterForm.cs / TodoEditForm.cs      학사 일정 &gt; 할일등록(제목/마감일/담당업무/우선순위, 기록 권한 게이팅)
    OptionsForm.cs / OptionsPages/*.cs         환경설정(Visual Studio 옵션 창 스타일)
                                                OptionsPages/TaskOptionsPage.cs, ShortcutOptionsPage.cs,
                                                HotkeyCaptureBox.cs 포함
    DutyBannerForm.cs / ScheduleOverlayForm.cs 복무 알림 배너 / 학사달력 오버레이 창
    WorkJournalForm.cs                         업무 일지 포스트잇 오버레이(항상 최상단, 요청사항·
                                                알림·법정연수 요약 + 🗒 쪽지(스티커 메모) 포함)
    SchoolInfoForm.cs                          기본정보 &gt; 학교기본정보(조회 전용 모달)
    SharedAccountsForm.cs                      기본정보 &gt; 공통계정(조회 전용 + 비밀번호 보기)
    RequestsForm.cs / RequestEditForm.cs       기본정보 &gt; 요청사항(조회/등록/본인 것 수정·삭제)
    MeetingsForm.cs                            기본정보 &gt; 협의사항(조회 전용)
    GuideDocsForm.cs                           기본정보 &gt; 길라잡이 조회(문서함 트리 + 다운로드)
    StartupSummaryForm.cs                      로그인 직후 공지 요약 팝업
  Interop/NativeMethods.cs       오버레이 창을 배경으로 보내기 위한 최소 P/Invoke
```

## 메뉴 구성

프로그램은 실행 후 별도 창 없이 트레이 아이콘 상태로 최소화되어 상주합니다.
트레이 아이콘을 **우클릭**하면 아래 메뉴가 나타납니다. **왼쪽 클릭**하면 같은 메뉴 항목을
일반 윈도우 프로그램처럼 **화면 위쪽 가운데에 뜨는 flat 스타일 창**(`Forms/TrayControlPanelForm.cs`,
학사달력 배경화면과 같은 규칙으로 최대 1024×768, 모니터가 더 작으면 그 작업 영역에 맞춤)으로
다시 보여줍니다 — 오른쪽 클릭이 익숙하지 않은 상황에서도 왼쪽 클릭만으로 모든 메뉴에
접근할 수 있고, 화면 가운데가 아니라 위쪽에 뜨기 때문에 창이 새로 나타났다는 것을 바로
알아볼 수 있습니다. **이미 이 창이 열려 있는 상태에서 트레이 아이콘을 또 왼쪽 클릭하면
새 창을 띄우지 않고 기존 창을 앞으로 가져옵니다**(중복으로 여러 개 뜨지 않습니다).

- 왼쪽에 **아이콘 사이드바**(학사 일정/사용자 정보/기본정보 등 하위 메뉴가 있는 대분류),
  오른쪽에 선택한 대분류의 실제 동작 버튼들을 보여주는 구성입니다(사이드바를 누르면
  선택 표시가 오렌지색으로 바뀌고, 오른쪽 내용이 그 대분류로 바뀝니다). 하위 메뉴가 없는
  단일 동작(환경설정, 업데이트 확인, 종료 등)은 사이드바에서 바로 눌러 실행합니다.
  아래쪽 상태 표시줄에는 연결 상태·라이센스 상태를 보여줍니다.
- 이 창은 항목을 따로 소유하지 않고 원본 컨텍스트 메뉴 항목의 `PerformClick()`을 그대로
  호출하므로, 메뉴 구성이 바뀌어도 이 창을 따로 손볼 필요가 없습니다. 버튼을 누르면 해당
  동작이 실행된 뒤 이 창은 자동으로 닫힙니다.

- 연결 상태 표시 (읽기 전용, 웹소켓 연결 상태)
- 교무업무 페이지 — C#에서 이미 로그인되어 있으면 SSO 1회용 티켓(`sso_ticket.php`)을
  발급받아 그 주소로 열어서 브라우저가 재로그인 없이 바로 대시보드로 들어갑니다(아래
  "SSO(자동 로그인)" 참고). 로그인 전이거나 티켓 발급이 실패하면(세션 만료 등) 평소처럼
  환경설정 &gt; 네트워크의 주소를 기본 브라우저로 엽니다.
- **학사 일정**
  - 일정 등록...
  - 일정 목록... (구글 캘린더 스타일 월간 보기, 등록/수정/삭제)
  - 복무등록... (교장/교감/교무부장/행정실장의 연가·출장·조퇴 기록, 아래 "복무등록" 참고)
  - 할일등록... (제목/마감일/담당업무/우선순위 기록, 아래 "할일등록" 참고)
- **사용자 정보**
  - 로그인...
  - 정보 수정... (로그인 후 활성화)
  - 개인일정 등록... (로그인 여부와 무관하게 항상 사용 가능 — 아래 "개인일정" 참고)
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
| 할일 목록 | `GET SchoolCalendar/php/api/todos.php?action=list` | school_todos 전체 목록(미완료→마감일 순). `deptId`가 담당업무, `done`이 완료 여부(DB의 status를 변환해 내려줌). 조회는 권한과 무관하게 누구나 가능 |
| 할일 기록 권한 확인 | `GET SchoolCalendar/php/api/todos.php?action=can_manage` | 복무와 같은 권한 체계(관리자·교장/교감/교무부장/행정실장·개별 허용) |
| 할일 등록/수정/삭제 | `POST SchoolCalendar/php/api/todos.php` (`action=add\|update\|delete`, JSON body) | **수정은 전체 교체**라 서버가 최종 판단하는 기록 권한이 있는 계정만 가능. 완료 체크박스는 `{action:"update", id, done}`만 보내는 전용 경로 사용 |
| 복무(연가/출장/조퇴) 목록 | `GET SchoolCalendar/php/api/duty_status.php?action=list` | 교장/교감 등 복무 변동사항(전체 사용자 열람 가능) |
| 복무 기록 권한 확인 | `GET SchoolCalendar/php/api/duty_status.php?action=can_manage` | 현재 계정이 기록 가능한지, 어떤 직위 자격인지 확인 |
| 복무 기록 등록/수정/삭제 | `POST SchoolCalendar/php/api/duty_status.php` (`action=add\|update\|delete`, JSON body) | `role=admin` 또는 `school_teachers.position`이 교장/교감/교무부장/행정실장인 계정만 가능 |

> **쓰기(저장)와 읽기(조회)는 인증이 다릅니다.** 조회·저장 모두 로그인 세션 쿠키(`WSSESSID`)로
> 이루어지고(웹소켓 티켓과는 무관), 저장(POST) 요청에는 실시간 연동 소켓 접속으로 받은
> `clientId`를 `X-WS-Client-Id` 헤더(+ `X-WS-Client-Type: windows`)에 실어 보내
> 서버가 "이 PC가 일으킨 변경"임을 알고 같은 PC에게는 도메인 이벤트를 다시 보내지 않도록
> 합니다(에코 억제, `WorkSupportApiClient.ClientId`). 아직 소켓에 접속하지 못한 상태라도
> 헤더 없이 저장은 정상 동작하며, 목록이 한 번 더 갱신되는 정도의 부작용만 있습니다.

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
  (교사 레코드가 없으면 로그인 프로필의 대표 담당업무 하나만 사용). 사용자 정보 수정
  화면의 "담당업무" 표시도 이 다건 목록을 쉼표로 이어서 보여줍니다(`UserInfoForm.ResolveDeptNames`) —
  `profile.php`가 주는 `dept_name`은 대표 업무 하나뿐이라, 이전에는 화면에 한 개만 보였습니다.

## 환경설정 (Visual Studio 옵션 창 스타일)

트레이 메뉴의 "환경설정..."을 열면 왼쪽에 대분류 트리, 오른쪽에 선택한 대분류의
설정 항목이 나타나는 옵션 창이 뜹니다(확인/취소/적용).

| 대분류 | 항목 |
|---|---|
| 일반 | Windows 시작 시 자동 실행, **첫 시작 시 공지사항 알림**(기본 미체크), **쉬는 시간 전자칠판 페이지 활성화**·출력 모니터·**쉬는 시간 종료안내** |
| 학사일정 | 출력 모니터, 출력 단위(주 단위/월 단위), 배경화면 출력 체크박스, **투명도(10~100%)** |
| 개인일정 | 표시 색상, 아이콘(10종), 저장 폴더 — **로컬 전용, 학사 일정과 연동되지 않음**(아래 "개인일정" 참고) |
| 차시 | 하루 시간표(교시/점심시간) 등록, "차시 추가"(4교시 다음 점심시간 자동 추가), 쉬는 시간 자동 계산 |
| 복무 | 출력 모니터, 교감 체크박스, 교장 체크박스, **투명도(30~100%)** |
| 업무 | "업무 및 할일 모니터 출력" 체크박스, 출력 모니터, 출력 단위(일 단위/주 단위), **투명도(20~100%)** |
| 출력 | 프린터 선택, 나의 일간 일정 자동 출력 체크박스 |
| 네트워크 | API 기준 서버, 업데이트 서버, WorkSupport 서버 주소(API, 선택), **교무업무 페이지**, **전자칠판 페이지**, 프로그램 버전(읽기 전용) |
| 실시간 연동 | 실시간 연동(웹소켓)용 기기 ID·기기 토큰 입력(토큰은 DPAPI로 암호화 저장, 비어 있으면 이 기능만 비활성화) |
| 단축키 | 학사달력보기 / 관리자 복무상황 보기 / 업무 일지 보기 전역 단축키 활성화·설정(각각 Ctrl/Alt/Shift 조합 + 키) |
| 라이센스 | **학교명**(원래 일반 탭에 있었으나 이 탭으로 이동, 로그인 시 서버 값으로 최초 자동 채움), 인증키 입력(학교 정보의 auth_key와 대조, 불일치 시 3분 후 자동 종료), **프로그램 버전**(읽기 전용) |

## 배경화면형 학사달력

학사일정 설정에서 "배경화면 출력"을 켜면 선택한 모니터에 학사달력을 상시 표시합니다.

- **주 단위**: 선택한 모니터 하단에 얇은 띠로 이번 주 학사달력(주간일정)을 표시합니다.
- **월 단위**: 선택한 모니터의 **작업 표시줄을 제외한 작업 영역(WorkingArea)** 안에,
  **최대 1024×768** 크기로 화면 가운데에 표시합니다(모니터가 이보다 작으면 그 크기에 맞춥니다).
  예전에는 화면 전체(`screen.Bounds`)를 덮어써서 작업 표시줄까지 가려지던 문제가 있었습니다.
- 담당업무 색상은 실제 DB 색상을 사용하며, **투명도**를 환경설정에서 조절할 수 있습니다.

이 오버레이 창은 포커스를 가져가지 않고(`WS_EX_NOACTIVATE`) 다른 창들의 Z-order 최하단으로
내려갑니다(`SetWindowPos(HWND_BOTTOM)`). **실제 바탕화면(WorkerW)에 자식으로 삽입하는 방식이
아니라 다른 창들 뒤로 보내 배경처럼 보이게 하는 근사적인 구현**이며, 30분 주기로 최신 일정을
다시 불러옵니다. 또한 이 프로그램에서 일정을 등록/수정/삭제하면
(`SessionManager.ScheduleChanged` 이벤트) 30분을 기다리지 않고 그 즉시 다시 불러옵니다
(`ScheduleOverlayService.RefreshNow()`) — 그동안 오버레이가 갱신되지 않는 것처럼 보였던
문제를 이렇게 해결했습니다. 같은 시점에 웹소켓으로 서버에도 갱신 알림을 보냅니다(아래
"웹소켓(작업 요청) 기능"의 `schedule.updated` 참고).

## 차시(하루 시간표)와 쉬는 시간

환경설정 &gt; 차시에서 하루 시간표를 등록합니다. "차시 추가" 버튼을 누르면 이전 교시 종료
10분 뒤부터 40분짜리 새 교시가 추가되고, **4교시 다음으로 "차시 추가"를 누르면 50분짜리
점심시간이 자동으로 먼저 추가된 뒤** 5교시가 이어서 추가됩니다. 각 항목의 시작/종료 시간은
표에서 직접 "HH:mm" 형식으로 고쳐 쓸 수 있습니다.

**쉬는 시간은 별도로 입력하지 않고, 등록한 교시/점심시간 사이의 빈 시간으로 자동 계산됩니다**
(`Services/PeriodScheduleHelper.cs`). 환경설정 화면 하단에 계산된 쉬는 시간 목록이 바로
표시되며, 이 값을 아래 "쉬는 시간 전자칠판" 기능이 그대로 사용합니다.

## 쉬는 시간 전자칠판

환경설정 &gt; 일반의 "쉬는 시간 전자칠판 페이지 활성화"를 켜면, 위에서 계산된 쉬는 시간마다
환경설정 &gt; 네트워크에 등록해 둔 "전자칠판 페이지" 주소를 선택한 모니터에 테두리 없이
전체화면으로 띄우고(`BreakBoardService`/`BreakBoardForm`, 내장 `WebBrowser` 컨트롤 사용),
쉬는 시간이 끝나면 자동으로 닫습니다(포커스를 가져가지 않아 다른 작업을 방해하지 않습니다).

"쉬는 시간 종료안내"를 함께 켜면, 쉬는 시간이 끝나기 **2분 전**부터 화면 가운데에 남은
시간을 세는 작은 카운트다운 안내(`BreakCountdownForm`)를 보여주고, 0이 되면 자동으로
사라집니다.

## SSO(자동 로그인)

트레이 메뉴의 "교무업무 페이지"를 클릭하면, C#이 이미 로그인(WSSESSID 보유)되어 있는 경우
브라우저가 다시 로그인하지 않고 바로 대시보드로 들어갑니다.

1. `GET php/auth/sso_ticket.php`를 (로그인 세션이 담긴 `CookieContainer`로) 호출해서
   1회용 SSO 토큰과 완성된 `loginUrl`을 발급받습니다(`WorkSupportApiClient.GetSsoTicketAsync()`).
2. 받은 `loginUrl`을 그대로 시스템 브라우저로 엽니다(`Process.Start`) — 토큰은 40초짜리
   1회용이라 저장하지 않고 받은 즉시 사용합니다.
3. 그 주소를 브라우저가 열면 서버가 토큰을 확인하고 브라우저에 새 세션을 만든 뒤
   대시보드로 바로 이동시킵니다.

로그인 전이거나, 티켓 발급이 실패하면(세션 만료 등 HTTP 401) 예외를 조용히 삼키고
평소처럼 환경설정 &gt; 네트워크에 등록된 주소로 로그인 화면을 엽니다 — 자동 로그인이 안
되더라도 수동 로그인은 항상 그대로 가능합니다.

**업무 일지의 요청사항·알림·법정연수 항목을 클릭할 때도 같은 방식을 씁니다**
(`WorkJournalService.OpenLinkWithSsoAsync`). 다만 이 경우에는 대시보드가 아니라 그
항목의 상세 주소로 바로 들어가야 하므로, `loginUrl`에 `next=<상세 주소>` 쿼리 파라미터를
덧붙여서 엽니다. **서버의 `sso_login.php`가 아직 `next`를 처리하지 않는다면(현재
안내 문서 기준으로는 항상 대시보드로만 이동합니다), 로그인은 자동으로 되지만 화면은
대시보드에 머무릅니다** — 클릭한 항목까지 바로 이동하게 하려면 서버 쪽에서
`sso_login.php`가 `next` 파라미터를 검증 후 그 주소로 리다이렉트하도록 확장해야 합니다.

### 세션 감시(로그아웃 동기화)와 그 한계

`SessionWatcherService`가 로그인해 있는 동안 3분마다 `ws_me.php`로 C#의 서버 세션이
여전히 유효한지 확인합니다. 유효하지 않으면(세션 만료 등) `SessionManager.Clear()`를
호출해서 로그인 화면·업무 일지("🔒 아직 로그인 전입니다")까지 자동으로 로그아웃 상태로
되돌립니다.

> ⚠ **"웹페이지에서 로그아웃하면 C#도 자동으로 로그아웃"이 항상 되는 것은 아닙니다.**
> C#은 로그인할 때 자기 자신의 `WSSESSID`를 발급받아 들고 있고, 교무업무 페이지를 SSO로
> 열 때 브라우저는 **또 다른(별도의) `WSSESSID`**를 발급받습니다 — 두 세션은 원래 서로
> 독립적입니다. `ws_logout.php`가 그 계정의 세션을 전부 무효화하는 서버라면 위 감시로
> 3분 이내에 자동 반영되지만, 세션 단위로만 로그아웃하는 서버라면 브라우저 쪽 로그아웃은
> C#에 전혀 영향을 주지 않으며 이 감시로도 잡을 수 없습니다. 실시간으로 정확히 반영하려면
> 서버가 로그아웃 시 그 계정의 웹소켓 개인 Room에 도메인 이벤트(예: `work.session.revoked`)를
> 발행해 주는 서버 쪽 변경이 필요합니다 — 자세한 내용은 `docs/연동방법_및_규칙.md`를
> 참고하세요.

## 라이센스

환경설정 &gt; 라이센스에서 **학교명**과 학교에 발급된 인증키를 입력합니다(학교명은 원래
환경설정 &gt; 일반에 있었으나, 라이센스 확인과 함께 관리하는 것이 자연스러워 이 화면으로
옮겼습니다). 이 화면 하단에서 현재 실행 중인 프로그램의 **파일 버전**도 바로 확인할 수
있습니다(`Assembly.GetExecutingAssembly().GetName().Version`, `.csproj`의
`FileVersion`과 같은 값 — 업데이트 배포 시 이 값을 비교해 새 버전 여부를 판단합니다).

`LicenseGuardService`가
학교 정보(`php/features/school.php`의 `info.auth_key`, `schoolwork_school_info.auth_key`
컬럼)를 조회해서 입력한 인증키와 대조하며, 다음 시점마다 자동으로 다시 검사합니다:

- 로그인에 성공했을 때
- 환경설정 창에서 **확인**을 눌러 저장했을 때(로그인되어 있는 경우)

라이센스 페이지 자체도 창을 열 때와 **"지금 확인"** 버튼을 눌렀을 때 즉시 서버와 대조해서
결과를 그 자리에서 보여줍니다(저장하지 않고도 입력한 값으로 바로 확인 가능) — 예전에는
로그인 직후에만 검사해서, 이미 로그인한 상태로 인증키를 입력하고 저장만 하면 트레이
메뉴도 환경설정 화면도 "확인 전"에 머물러 있는 문제가 있었습니다.

- 서버(학교)에 인증키가 아예 설정되어 있지 않으면 검사하지 않습니다(라이센스 정책을
  아직 적용하지 않은 학교를 위한 기본 동작).
- 서버에 인증키가 있는데 입력한 값과 다르면 경고 메시지를 띄우고, **3분 뒤 프로그램을
  자동으로 종료**합니다. 3분 안에 환경설정에서 올바른 인증키를 입력하고 저장하면
  종료가 취소됩니다.
- 조회 자체가 실패(네트워크 오류 등)하면 강제 종료하지 않고 상태도 그대로 두되,
  원인을 `%AppData%\LmsAgent\realtime.log`(트레이 메뉴 "실시간 연동 로그 열기...")에
  남깁니다 — "확인 전"에 계속 머물러 있다면 이 로그를 먼저 확인하세요.

트레이 메뉴의 연결 상태 바로 아래에 라이센스 인증 상태를 작은 원형 아이콘 + **색이 있는
글자**로 보여주는 항목이 있습니다(조회 전용). 회색은 아직 확인 전/서버에 인증키 미설정,
**초록**(체크 아이콘)은 인증됨, **빨강**(x 아이콘)은 인증 실패를 뜻합니다. 연결 상태 항목과
마찬가지로 `Enabled=false`라 기본 렌더러가 텍스트를 항상 회색으로만 그리는 것을,
`ToolStripRenderer.RenderItemText`에서 직접 색을 지정해 우회했습니다.

## 일정 시작 알림

환경설정 &gt; 학사일정의 "일정 시작 알림"에서 10분/20분/30분/1시간 전 알림을 각각
체크박스로 켤 수 있습니다. 켜져 있으면 시간이 지정된 학사 일정(종일 일정 제외)의
시작 전, 체크한 시간마다 트레이 풍선 알림으로 안내합니다(`ScheduleReminderService`,
30초 주기로 확인하며 같은 일정·같은 시간대는 하루에 한 번만 알립니다).

## 복무등록

트레이 메뉴 "학사 일정 &gt; 복무등록..."에서 교장·교감·교무부장·행정실장의 연가·출장·조퇴
기록을 월 단위로 조회하고 등록/수정/삭제할 수 있습니다.

- **조회는 로그인한 누구나** 가능합니다(복무 알림 배너·배경화면 오버레이가 참고하는 것과
  같은 `duty_status.php?action=list` 데이터를 그대로 보여줍니다).
- **등록/수정/삭제는 서버가 최종 판단합니다.** 화면을 열면 먼저
  `action=can_manage`로 현재 계정의 기록 권한을 확인하고, 권한이 없으면(교장/교감/
  교무부장/행정실장이 아니고 관리자 계정도 아니면) 등록/수정/삭제 버튼을 비활성화한 채
  조회만 제공합니다.
- 등록/수정 창에서는 **대상**(교장/교감/교무부장/행정실장), **구분**(연가/출장/조퇴),
  **날짜**, **종일 체크박스**, **메모(선택)**를 입력합니다. "종일"을 체크하거나 시간을
  입력하지 않으면 시간 없이 종일 기록으로 저장되고, 체크를 해제하면 시작/종료 시간을
  따로 입력할 수 있습니다(종료가 시작보다 빠르면 저장하지 않습니다).

## 할일등록

트레이 메뉴 "학사 일정 &gt; 할일등록..."에서 학교 전체가 함께 보는 할일 목록을 조회하고
등록/수정/삭제, 완료 체크를 할 수 있습니다. 복무등록과 동일한 권한 모델(연동가이드.md §2-4)을
그대로 따릅니다.

- **조회는 로그인한 누구나** 가능합니다(`todos.php?action=list`, 전체 목록을 미완료→마감일
  순으로 받아 그대로 보여줍니다).
- **등록/수정/삭제 및 완료 체크는 서버가 최종 판단합니다.** 화면을 열면 먼저
  `action=can_manage`로 현재 계정의 기록 권한(관리자 · 교장/교감/교무부장/행정실장 ·
  관리자가 사용자 관리에서 "할일·복무 기록"을 개별 허용한 계정)을 확인하고, 권한이 없으면
  등록/수정/삭제 버튼과 완료 체크박스를 모두 비활성화한 채 조회만 제공합니다.
- 등록/수정 창에서는 **제목**(필수), **마감일**(선택, "없음" 체크로 생략), **담당업무**
  (`departments.php` 목록, 선택), **우선순위**(높음/보통/낮음), **메모**(선택)를 입력합니다.
- **수정은 서버가 전체 교체로 처리합니다**(연동가이드.md §5-5) — 제목만 바꿔서 보내면
  마감일·담당업무·메모 등 나머지 필드가 비워지므로, 목록에서 받은 객체를 그대로 들고 있다가
  사용자가 바꾼 항목만 반영한 전체 객체를 저장합니다. Google Tasks 연동 필드(`gcalTaskId`,
  `gcalTaskListId`)도 의미를 해석하지 않고 받은 값 그대로 되돌려 보냅니다.
- 목록의 **완료 체크박스**를 직접 누르면 다른 필드를 건드리지 않도록 `{action:"update",
  id, done}`만 보내는 전용 경로로 즉시 저장됩니다(§5-4). 저장에 실패하면 체크 상태를
  원래대로 되돌립니다.
- 할일·복무는 **Google Calendar와 연동하지 않습니다**(학사 일정만 연동됩니다) — C# 쪽에서
  Google Calendar API를 직접 호출하거나 `channels` 같은 값을 보낼 필요가 없습니다.

## 복무 알림 배너

환경설정 &gt; **복무**의 "**복무 알림 배너 표시**" 체크박스로 이 기능 전체를 켜고 끕니다
(기능이 처음 생겼을 때는 이 마스터 스위치가 없어서, 교감/교장 체크박스만으로 켜고 꺼야
했는데 헷갈리기 쉬워 별도로 추가했습니다). 이 체크박스가 꺼져 있으면 교감/교장 체크
여부와 무관하게 배너가 아예 뜨지 않습니다.

체크박스를 켜고 교감/교장 중 알림받을 대상을 체크하면, 15분마다 해당 직위의 출장·연가 기록을 확인해서

- **하루 전**: 화면 우측 상단에 "내일 부재 예정" 안내 배너를,
- **당일**: "오늘 부재 안내" 배너를(강조색)

모던 스타일의 카드형 토스트로 표시합니다 — 둥근 모서리, 실제 창 그림자(`CS_DROPSHADOW`),
긴급도에 따라 색이 바뀌는 원형 아이콘 배지, 굵은 제목 + 상세 설명 2단 구성, 표시될 때
부드러운 페이드인 애니메이션을 적용했습니다(`Forms/DutyBannerForm.cs`). **우상단의 닫기(×)
표시를 눌렀을 때만** 닫히며, 배너 몸통을 눌러서는 닫히지 않습니다(실수로 닫는 것을 방지).
항상 위(`TopMost`, 포커스는 가져가지 않음)로, 설정한 **투명도**로 표시합니다.
조퇴는 "하루 전 예고"의 성격이 아니라 당일 알림 대상에서 제외했습니다.

## 업무 일지 (포스트잇 오버레이)

환경설정 &gt; 업무에서 "업무 및 할일 모니터 출력"을 체크하면, 선택한 모니터 왼쪽 위에
포스트잇 스타일의 업무 일지 창(`Forms/WorkJournalForm.cs`)이 항상 최상단(topmost)으로
표시됩니다. 다른 프로그램의 topmost 창에 가려지는 것을 막기 위해 3초마다 Z-order를
다시 맨 위로 올립니다("Move To Top").

담당업무 필터링이 로그인 계정 기준이라 **로그인 전에는 조회 자체를 하지 않습니다.** 예전에는
이 경우 창이 그냥 빈 채로 남아 있어 사용자가 "고장났다"고 오해할 수 있었는데, 지금은
"🔒 아직 로그인 전입니다" 안내를 대신 보여줍니다(`WorkJournalForm.SetLoggedOut()`).
로그인에 성공하면(`SessionManager.SessionChanged`) 즉시 실제 내용으로 바뀝니다.

> ⚠ **과거 구현 버그(수정됨)**: 이전에는 `TopMost = false; TopMost = true;`로 껐다 켜는
> 방식이었는데, 이 방식은 내부적으로 `HWND_NOTOPMOST → HWND_TOPMOST` 순서로 두 번
> Z-order를 바꾸면서 현재 활성 창에 `WM_NCACTIVATE`(비활성) 메시지를 전달했습니다. 그
> 결과 3초 주기로 **로그인 창·컨텍스트 메뉴 등 다른 팝업/모달 창이 저절로 닫히거나
> 활성 표시가 사라지고 입력란 커서가 사라지는** 문제가 있었습니다. 지금은
> `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`로 활성 상태를 건드리지 않고 Z-order만
> 다시 확인시키도록 고쳤습니다.

표시 내용은 세 가지를 날짜별로, **같은 날짜 안에서는 시각 순서로** 묶어서 보여줍니다
(종일 일정·할일처럼 특정 시각이 없는 항목은 그 날짜의 맨 위에 모아서 표시):

- **내 담당업무 관련 학사일정** — `school_events.deptId`가 내 담당업무(복수 가능) 중 하나인 일정.
  종일 일정이 아니면 시작 시각을 "HH:mm"으로 표시합니다.
- **내가 해야 할 할일** — `school_todos.deptId`가 내 담당업무 중 하나이고 아직 완료(`done`)되지
  않은 것. 할일은 학사일정과 마찬가지로 **담당업무(부서) 단위**로 배정되며, 개인별 담당자
  목록 같은 것은 없습니다(마감일만 있고 시각은 없음).
- **나에게 알림으로 지정된 일정** — `SchoolEvent.NotifyTargets.TeacherIds`에 내 교사 id가 포함된 일정
- **개인일정** — 사용자 정보 &gt; 개인일정 등록으로 기록한, **이 PC에만 저장되는 로컬 전용**
  일정(아래 "개인일정" 참고). 환경설정 &gt; 개인일정에서 고른 아이콘·색으로 다른 항목과
  구분되어 표시됩니다.

설명(메모)이 있는 항목은 **주황색 화살표(▶)**로 눈에 띄게 표시되며, 클릭하면 화살표가
▼로 바뀌면서 그 자리에서 설명이 펼쳐집니다(설명이 없는 항목은 수수한 회색 점 •). 예전에는
색 구분 없이 📝 아이콘 하나로만 표시해서 "클릭하면 더 나온다"는 게 눈에 잘 안 띈다는
피드백을 반영했습니다.

> ⚠ **과거 구현 버그(수정됨)**: 항목 한 줄을 그리는 `Panel`에 `AutoSize=true`를 준 채로
> `Height`를 코드에서 또 직접 지정하고 있어서, 두 크기 산정 방식이 충돌해 항목 사이
> 행간이 고르지 않게 보였습니다. `AutoSize`를 끄고 `Height`만 직접 관리하도록 정리했고,
> 링크 하나만 들어가는 요청사항/알림/법정연수 행은 아예 `Panel`로 감싸지 않고 레이블을
> 바로 넣어 레이아웃 엔진에 맡기도록 단순화했습니다.

출력 단위가 **일 단위**면 오늘 하루, **주 단위**면 이번 주(월~일) 범위만 보여줍니다.

### 요청사항 · 알림 · 법정연수 요약 (학사 달력 밖 업무)

학사 일정 위쪽에, 서버 팀이 작성한 `docs/학사달력외_연동가이드.md`를 참고해 학사 달력에
속하지 않는 세 가지 업무도 함께 정리해 보여줍니다 — **분류별로 묶어서**, 항목을 클릭하면
해당 웹 페이지가 기본 브라우저로 열립니다.

- **📮 요청사항** — 나에게 온 요청 · 내가 올린 요청 중 **요청 · 처리 중**인 것만
  (`requests.php?action=active`). 완료·종료·삭제된 것과, 지정 대상 글에서 내가 이미
  처리완료 표시한 것은 서버가 걸러 줘서 애초에 오지 않습니다. 긴급 건과 기한이 지난 건은
  `[긴급]`/`[기한초과]` 배지로 표시합니다.
- **🔔 알림** — 아직 읽지 않은 알림 전체(`notifications.php?action=list&only_unread=1`).
- **🎓 법정연수** — 내가 아직 낼 이수증(미제출·보완요청)과, 내가 등록한 과정 중 확인을
  기다리는 제출 건수(`training.php?action=active`). 보완요청 받은 건은 `[보완요청]`
  배지로 표시합니다.

세 소스는 서로 독립적으로 조회해서, 하나가 실패해도 나머지와 학사일정/할일 표시에는
영향을 주지 않습니다. 실시간 갱신을 위해 `request`·`training` 모듈도 구독합니다
(`WebSocketClientService.Modules`) — 두 scope의 이벤트를 받으면 업무 일지만 다시
조회합니다(배경화면 오버레이·복무 배너는 건드리지 않습니다).

> 이 세 API는 학사 달력 API와 인증·응답 형식이 다릅니다(폼 전송 · 메시지 키가 `msg`가
> 아니라 `message`, 조회도 로그인 필수). 지금은 **조회(읽기)만** 구현했습니다 — 처리완료
> 표시·댓글·이수확인·이수증 제출 같은 **쓰기 동작은 화면이 복잡해 기존처럼 웹 페이지를
> 여는 방식**을 그대로 씁니다(항목 클릭 → 브라우저).

### 개인일정 (로컬 전용)

트레이 메뉴 &gt; 사용자 정보 &gt; **개인일정 등록...**(`Forms/PersonalScheduleForm.cs` +
`Forms/PersonalScheduleEditForm.cs`)에서 제목·날짜·시각(선택)·메모를 적어 개인일정을
기록할 수 있습니다. 로그인 여부와 무관하게 항상 열 수 있습니다.

- **서버(DB)·웹 화면과 전혀 통신하지 않는, 이 PC에만 있는 기록입니다**
  (`Services/PersonalScheduleStore.cs`, JSON 파일 하나로 저장). 학사 일정과 절대
  자동으로 합쳐지거나 동기화되지 않습니다 — 등록 창 하단에도 "개인일정은 로컬에만
  기록될 뿐 학사 일정과 연동이 되지 않습니다"라고 항상 안내합니다.
- 등록한 개인일정은 **업무 일지**의 날짜별 목록에 함께 나타나며, 환경설정에서 고른
  아이콘·색으로 학사일정/할일과 구분됩니다.
- 환경설정 &gt; 학사일정 바로 아래 **개인일정** 페이지(`PersonalScheduleOptionsPage`)에서
  표시 색상·아이콘(10종 중 선택)·저장 폴더를 바꿀 수 있습니다. 저장 폴더를 비워두면
  다른 로컬 전용 기능(쪽지 등)과 같은 `%AppData%\LmsAgent\`를 씁니다. 이 옵션 페이지
  하단에도 같은 "로컬에만 기록" 안내 문구가 있습니다.

### 쪽지 (스티커 메모)

업무 일지 창의 헤더, 닫기(×) 버튼 바로 왼쪽에 **🗒 버튼**이 있습니다. 누르면 업무 일지
**위에 겹쳐지는 게 아니라**, 실제 Windows **스티커 메모**처럼 완전히 독립된 별도의 작은
창(`Forms/StickyNoteForm.cs`)이 떠서 자유롭게 쓸 내용을 적을 수 있습니다 — 업무 일지에
표시되던 학사일정·할일·요청사항 등은 그대로 남아 있고 가려지지 않습니다.

- **여러 개를 동시에 띄울 수 있습니다.** 메모 창 헤더의 **+** 버튼을 누르면 새 메모가 하나
  더 생깁니다(🗒 버튼을 처음 누르면 저장된 메모를 복원하고, 하나도 없으면 빈 메모를 새로
  만듭니다). 각 메모는 헤더를 드래그해서 원하는 위치로 옮길 수 있습니다.
- 메모 하단에는 **굵게(B) · 기울임(I) · 밑줄(U) · 취소선(S) · 글머리 기호(≡) · 이미지
  삽입(🖼)** 툴바가 있어 서식 있는 메모를 쓸 수 있습니다(`RichTextBox` 기반).
- 헤더의 **"…"** 메뉴에서 그 메모만 확인 후 완전히 삭제할 수 있습니다. **×**는 삭제가
  아니라 숨기기입니다(내용은 보존됩니다).
- 서버와 주고받지 않는 **순수 로컬 메모**입니다(`Services/StickyNoteService.cs`,
  `%AppData%\LmsAgent\notes\`— 메모별 RTF 파일 + 위치/크기를 담은 `manifest.json`).
  다른 PC나 웹 화면과 동기화되지 않습니다.
- **생명주기는 업무 일지와 함께 갑니다.** 환경설정 &gt; 업무의 "업무 및 할일 모니터 출력"을
  켜면 저장해 둔 메모들이 함께 나타나고, 체크를 해제해 업무 일지를 끄면 메모 창들도 함께
  숨겨집니다(내용은 삭제되지 않고 다시 켤 때 그대로 복원됩니다).

할일 조회는 실제 서버의 `SchoolCalendar/php/api/todos.php`(action=list) 코드를 확인해
그대로 맞췄습니다(`Models/WorkSupport/TodoItem.cs`) — 서버 DB 컬럼은 `status`('pending'/'done')
이지만 이 API가 응답에서 `done`(boolean)으로 변환해 내려주므로, 별도의 API 파일을 새로
만들 필요 없이 이 엔드포인트를 그대로 씁니다.

> ⚠ **과거 버그(수정됨)**: 업무 일지는 로그인 계정의 담당업무(`MyDeptIds`)를 기준으로
> 걸러 보여주는데, 로그인 **전**(프로그램 시작 직후)에 한 번 조회를 시도한 뒤로는 로그인이
> 끝나도 다시 조회하는 호출이 없었습니다. 그래서 로그인 직후에는 실시간 이벤트가 실제로
> 발생하기 전까지 업무 일지가 계속 비어 있는 것처럼 보였습니다. 이제 로그인이 성공하면
> `TrayApplicationContext.OnLoginClicked`에서 `WorkJournalService.RefreshNow()`를 바로
> 호출해 갱신합니다.
>
> 또한 담당업무(`MyDeptIds`) 판정은 `teachers.php`(교사 레코드 필요)보다
> **`ws_me.php`의 `dept_ids`**(모든 계정 유형을 커버하는 값, 웹소켓_데이터통신규칙.md
> §7-A)를 우선 사용하도록 고쳤습니다 — `teacher_id`가 없는 관리자·행정실 계정도
> 정확한 담당업무 판정을 받습니다(`SessionManager.RefreshDepartmentContextAsync`).
>
> **세 번째 원인(진짜 근본 원인)**: 위 두 가지를 고친 뒤에도 데이터는 정확히 계산되는데
> (디버거로 `items.Count`가 맞게 나옴) 화면에는 아무것도 안 나타나는 문제가 남아 있었습니다.
> `WorkJournalService.RefreshAsync()`가 API 호출에 `.ConfigureAwait(false)`를 쓰고 있어서,
> `await` 이후의 코드 — 최종적으로 WinForms 컨트롤을 직접 조작하는
> `_form.SetItems(items)` — 가 **UI 스레드가 아닌 스레드풀 스레드**에서 실행되고 있었습니다.
> WinForms 컨트롤은 자신을 만든 스레드에서만 조작해야 하므로, 이 호출은 예외를 던지지만
> `_ = RefreshAsync();`(discard) 형태로 호출되어 그 예외가 조용히 무시되어 화면 갱신만
> 실패했습니다. 두 `ConfigureAwait(false)`를 `ConfigureAwait(true)`로 바꿔 UI 스레드로
> 정상적으로 돌아오도록 수정했습니다(같은 이유로 `ScheduleOverlayService`는 처음부터
> `ConfigureAwait(true)`를 쓰고 있어서 문제가 없었습니다).

## 단축키 (전역 핫키)

환경설정 &gt; 단축키에서 세 가지 창을 전역 단축키로 나타내거나 숨길 수 있습니다: 학사달력보기,
관리자 복무상황 보기(복무등록 창), 업무 일지 보기. 각 항목은 활성화 체크박스와 입력칸으로
구성되며, 입력칸을 클릭한 뒤 원하는 키를 누르면 그대로 저장됩니다(Esc로 지우기).

- 반드시 Ctrl/Alt/Shift 중 하나 이상 + 다른 키의 조합이어야 합니다(`HotkeyBinding.IsUsable`).
- Win32 `RegisterHotKey`/`WM_HOTKEY` 기반(`Services/GlobalHotkeyService.cs`)이라, 트레이
  상주 상태에서 다른 프로그램에 포커스가 있어도 동작합니다.
- 이미 다른 프로그램이 선점한 조합은 조용히 등록에 실패합니다(예외를 던지지 않음) — 등록이
  안 되면 다른 조합으로 바꿔서 다시 시도하세요.
- 학사달력보기/관리자 복무상황 보기는 트레이 메뉴에서 여는 것과는 별개의 인스턴스를
  비모달로 띄워 두고 보이기/숨기기만 토글합니다(닫기 버튼도 실제로 닫지 않고 숨김 처리).

## Google Calendar 연동 (n8n)

현재 3자 동기화 구도는 이렇습니다:

```
Google Calendar ⇄ (n8n) ⇄ 학사달력(DB) ⇄ (실시간 연동/HTTP API) ⇄ LmsAgent(C#)
```

- **학사달력 → Google Calendar**: 이미 동작 중(첨부해 주신 n8n 워크플로만 참고, 저장소에
  포함하지는 않음). `events.php`의 add/update/delete가 `gcal_event_push.php`를 호출해
  n8n 웹훅(`gcal-event-push`)을 트리거하고, n8n이 `action`(create/update/delete)에 따라
  Google Calendar API를 호출한 뒤, 생성 시엔 `gcal_event_push_callback.php`로 새로
  발급된 `gcalEventId`를 DB에 다시 저장합니다.
- **학사달력 ⇄ LmsAgent(C#)**: 이미 동작 중(이 저장소의 실시간 연동 기능). `events.php`가
  DB에 쓸 때마다 `rt_emit(...)`으로 `domain.event`를 발행하고, LmsAgent가 이를 구독해
  재조회합니다 — **어느 클라이언트가 그 HTTP API를 호출했는지는 상관없습니다.**

이 두 가지가 이미 되어 있다는 것이 핵심입니다. 즉 **"C# 로컬에서 작성한 내용을 Google
Calendar에 반영"은 이미 자동으로 되어야 합니다** — LmsAgent가 일정을 등록/수정할 때도
결국 웹 UI와 똑같은 `events.php`를 호출하기 때문에, `events.php`가 호출자를 가리지 않고
매번 Google 푸시를 트리거한다면 별도 코드 없이 이미 동작할 것입니다. **먼저 이것부터
테스트해 보세요**: LmsAgent에서 일정을 하나 등록해 보고 Google Calendar에 나타나는지
확인하십시오. 안 나타난다면 `events.php`가 호출자(웹/로컬)를 구분해서 특정 조건에서만
푸시를 트리거하고 있다는 뜻이니, 그 조건을 다시 봐야 합니다(서버 쪽 확인 사항).

### 반대 방향: Google Calendar → 학사달력 → LmsAgent(C#)

이 방향의 n8n 워크플로(Google Calendar 트리거 → 학사달력 DB 반영)는 이번에 공유해 주신
파일과는 반대 방향이라 내용을 보지 못했습니다. 다만 원리는 동일합니다 — **이 워크플로가
DB에 새 일정을 쓸 때 어떤 경로를 쓰느냐**에 따라 결과가 갈립니다.

**n8n(및 그 워크플로가 호출하는 PHP)에서 해야 할 일:**

1. 그 워크플로가 이미 `events.php?action=add`(같은 엔드포인트)를 호출해서 DB에 쓰고
   있다면 — **아무것도 더 할 필요가 없습니다.** `events.php`가 쓰기 시점에 자동으로
   `rt_emit(...)`을 호출하므로, LmsAgent도 웹도 동시에 알림을 받습니다.
2. 만약 그 워크플로가 별도의 PHP 스크립트나 n8n의 DB 노드로 `school_events` 테이블에
   **직접** INSERT하고 있다면(=`events.php`를 거치지 않는다면), 그 경로에는 실시간 알림이
   빠져 있을 가능성이 큽니다. 이 경우 다음 중 하나로 고쳐야 합니다:
   - **권장**: 그 스크립트/노드를 없애고 `events.php?action=add`를 HTTP Request 노드로
     호출하도록 바꾸십시오(검증·리비전 관리·실시간 알림을 전부 공짜로 얻습니다).
   - 또는 기존 스크립트를 유지해야 한다면, DB INSERT 직후에 `todos.php`의 `addTodo()`가
     하는 것과 똑같이 아래를 추가하십시오:
     ```php
     require_once __DIR__ . '/../../../php/features/_realtime.php';
     rt_emit(['type'=>'work.calendar.event.created','scope'=>'calendar','op'=>'created',
              'resource'=>['type'=>'school_events','id'=>$newId],'rooms'=>['module:calendar']]);
     ```

**LmsAgent(C#)에서 할 일: 없습니다.** `WebSocketClientService`/`TrayApplicationContext`는
이미 `work.calendar.event.*` 타입의 `domain.event`를 받으면 출처(웹/모바일/n8n/Google 등)를
가리지 않고 학사달력 오버레이·업무 일지를 재조회하도록 되어 있습니다(§실시간 연동). 위
1번 또는 2번 조건만 만족하면, Google Calendar에 새 일정을 넣는 즉시 LmsAgent 화면에도
반영됩니다 — 이 저장소를 다시 빌드할 필요조차 없습니다.

### 이번에 함께 고친 것 — Google 연동 끊김 위험

`events.php`의 update가 `todos.php`의 `updateTodo()`와 같은 방식(요청 필드로 컬럼을
그대로 덮어씀)이라면, LmsAgent에서 **이미 Google Calendar와 연동된 일정을 수정**할 때
`gcalEventId`를 함께 보내지 않으면 그 값이 `null`로 지워져 연동이 끊길 위험이 있었습니다.
`ScheduleRegisterForm`이 수정 시 기존 `gcalEventId`를 그대로 실어 보내도록 고쳤습니다
(`WorkSupportApiClient.ToWriteRequest`). 신규 등록 시에는 당연히 비어 있는 채로 보내고,
n8n의 "CREATE 후처리 → gcalEventId DB 저장" 단계가 새로 채워 넣습니다.

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

## 실시간 연동 (학사일정·복무 ↔ 웹 브라우저)

학사일정/복무 등을 Windows 쪽과 웹 브라우저(WorkSupport 웹페이지) 양쪽에서 실시간으로
서로 반영되도록 하는 기능입니다. 서버(`node2.future-class.kr`, `future-class.kr`)는 이미
구축·운영 중이며, 이 저장소(C# 클라이언트)만 그 규약에 맞추면 됩니다(서버는 수정 대상이
아닙니다). 정확한 프로토콜 규약은 서버 팀이 작성한 `docs/웹소켓_데이터통신규칙.md`를 그대로
따랐습니다 — 아래는 그 요약입니다. 복무·할일의 **저장(쓰기)** 쪽 인증·권한·API 계약은
같은 팀이 작성한 `docs/복무_할일_연동가이드.md`를 그대로 따랐습니다(요약은 "할일등록"·
"복무등록" 절 참고). 학사 달력 밖의 요청사항·알림·법정연수는 같은 팀이 작성한
`docs/학사달력외_연동가이드.md`를 그대로 따랐습니다(요약은 "요청사항 · 알림 · 법정연수
요약" 절 참고). **이 C# 앱이 그 규약들을 실제로 어느 파일에서 어떻게 구현하는지를 한 곳에
정리한 지도는 `docs/연동방법_및_규칙.md`를 참고하세요**(세션 모델 — C#과 SSO 브라우저의
세션이 왜 서로 독립적인지, SSO/세션 감시/API/웹소켓 전체 요약, 그리고 로컬 전용이라 이
"연동" 범위에서 제외되는 기능 목록 포함).

### 핵심 오해 금지 사항 (중요)

- **Socket.IO가 아니라 raw WebSocket입니다.** 웹 브라우저는 Socket.IO(`/ws-work`)로 붙지만,
  Windows 클라이언트는 표준 `System.Net.WebSockets.ClientWebSocket`으로 **`/ws-lms`**에
  붙습니다(SocketIOClient 같은 별도 패키지가 필요 없습니다).
- 같은 서버의 **`/ws`는 이 프로그램과 무관한 다른 프로그램 전용**이라, 거기 붙으면
  관계없는 메시지를 받게 됩니다. 반드시 `/ws-lms`를 써야 합니다.
- 소켓으로는 **"무엇이 바뀌었다"는 사실만** 옵니다(payload에 실제 학사 데이터가 들어있지
  않음). 알림을 받으면 기존 HTTP API를 다시 호출해서 화면을 갱신해야 하며, 이 원칙은
  `WebSocketClientService.cs`에 그대로 구현되어 있습니다.

### 흐름

```
[1] 기기 등록 (서버 관리자가 최초 1회, schoolwork_realtime_devices에 device_id + 토큰 해시 등록)
[2] 접속할 때마다 60초짜리 1회용 티켓 발급
       POST {WorkSupport 기준주소}/php/auth/rt_ticket.php?mode=device
            { "device_id": "...", "token": "<평문 토큰>" }
       →   { token(60초), agentUrl, clientId, revisions, ... }
[3] wss://node2.future-class.kr/ws-lms 에 헤더 x-auth-token: <위 token>으로 접속
[4] 접속 직후 { "type": "subscribe", "modules": ["calendar", "notice"] } 전송
[5] domain.event 수신 → 리비전 검사(§ 아래) 통과 시 → 기존 HTTP API 재조회 → 화면 갱신
```

### 기기 등록이 먼저 필요합니다 (서버 관리자 작업)

이 기능을 쓰려면 **서버 관리자가 먼저** 기기를 등록해야 합니다(이 저장소로는 할 수 없는
작업입니다):

```bash
openssl rand -hex 24                                          # 기기 토큰 평문 생성
php -r "echo password_hash('토큰평문', PASSWORD_BCRYPT);"       # bcrypt 해시
```

```sql
INSERT INTO schoolwork_realtime_devices
  (device_id, device_name, user_id, token_hash, client_type, is_active, created_at)
VALUES ('pc-teacher-401', '4학년 1반 교무용 PC', 12, '<위 해시>', 'windows', 1, NOW());
```

발급받은 **`device_id`(평문 그대로)**와 **토큰 평문**을 환경설정 &gt; **실시간 연동**
페이지에 입력하면 됩니다(`Forms/OptionsPages/RealtimeOptionsPage.cs`). 토큰은 저장 시
DPAPI(현재 Windows 사용자 계정 범위)로 암호화되어 `settings.json`에 저장되며, 저장 후에는
평문이 화면에 다시 표시되지 않습니다(`Configuration/DeviceTokenProtector.cs`). 기기 ID/토큰이
비어 있으면 이 기능은 그냥 비활성화되고, 그 외 모든 기능은 기존처럼 동작합니다.

### 리비전 처리 (데이터가 어긋나지 않게 하는 핵심 규칙)

`domain.event`를 받을 때마다 `WebSocketClientService.ShouldHandle`이 순서대로 검사합니다:

1. `eventId`가 최근 200건 안에 있으면 버림(중복 수신)
2. `origin.clientId`가 내 `clientId`와 같으면 버림(내가 만든 변경의 메아리) — 단, 리비전은 갱신
3. `revision`이 내가 가진 값 이하이면 버림(순서가 뒤바뀐 오래된 이벤트)
4. 리비전을 **먼저** 갱신
5. 호출부(`TrayApplicationContext.OnRealtimeScopeChanged`)가 scope/type에 맞는 화면만 재조회

재접속에 성공하면 새 티켓의 `revisions`를 로컬 값과 비교해서, 서버 값이 더 크면(끊긴 동안
놓친 변경이 있으면) 그 scope도 갱신 신호를 보냅니다(재동기화).

### 반영되는 범위

| 받은 type | Windows 쪽에서 하는 일 |
|---|---|
| `work.calendar.event.*` (학사 일정) | `ScheduleOverlayService.RefreshNow()` — 배경화면 학사달력 오버레이 즉시 갱신 |
| `work.calendar.duty.*` (복무) | `DutyNotificationService.RefreshNow()` — 복무 알림 배너 즉시 재확인 |
| `work.calendar.todo.*` (할일) | 오버레이(학사달력 배경) 및 할일등록 창을 다음에 열 때 최신 목록으로 갱신 |
| 그 외 / 재동기화 | 학사달력 오버레이 갱신 |

**모든 `calendar` scope 이벤트에서 업무 일지(`WorkJournalService`)도 함께 즉시 재조회됩니다.**
학사 일정이든 할일이든 복무든, 지금 보고 있는 날짜/주간 범위 안에 있고 내 담당업무와
관련이 있으면 다음 갱신 때 자동으로 반영됩니다(아래 "담당업무 변경 감지" 참고).

반대 방향(Windows에서 등록 → 웹 브라우저에 반영)은 **별도 코드가 필요 없습니다.** 일정/복무
등록·수정·삭제가 기존 WorkSupport HTTP API(`SchoolCalendar/php/api/*`)를 그대로 통해 이루어
지고, 서버가 그 DB 커밋 시점에 자동으로 `domain.event`를 발행해 웹 브라우저(Socket.IO) 쪽에도
전달하기 때문입니다(부록 B: DB 커밋 → outbox 적재 → 발행 순서).

### 담당업무 변경 감지 (§7-A) — "이 일정이 내 업무가 되었는가"

학사달력 웹서비스에서 어떤 일정의 **담당업무(부서)** 를 바꾸면, 그 일정이 로그인한 계정의
업무가 **되거나 빠질 수** 있습니다. 이 판단과 알림은 `Services/MyDutyChangeService.cs`가
담당하며, `docs/웹소켓_데이터통신규칙.md` §7-A를 그대로 구현했습니다.

- `work.calendar.event.*` 이벤트에는 항상 `hint`(`deptFrom`/`deptTo` — 변경 전/후 담당업무 id)가
  붙어 옵니다. 이 값과 로그인 계정의 담당업무 목록(`SessionManager.MyDeptIds`)을 대조해서
  아래처럼 판정합니다.

  | 상황 | 판정 |
  |---|---|
  | 새 일정이 내 업무로 등록됨 | `created` — 새 일정 |
  | 다른 업무 → 내 업무로 바뀜(또는 미지정 → 내 업무) | `assigned` — 배정됨 |
  | 내 업무 → 다른 업무로 바뀜(또는 미지정으로 바뀜) | `unassigned` — 해제됨 |
  | 내 업무 안에서 다른 내용만 수정 | `changed` — 조용히 갱신(알림 없음) |
  | 내 업무였던 일정이 삭제됨 | `deleted` — 삭제됨 |

- 판정되면 트레이 풍선 알림(`NotifyIcon.ShowBalloonTip`)으로 "내 업무로 배정되었습니다: OOO"
  같은 문구를 띄웁니다. **단, 내가 직접 바꾼 경우(`actor.userId`가 내 계정과 같음)와 단순
  수정(`changed`)은 알리지 않습니다** — 에코 억제(§4)와 별개로, 같은 계정이 다른 창(웹)에서
  바꾼 경우까지 걸러내기 위한 것입니다.
- 제목을 알림 문구에 넣기 위해 `events.php?action=get&id=`로 다시 조회합니다. 다만 **삭제되었거나
  내 업무에서 빠진 일정은 다시 조회해도 확인할 수 없으므로**, 배정된 시점에 캐시해 둔 제목을
  씁니다(캐시에 없으면 "(제목 확인 불가)"). 이 캐시는 프로그램을 새로 시작하면 비어 있습니다.
- 화면 갱신 자체(배경화면 오버레이·업무 일지 재조회)는 이 서비스가 아니라 위 "반영되는 범위"의
  일반 처리(`OnRealtimeScopeChanged`)가 **모든 학사 일정 변경에 대해 이미** 수행합니다. 즉 담당
  업무가 나로 바뀐 일정은 알림 여부와 무관하게, 오늘/이번 주 범위 안에 있으면 다음 업무 일지
  갱신 때 자동으로 목록에 나타납니다 — `MyDutyChangeService`는 "왜 나타났는지"를 알려주는
  역할만 합니다.

### 재접속 · 오류 처리

- 연결이 끊기면 1초 → 2초 → 4초… 최대 30초까지 지수 백오프로 재접속하며, 접속마다 티켓을
  새로 받습니다(저장하지 않음).
- 인증 실패(401)가 5회 연속되면 설정 문제로 보고 재시도를 멈추고 로그를 남깁니다 — 기기
  토큰이 틀렸거나, `is_active = 0`이거나, PC 시간이 서버와 크게 차이 나는 경우입니다.
- 소켓이 아예 연결되지 않아도 나머지 기능(로그인, 일정/복무 등록, 조회 등)은 평소대로
  동작합니다 — 실시간 연동은 어디까지나 편의 계층입니다.

## UI 디자인 개편 (하늘색·오렌지 테마, Modern Flat UI)

기본 WinForms 회색조 인터페이스가 사용자 친화적이지 않다는 피드백에 따라, 밝은 하늘색과
오렌지색 계열을 중심으로 전체 화면을 다시 스타일링했습니다. 이후 모든 창을 Panel
기반의 Dock 레이아웃(상단/하단 툴바 + 본문 Dock=Fill)으로 재구성해 창 크기를 키우면
그리드·캘린더·메모 등이 실제로 여백까지 채우도록 했고, 마지막으로 전체를 Modern Flat
UI 스타일로 다듬었습니다(색 테마는 그대로 유지).

### Modern Flat UI 적용 내용

`UiTheme.ApplyForm`이 각 창이 뜰 때(Form.Load) 자식 컨트롤을 전부 훑어서 아래 항목을
자동으로 평평한 스타일로 바꿉니다(개별 폼 코드를 건드릴 필요 없이 전체에 일괄 적용됩니다).

- `ComboBox` → `FlatStyle.Flat`(입체 드롭다운 버튼 제거)
- `TextBox`/`NumericUpDown` → 기본 오목한 `Fixed3D` 테두리 대신 1px `FixedSingle` 테두리
  (이미 `BorderStyle.None`으로 라벨처럼 쓰는 읽기 전용 상태 표시줄은 그대로 둡니다)
- `CheckBox`/`RadioButton` → `FlatStyle.Flat` + 테마 색 강조(체크 시 오렌지)

버튼/그리드/트리/메뉴는 이전부터 개별적으로 평평하게 스타일링되어 있어 그대로 유지됩니다
(`StylePrimaryButton` 등, 아래 표 참고). `DateTimePicker`, `GroupBox` 등 일부 표준 컨트롤은
WinForms 자체에서 테두리 색을 바꿀 수 있는 API를 제공하지 않아 운영체제 기본 렌더링을
그대로 사용합니다.

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

## Windows 자동 시작

`Services/AutoStartManager.cs`가 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
레지스트리 키에 실행 파일 경로를 등록/해제합니다(관리자 권한 불필요). 환경설정 &gt; 일반에서
켜고 끌 수 있습니다.

## 빌드 · 설치 · 업데이트

### 개발용 빌드/실행 (Windows, .NET 8 SDK 필요)

```
dotnet build LMS_C-.sln
dotnet run --project src/LmsAgent/LmsAgent.csproj
```

> Windows Forms는 Windows 데스크톱 런타임이 필요하므로 macOS/Linux에서는 빌드/실행할 수
> 없습니다. Windows 환경(또는 Windows용 CI)에서 빌드하세요. 이 저장소를 다루는 세션은
> Linux 컨테이너라 `dotnet` SDK가 없어 실제 빌드 검증은 하지 못했습니다 — 코드 리뷰와
> API 스펙 대조로 정합성을 확인했으니, 빌드 후 에러가 있다면 알려주세요.

### 배포용 설치 파일 만들기 (dotnet CLI)

exe 하나로 배포하려면(별도 .NET 런타임 설치 없이 실행되도록) 자체 포함(self-contained)
단일 파일로 게시합니다.

```
dotnet publish src/LmsAgent/LmsAgent.csproj -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o dist/LmsAgent
```

- 결과물은 `dist/LmsAgent/LmsAgent.exe` 하나입니다(및 필요 시 `.pdb`). 이 폴더를 그대로
  각 PC의 설치 위치(예: `C:\Program Files\LmsAgent\`)에 복사하면 설치가 끝납니다.
- 별도의 설치 마법사(예: Inno Setup, WiX)를 만들고 싶다면 이 `dist/LmsAgent` 폴더를
  설치 스크립트의 소스 폴더로 지정하면 됩니다. 이 저장소에는 아직 설치 마법사 스크립트를
  포함하지 않았습니다.
- 최초 설치 후 첫 실행 시 사용자가 환경설정 &gt; 네트워크에서 실시간 연동 서버/업데이트 서버/
  WorkSupport 서버 주소를 학교 환경에 맞게 확인해야 합니다(코드의 기본값은
  `future-class.kr` 기준입니다).

### Visual Studio에서 배포용 파일 만들기 (게시/Publish)

명령줄 대신 Visual Studio GUI만으로도 같은 결과물을 만들 수 있습니다.

1. **솔루션 탐색기**에서 `LmsAgent` 프로젝트를 마우스 오른쪽 버튼으로 클릭 → **게시(Publish)**.
2. 처음 게시하는 경우 게시 대상 선택 화면이 뜹니다 → **폴더(Folder)** 선택 → 위치는
   프로젝트 기준 상대경로로 `dist\LmsAgent`처럼 지정합니다 → **완료(Finish)**.
3. 생성된 게시 프로필 화면에서 **모든 설정 표시(Show all settings)**(또는 연필 아이콘의
   편집)를 눌러 아래 값들을 확인/설정합니다.
   | 항목 | 값 |
   |---|---|
   | 구성(Configuration) | Release |
   | 대상 프레임워크(Target framework) | net8.0-windows |
   | 배포 모드(Deployment mode) | 자체 포함(Self-contained) |
   | 대상 런타임(Target runtime) | win-x64 |
   | 단일 파일 생성(Produce single file) | 체크 |
   | 트리밍(Trim unused code) | 체크 해제(WinForms는 리플렉션을 쓰는 부분이 있어 트리밍하면 런타임 오류가 날 수 있습니다) |
4. **저장(Save)** 후 **게시(Publish)** 버튼을 누르면 `dist\LmsAgent\`에 `LmsAgent.exe`
   (및 `.pdb`)가 생성됩니다.
5. 이 설정은 `src\LmsAgent\Properties\PublishProfiles\FolderProfile.pubxml`에 저장되므로,
   다음 버전부터는 프로젝트 우클릭 → 게시 → **게시** 버튼만 다시 누르면 됩니다(설정을 매번
   다시 할 필요 없음). 이 `.pubxml`은 저장소에 커밋해 두면 팀원 누구나 같은 설정으로
   게시할 수 있습니다.

### 버전 변경하기

`manifest.json`의 `version`과 비교되는 값은 exe에 박히는 **파일 버전(FileVersion)**이므로,
새 패치를 배포할 때마다 이 값을 반드시 올려야 클라이언트가 업데이트를 감지합니다. 버전은
`src\LmsAgent\LmsAgent.csproj`의 `<AssemblyVersion>`/`<FileVersion>`/`<Version>` 세 값으로
관리하며(보통 셋 다 같은 값으로 맞춥니다), Visual Studio에서 바꾸는 방법은 두 가지입니다.

- **GUI로 수정**: 솔루션 탐색기에서 `LmsAgent` 프로젝트 우클릭 → **속성(Properties)** →
  왼쪽 **패키지(Package)** 탭 → **일반(General)** → "어셈블리 버전", "파일 버전", "패키지
  버전" 세 항목을 새 버전으로 수정 → 저장(Ctrl+S).
- **파일을 직접 수정**: 솔루션 탐색기에서 `LmsAgent` 프로젝트 우클릭 → **프로젝트 파일
  편집(Edit Project File)** → `.csproj`가 XML 편집기로 열리면 `<AssemblyVersion>1.0.0.0</AssemblyVersion>`
  등 세 줄을 직접 고치고 저장.

### 패치 파일 준비 절차 (Visual Studio 기준, 버전 변경 → 빌드 → 서버 업로드 직전까지)

새 버전을 만들 때마다 아래 순서로 진행하면 됩니다. 5번까지 마치면 서버에 zip을 올릴
준비가 끝난 것이고, 그 다음은 아래 "업데이트 배포 방법"의 3~5단계로 이어집니다.

1. 위 "버전 변경하기"대로 `LmsAgent.csproj`의 버전 세 값을 새 버전으로 올리고 저장합니다.
2. 상단 메뉴 **빌드(Build) → 솔루션 다시 빌드(Rebuild Solution)**로 한 번 깨끗하게
   다시 빌드해서, 구성이 **Release**로 되어 있고(상단 툴바의 구성 드롭다운) 컴파일 오류가
   없는지 확인합니다.
3. 위 "Visual Studio에서 배포용 파일 만들기" 대로 **게시(Publish)**를 실행해 `dist\LmsAgent\`
   폴더를 새로 만듭니다. 같은 폴더에 이전 버전 결과물이 남아있으면 옛 파일이 섞여 들어갈
   수 있으니, 게시 전에 `dist\LmsAgent` 폴더를 탐색기에서 미리 비워 두는 것을 권장합니다.
4. `dist\LmsAgent` 폴더 **안으로** 들어가서 그 안의 파일 전체를 선택한 뒤 압축합니다
   (`dist` 폴더 자체를 압축하면 압축 파일 안에 폴더가 한 겹 더 들어가 버려서, 업데이트
   적용 시 `xcopy`가 실행 파일을 제자리에 덮어쓰지 못합니다 — 반드시 `LmsAgent.exe`가
   zip의 최상위에 오도록 압축하세요). 파일명은 버전을 포함해서 예:
   `LmsAgent-1.2.0.0.zip`처럼 짓습니다.
5. 이 zip 파일의 SHA-256 해시를 구해 둡니다(다음 단계에서 `manifest.json`의 `sha256`에
   넣습니다). Windows 명령 프롬프트에서:
   ```
   certutil -hashfile LmsAgent-1.2.0.0.zip SHA256
   ```
   PowerShell이면:
   ```
   Get-FileHash LmsAgent-1.2.0.0.zip -Algorithm SHA256
   ```

여기까지가 서버 업로드 **직전까지** 로컬(Visual Studio)에서 해야 할 과정입니다.

### 업데이트 배포 방법

프로그램은 실행될 때마다(및 트레이 메뉴 "업데이트 확인..."을 눌렀을 때) 환경설정 &gt;
네트워크의 "업데이트 서버" 주소에서 버전 매니페스트 JSON을 조회합니다
(`Services/UpdateService.cs`). 매니페스트 형식은 다음과 같습니다.

```json
{
  "version": "1.2.0.0",
  "downloadUrl": "https://update.example.com/LmsAgent-1.2.0.0.zip",
  "sha256": "위 zip 파일의 SHA-256 해시(대소문자 무관, 선택)",
  "notes": "변경 사항 설명 — 업데이트 안내 창에 그대로 표시됩니다",
  "mandatory": false
}
```

새 버전을 배포하는 절차:

1. 위 "버전 변경하기" · "패치 파일 준비 절차"대로 버전을 올리고, 빌드/게시해서
   `LmsAgent-1.2.0.0.zip`과 그 SHA-256 해시를 준비합니다.
2. 이 zip 파일을 정적 파일로 서빙할 수 있는 곳(웹 서버, 오브젝트 스토리지 등)에 올립니다.
3. `UpdateManifestUrl`이 가리키는 `manifest.json`을 위 형식대로 갱신합니다
   (`version`을 새 버전으로, `downloadUrl`을 1번의 zip 주소로, `sha256`을 1번의 해시로,
   `notes`에 사용자에게 보여줄 변경 사항 설명을 적습니다).
4. 그 다음부터 프로그램을 실행하는 모든 PC가 실행 시점에(또는 "업데이트 확인..."을 눌렀을
   때) 새 버전을 감지해서 아래 순서로 사용자에게 보여준 뒤 적용합니다 — **예전에는 백그라운드에서
   조용히 받아 적용해서, 사용자 입장에서는 지금 업데이트 중인지 멈춰 있는지 구분할 수
   없었는데, 이제는 매 단계가 화면에 그대로 보입니다.**

   1. **확인**: manifest.json을 조회해서 더 높은 버전이 있는지만 판단합니다(아직 다운로드
      하지 않습니다).
   2. **안내 팝업**(`Forms/UpdateAvailableForm.cs`): 새 버전이 있으면 "현재 버전 → 새 버전"과
      `notes`에 적은 설명을 보여주고, **"지금 업데이트" / "나중에"**를 사용자가 직접 선택하게
      합니다. `mandatory`를 `true`로 두면 "나중에" 버튼이 사라지고 업데이트를 진행해야만
      창이 닫힙니다.
   3. **"지금 업데이트"를 선택한 경우만** 다운로드를 시작합니다: 진행률 창
      (`Forms/UpdateProgressForm.cs`)이 뜨고, 전체 크기를 알 수 있으면 실제 퍼센트 진행률
      막대로, 모르면 진행률 미상(Marquee)으로 보여줍니다. 이어서 sha256 일치 확인 →
      임시 폴더에 압축 해제 → 배치 스크립트(`apply_update.bat`)를 띄우고 현재 프로세스 종료
      → 배치 스크립트가 2초 대기 후 압축 해제한 파일들을 설치 폴더에 `xcopy /E /Y /I`로
      덮어쓰고 → 새 exe를 다시 실행 → 배치 스크립트 자기 자신을 삭제. 이 과정에서 프로그램
      전체가 종료되므로, 진행률 창도 그 순간 함께 사라집니다.
   4. 다운로드나 체크섬 검증이 실패하면 진행률 창을 닫고 실패 사유를 안내하며, 이전 버전
      그대로 계속 실행됩니다(자세한 원인은 아래 로그 참고).

   이 흐름은 `Services/UpdateFlow.cs`가 전담합니다. 프로그램이 아직 트레이로 뜨기도 전
   (`Program.cs`의 최초 실행 시점, 메시지 루프가 시작되기 전)과 이미 실행 중인 트레이
   메뉴 양쪽에서 똑같이 동작해야 해서, `async/await` 대신 `Application.DoEvents()`로 직접
   메시지 루프를 펌프하며 진행률 창을 갱신합니다.
- `sha256`을 비워두면 해시 검증 없이 그대로 적용합니다(내부망 등 신뢰된 배포 경로에서만
  권장).
- **manifest 조회 자체가 실패하는 경우(주소 오설정, 네트워크 오류, 다운로드 타임아웃 등)의
  원인은 전부 `%AppData%\LmsAgent\realtime.log`(트레이 메뉴 "실시간 연동 로그 열기...")에
  `[업데이트]` 접두사로 남습니다.** HTTP 상태 코드, 예외 메시지, 체크섬 불일치 값까지
  그대로 기록되므로, "업데이트가 안 된다"는 문의를 받으면 이 로그부터 확인하세요.
- zip 다운로드는 자체 포함(self-contained) 게시라 100MB를 넘기기 쉬워, manifest.json
  조회(15초 타임아웃)와는 별도로 **10분 타임아웃의 전용 HttpClient**를 씁니다. 그래도
  느린 회선에서는 오래 걸릴 수 있습니다.

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
