# LMS_C-

LMS연동 윈도우 프로그램

Windows 로그인 시 자동 실행되어 트레이(작업 표시줄 알림 영역)에 상주하며,
`node2.future-class.kr` 웹소켓 서버를 통해 학사 일정/사용자 정보를 주고받고
서버로부터 작업을 요청받아 처리하는 상시 실행형 클라이언트입니다.

## 기술 스택

- .NET 8 (Windows Forms), C# 12
- `System.Net.WebSockets.ClientWebSocket` 기반 웹소켓 연동
- 트레이 아이콘 상주 방식 (메인 창 없이 `ApplicationContext`로 실행)

## 프로젝트 구조

```
src/LmsAgent/
  Program.cs                    진입점: 중복 실행 방지, 업데이트 확인, 트레이 실행
  App/TrayApplicationContext.cs 트레이 아이콘과 전체 메뉴 구성
  Configuration/                AppSettings, 로컬 설정 파일(JSON) 저장/로드
  Networking/                   웹소켓 클라이언트, 메시지 봉투/타입 정의
  Services/                     세션 관리, 자동 시작(레지스트리), 업데이트 서비스
  Models/                       서버와 주고받는 메시지 페이로드 모델
  Forms/                        로그인 / 정보 수정 / 일정 등록 / 설정 / 작업 요청 창
```

## 메뉴 구성

프로그램은 실행 후 별도 창 없이 트레이 아이콘 상태로 최소화되어 상주합니다.
트레이 아이콘을 우클릭하면 아래 메뉴가 나타납니다.

- 연결 상태 표시 (읽기 전용)
- **학사 일정**
  - 일정 등록...
- **사용자 정보**
  - 로그인...
  - 정보 수정... (로그인 후 활성화)
- 설정... (서버 주소, 업데이트 서버 주소, Windows 자동 시작 여부)
- 업데이트 확인...
- 종료

## 웹소켓 통신 프로토콜

모든 메시지는 아래와 같은 JSON 봉투(envelope) 형식으로 주고받습니다.

```json
{ "type": "auth.login", "id": "요청-응답 상관관계 ID", "payload": { ... } }
```

`Networking/MessageTypes.cs` 에 정의된 메시지 타입 (실제 서버 프로토콜에 맞게 조정 필요):

| 타입 | 방향 | 설명 |
|---|---|---|
| `auth.login` / `auth.login.result` | 클라이언트→서버→클라이언트 | 로그인 요청/결과 |
| `user.updateProfile` / `.result` | 클라이언트→서버→클라이언트 | 사용자 정보 수정 |
| `schedule.register` / `.result` | 클라이언트→서버→클라이언트 | 학사 일정 등록 |
| `task.request` | 서버→클라이언트 | 서버가 클라이언트에 작업을 요청 (push) |
| `task.response` | 클라이언트→서버 | 작업 요청에 대한 수락/거절 회신 |
| `ping` / `pong` | 양방향 | 연결 유지 |

연결이 끊기면 지수 백오프(2초~30초)로 자동 재연결합니다.

## 업데이트 방식

프로그램 시작 시 매번 `UpdateManifestUrl` (설정의 업데이트 서버 주소)에서
아래와 같은 매니페스트를 조회합니다.

```json
{
  "version": "1.0.1",
  "downloadUrl": "https://node2.future-class.kr/update/LmsAgent-1.0.1.zip",
  "sha256": "다운로드 파일 무결성 검증용 해시(선택)",
  "notes": "변경 내역(선택)",
  "mandatory": false
}
```

현재 버전보다 높은 버전이 있으면 zip을 내려받아 해시를 검증한 뒤, 실행 중인
exe 자신은 스스로 덮어쓸 수 없으므로 임시 배치 스크립트를 띄워
(1) 현재 프로세스 종료 대기 → (2) 새 파일로 교체 → (3) 프로그램 재시작
순서로 적용합니다.

## Windows 자동 시작

`Services/AutoStartManager.cs` 가 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
레지스트리 키에 실행 파일 경로를 등록/해제합니다. 관리자 권한이 필요 없습니다.

## 빌드 및 실행 (Windows, .NET 8 SDK 필요)

```
dotnet build LMS_C-.sln
dotnet run --project src/LmsAgent/LmsAgent.csproj
```

> Windows Forms는 Windows 데스크톱 런타임이 필요하므로 macOS/Linux에서는
> 빌드/실행할 수 없습니다. Windows 환경(또는 Windows용 CI)에서 빌드하세요.

## 다음 작업 제안

- 실제 서버와의 인증 방식(토큰 갱신, 만료 처리) 확정 및 반영
- 로그인 정보(암호) 안전한 저장이 필요하다면 DPAPI(`ProtectedData`) 적용
- 업데이트 매니페스트/다운로드 URL을 실제 업데이트 서버 스펙에 맞게 조정
- 학사 일정 목록 조회, 수정/삭제 등 CRUD 메뉴 확장
