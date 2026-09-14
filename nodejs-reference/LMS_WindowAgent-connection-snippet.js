/**
 * 기존 소켓 연결 파일(각 서비스가 io.on('connection', socket => { ... }) 안에서
 * socket.on('ClassVote', ...) / socket.on('ClassVote_ms', ...) 식으로 서비스별
 * 이벤트를 등록해 두는 그 파일)에 아래 블록을 그대로 추가하세요.
 *
 * 기존 ClassVote 블록과 완전히 같은 구조입니다 — 연결/입장용 이벤트 하나와
 * 실제 메세지 처리용 이벤트 하나를 등록하고, 메세지 처리 자체는 다른 서비스들처럼
 * 별도 파일(ws_LmsWindowAgent.js)에 위임해서 이 파일과 완전히 분리해 둡니다.
 * 이렇게 분리해 두면 LmsAgent(Windows) 쪽 프로토콜이 바뀌어도 이 연결 파일은
 * 건드릴 필요 없이 ws_LmsWindowAgent.js만 고치면 됩니다.
 *
 * data(첫 번째 인자)는 LmsAgent(Windows, C#)가 보내는 WsEnvelope 그대로입니다:
 *   { type: "task.response", id: "...", payload: { ... } }
 * (LmsAgent/Networking/WsEnvelope.cs, MessageTypes.cs 참고)
 */

//#region LMS_WindowAgent 웹소켓 연결 및 메세지 처리
socket.on('LMS_WindowAgent', function (data) {

    console.log("A user connected to the LMS_WindowAgent group");

    Add_UserList(data, io, socket.id, "Connection_LMS_WindowAgent");

});

// 교무업무 Windows 클라이언트(LmsAgent) 이용을 위한 메세지처리
socket.on('LMS_WindowAgent_ms', function (data) {

    try {
        const ws_LmsWindowAgent = require("./ws_LmsWindowAgent");
        ws_LmsWindowAgent.SocketMsg_LmsWindowAgentMsg(data, io, userList);
    } catch (e) {
        console.error("LMS_WindowAgent 처리 오류:", e.message);
    }

});

//#endregion
