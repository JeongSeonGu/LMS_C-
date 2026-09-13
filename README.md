# LMS_C-

LMS연동 윈도우 프로그램

Windows 로그인 시 자동 실행되어 트레이(작업 표시줄 알림 영역)에 상주하며,
학교의 **WorkSupport(교무업무 지원) 웹 서비스**와 통신해 학사 일정을 등록·조회하고,
복무(출장/연가) 변동사항을 안내하며, 서버로부터 작업을 요청받아 처리하는
상시 실행형 클라이언트입니다.

이번 갱신은 실제 WorkSupport 서비스 코드(PHP + MySQL, `SchoolWork/WorkSupport`)를
분석해서 얻은 진짜 API 스펙을 기준으로 로그인·학사일정·복무 기능을 구현하고,
Visual Studio 옵션 창 스타일의 환경설정과 아이콘, 배경화면형 학사달력,
복무 알림 배너, 일정 자동 인쇄 기능을 추가한 버전입니다.

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
    WorkSupportApiClient.cs      WorkSupport PHP API 클라이언트 (쿠키 세션 유지)
    WsEnvelope.cs / MessageTypes.cs  웹소켓 메시지 봉투/타입
  Models/
    WorkSupport/                 서버 API 요청/응답 모델 (User, Department, Event, Duty, ...)
    TaskExchange.cs              웹소켓 작업 요청/응답 페이로드
  Services/
    SessionManager.cs            로그인 세션, 담당업무(부서) 목록, 권한 판정
    AutoStartManager.cs          Windows 시작 프로그램 등록(레지스트리)
    UpdateService.cs             업데이트 확인/다운로드/적용
    DutyNotificationService.cs   복무(출장/연가) 알림 배너
    ScheduleOverlayService.cs    학사달력 배경화면형 오버레이
    AutoPrintService.cs          평일 08:30~10:00 일정 자동 인쇄
    PrintingService.cs           프린터 출력 렌더링
    DisplayHelper.cs             다중 모니터 열거
    AppIconProvider.cs           임베디드 아이콘 로드
  Forms/
    LoginForm.cs / UserInfoForm.cs             로그인 / 개인정보 수정
    ScheduleRegisterForm.cs / ScheduleListForm.cs  학사 일정 등록·수정 / 목록·삭제
    OptionsForm.cs / OptionsPages/*.cs         환경설정(Visual Studio 옵션 창 스타일)
    DutyBannerForm.cs / ScheduleOverlayForm.cs 복무 알림 배너 / 학사달력 오버레이 창
    TaskRequestForm.cs           웹소켓으로 들어온 작업 요청 수락/거절 창
  Interop/NativeMethods.cs       오버레이 창을 배경으로 보내기 위한 최소 P/Invoke
```

## 메뉴 구성

프로그램은 실행 후 별도 창 없이 트레이 아이콘 상태로 최소화되어 상주합니다.
트레이 아이콘을 우클릭하면 아래 메뉴가 나타납니다.

- 연결 상태 표시 (읽기 전용, 웹소켓 연결 상태)
- **학사 일정**
  - 일정 등록...
  - 일정 목록... (월별 조회, 수정/삭제)
- **사용자 정보**
  - 로그인...
  - 정보 수정... (로그인 후 활성화)
- 환경설정... (Visual Studio 옵션 창 스타일 — 아래 참고)
- 업데이트 확인...
- 종료

## WorkSupport API 분석 및 연동

첨부된 `WorkSupport.zip`(교무업무 지원 웹 서비스, PHP + MySQL)을 분석해
아래 실제 엔드포인트를 그대로 사용합니다. 인증은 JWT가 아니라 **세션 쿠키(`WSSESSID`)**
방식이라, `WorkSupportApiClient`가 `CookieContainer`로 로그인 쿠키를 계속 유지하며
이후 모든 요청에 함께 실어 보냅니다. 기준 주소는 환경설정의 "웹소켓 서버" 호스트에서
스킴만 `http(s)`로 바꾸고 `/SchoolWork/WorkSupport`를 붙여서 사용합니다
(같은 서버가 웹소켓과 웹 서비스를 함께 제공하는 구성을 전제로 합니다).

| 기능 | 메서드/경로 | 설명 |
|---|---|---|
| 로그인 | `POST php/auth/ws_login.php` (`login_id`,`login_pw`) | 성공 시 `WSSESSID` 쿠키 발급, 통합 프로필 반환 |
| 로그아웃 | `GET/POST php/auth/ws_logout.php` | 세션 파기 |
| 개인정보 조회 | `GET php/auth/profile.php?action=get` | 이름/직위/담당업무/아이디/연락처 |
| 개인정보 수정 | `POST php/auth/profile.php` (`action=update`, `contact`,`login_id`,`current_pw`,`new_pw`) | 현재 비밀번호 확인 필요, 아이디·연락처·비밀번호 변경 |
| 담당업무 목록 | `GET SchoolCalendar/php/api/departments.php?action=list` | 학사 일정에 연결할 "업무" 목록 |
| 교사 상세(담당업무 다건) | `GET SchoolCalendar/php/api/teachers.php?action=get&id=` | school_teacher_departments(N:M) 기준 본인 담당업무 전체 조회 |
| 학사 일정 목록/등록/수정/삭제 | `SchoolCalendar/php/api/events.php` (`action=list\|add\|update\|delete`) | 학사 일정 CRUD, `deptId`가 담당업무 |
| 복무(연가/출장/조퇴) 목록 | `GET SchoolCalendar/php/api/duty_status.php?action=list` | 교장/교감 등 복무 변동사항 |

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
| 일반 | 학교명(로그인 시 서버 값으로 최초 자동 채움), Windows 시작 시 자동 실행 |
| 학사일정 | 출력 모니터, 출력 단위(주 단위/월 단위), 배경화면 출력 체크박스 |
| 복무 | 출력 모니터, 교감 체크박스, 교장 체크박스 |
| 출력 | 프린터 선택, 나의 일간 일정 자동 출력 체크박스 |
| 네트워크 | 웹소켓 서버, 업데이트 서버, 프로그램 버전(읽기 전용) |

## 배경화면형 학사달력

학사일정 설정에서 "배경화면 출력"을 켜면 선택한 모니터에 학사달력을 상시 표시합니다.

- **주 단위**: 선택한 모니터 하단에 얇은 띠로 이번 주 학사달력(주간일정)을 표시합니다.
- **월 단위**: 선택한 모니터 화면 전체에 이번 달 학사달력(월간일정)을 표시합니다.

이 오버레이 창은 포커스를 가져가지 않고(`WS_EX_NOACTIVATE`) 다른 창들의 Z-order 최하단으로
내려갑니다(`SetWindowPos(HWND_BOTTOM)`). **실제 바탕화면(WorkerW)에 자식으로 삽입하는 방식이
아니라 다른 창들 뒤로 보내 배경처럼 보이게 하는 근사적인 구현**이며, 30분 주기로 최신 일정을
다시 불러옵니다.

## 복무 알림 배너

복무 설정에서 교감/교장 체크박스를 켜면 15분마다 해당 직위의 출장·연가 기록을 확인해서

- **하루 전**: 화면 우측 상단에 "내일은 OOO선생님이 출장/연가 예정입니다" 안내 배너를,
- **당일**: "OOO선생님이 오늘 출장/연가로 부재중입니다" 배너를(강조색)

항상 위(`TopMost`, 포커스는 가져가지 않음)로 표시합니다. 조퇴는 "하루 전 예고"의 성격이 아니라
당일 알림 대상에서 제외했습니다.

## 학사 일정 자동 인쇄

출력 설정에서 프린터를 고르고 "나의 일간 일정 자동 출력"을 켜면, **평일 08:30~10:00 사이
그날 처음으로 조건이 충족되는 시점**에 로그인한 사용자의 담당업무 학사 일정(오늘자)을
선택한 프린터로 자동 인쇄합니다. 하루 한 번만 동작하도록 마지막 인쇄일을 로컬에 기록합니다.

## 웹소켓(작업 요청) 기능

`node2.future-class.kr` 웹소켓 서버는 WorkSupport HTTP API와 별개로, 서버가 클라이언트에게
작업을 요청하고(`task.request`) 클라이언트가 수락/거절로 응답(`task.response`)하는 실시간
채널로 계속 사용합니다. 연결이 끊기면 지수 백오프(2초~30초)로 자동 재연결합니다.

## 아이콘

프로그램 기본 아이콘과 트레이 아이콘은 첨부해 주신 WorkSupport 코드 안의
`viewer/externalLectureViewer.ico`를 그대로 사용합니다(`Resources/AppIcon.ico`로 복사,
빌드 시 실행 파일 아이콘 및 임베디드 리소스로 포함).

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
  다른 창들보다 Z-order 최하단에 두는 근사 구현입니다. 완전한 "진짜 바탕화면 삽입"이
  필요하면 WorkerW 후킹을 추가로 구현해야 합니다.
- **자동 로그인**은 구현하지 않았습니다. 저장된 아이디는 로그인 창에 자동으로 채워지지만
  비밀번호는 저장하지 않으므로, 프로그램을 새로 시작할 때마다 비밀번호 입력이 필요합니다
  (복무 알림/배경화면 학사달력은 로그인 없이도 동작하고, 자동 인쇄와 "정보 수정"만 로그인이
  필요합니다).
- `events.php`의 `addEvent()`는 `createdBy`가 비어 있으면 초기화되지 않은 `$pdo`를 참조하는
  서버측 결함이 있어(별도 수정하지 않음), 클라이언트가 항상 로그인한 사용자의 `user_id`를
  `createdBy`로 채워 보내 이 문제를 피합니다.
- 담당업무가 없는("관련 업무 없음") 일정은 관리자만 수정/삭제할 수 있습니다(작성자 구분이
  없는 공용 일정이라 임의로 아무나 편집하지 못하도록 보수적으로 처리했습니다).

## 다음 작업 제안

- 실제 서버 환경에서 빌드/로그인/일정 등록 전 과정 통합 테스트
- 배경화면 오버레이를 실제 WorkerW에 삽입하는 방식으로 고도화
- 복무 배너에 담당자 이름(현재는 직위만 표시)까지 노출하려면 서버 API에 이름 필드 추가 필요
- 학사 일정에 태그/알림 대상(구성원·부서) 편집 UI 추가 (서버는 이미 지원)
