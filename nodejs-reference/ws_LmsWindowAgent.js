'use strict';

/**
 * 교무업무 Windows 클라이언트(LmsAgent) 전용 소켓 메세지 처리 모듈입니다.
 * (기존 ws_InteractiveMsg.js와 같은 역할을 LMS_WindowAgent 그룹에 대해 담당합니다.)
 *
 * 연결 파일의 socket.on('LMS_WindowAgent_ms', ...)에서
 *   ws_LmsWindowAgent.SocketMsg_LmsWindowAgentMsg(data, io, userList)
 * 형태로 호출됩니다.
 *
 * data는 LmsAgent(Windows, C#)의 WsEnvelope 그대로입니다:
 *   { type: "schedule.updated", id: "...", payload: { ... } }
 * type 값은 LmsAgent/Networking/MessageTypes.cs에 정의된 상수와 동일합니다.
 *
 * ⚠️ userList의 실제 필드명은 기존 Add_UserList 구현에 맞춰 조정하세요.
 * 여기서는 Add_UserList(data, io, socket.id, "Connection_LMS_WindowAgent")로 등록된
 * 각 접속자가 최소한 { socketId, group } 형태를 갖는다고 가정합니다
 * (다른 서비스의 userList 항목 구조를 그대로 따르면 됩니다).
 */

const GROUP_PREFIX = 'Connection_LMS_WindowAgent';

function SocketMsg_LmsWindowAgentMsg(data, io, userList) {
    if (!data || !data.type) {
        return;
    }

    switch (data.type) {
        case 'ping':
            // 애플리케이션 레벨 keep-alive. 필요하면 보낸 소켓에만 pong으로 응답하도록 확장하세요.
            break;

        case 'task.request':
        case 'task.response':
            // 서버 ↔ Windows 클라이언트(교사 PC)의 작업 요청/응답 중계.
            // 특정 교사 PC로만 보내야 한다면 data.payload에 담긴 식별자(예: teacherId)로
            // userList에서 대상 소켓 하나만 찾아 io.to(target.socketId).emit(...)하도록 다듬으세요.
            broadcastToGroup(data, io, userList, GROUP_PREFIX);
            break;

        case 'schedule.updated':
            // Windows 클라이언트(LmsAgent)에서 학사 일정을 등록/수정/삭제했다는 알림입니다.
            // 같은 학교의 웹페이지(브라우저) 클라이언트도 이 그룹에 함께 접속해 있다고 보고
            // 그대로 릴레이해서, 웹 쪽 학사달력이 새로고침되도록 합니다.
            broadcastToGroup(data, io, userList, GROUP_PREFIX);
            break;

        default:
            console.warn('알 수 없는 LMS_WindowAgent 메세지 타입:', data.type);
    }
}

/**
 * userList에서 같은 그룹(prefix)에 속한 소켓들에게 메세지를 그대로 릴레이합니다.
 * 학교 단위로 나눠 보내야 한다면(여러 학교가 같은 서버를 공유하는 경우) data나
 * Add_UserList로 등록할 때 schoolId 같은 필드를 함께 저장해 두고 여기서 필터를 추가하세요.
 */
function broadcastToGroup(data, io, userList, groupPrefix) {
    const targets = (userList || []).filter(
        (u) => typeof u.group === 'string' && u.group.indexOf(groupPrefix) === 0,
    );

    for (const target of targets) {
        io.to(target.socketId).emit('LMS_WindowAgent_ms', data);
    }
}

module.exports = { SocketMsg_LmsWindowAgentMsg };
