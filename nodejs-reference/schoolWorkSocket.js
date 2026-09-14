'use strict';

/**
 * WorkSupport(교무업무) 전용 웹소켓 메시지 처리 모듈입니다.
 * wsServer.js의 연결 전용 코어에 `.use(createSchoolWorkModule(...))`로 등록해서 사용합니다.
 * 이 파일만 따로 관리/배포하면 SchoolWork 쪽 로직을 wsServer.js와 분리해 패치할 수 있습니다.
 *
 * 처리하는 메시지 타입(LmsAgent의 Networking/MessageTypes.cs와 이름을 맞췄습니다):
 *  - task.request / task.response : 서버 ↔ Windows 클라이언트(교사 PC) 작업 요청/응답 중계
 *  - schedule.updated             : Windows 클라이언트에서 학사 일정을 등록/수정/삭제했을 때
 *                                    같은 학교의 웹페이지(브라우저) 클라이언트들에게 새로고침이
 *                                    필요하다는 것을 릴레이합니다.
 *  - ping / pong                  : 애플리케이션 레벨 keep-alive
 *    (ws 라이브러리 자체의 TCP ping/pong과는 별개로, LmsAgent가 보내는 애플리케이션 메시지입니다)
 *
 * @param {object} deps
 * @param {(context: {socket, request}) => string} [deps.getSchoolIdForSocket]
 *   접속한 소켓이 어느 학교 소속인지 판별하는 함수. 로그인 토큰, 쿼리스트링, 헤더 등
 *   실제 인증 방식에 맞게 구현하세요. 지정하지 않으면 모든 소켓을 같은 그룹으로 취급합니다.
 */
function createSchoolWorkModule({ getSchoolIdForSocket } = {}) {
  /** @type {Map<string, Set<import('ws').WebSocket>>} 학교ID -> 접속한 소켓 집합 */
  const clientsBySchool = new Map();

  function register(context) {
    const schoolId = getSchoolIdForSocket ? getSchoolIdForSocket(context) : 'default';
    context.socket.schoolId = schoolId;

    if (!clientsBySchool.has(schoolId)) {
      clientsBySchool.set(schoolId, new Set());
    }
    clientsBySchool.get(schoolId).add(context.socket);
  }

  function unregister(context) {
    clientsBySchool.get(context.socket.schoolId)?.delete(context.socket);
  }

  function broadcastToSchool(schoolId, envelope, exceptSocket) {
    const set = clientsBySchool.get(schoolId);
    if (!set) return;

    const json = JSON.stringify(envelope);
    for (const client of set) {
      if (client !== exceptSocket && client.readyState === client.OPEN) {
        client.send(json);
      }
    }
  }

  return {
    onConnection(context) {
      register(context);
    },

    onClose(context) {
      unregister(context);
    },

    /** true를 반환하면 이 모듈이 메시지를 처리했다는 뜻이며, 다른 모듈로 넘어가지 않습니다. */
    onMessage(context, envelope) {
      switch (envelope.type) {
        case 'ping':
          context.socket.send(JSON.stringify({ type: 'pong', id: envelope.id }));
          return true;

        case 'task.request':
        case 'task.response':
          // 실제 서비스에서는 envelope.id로 대응하는 요청/응답 짝을 찾아 정확한 대상
          // 소켓으로만 전달하도록 다듬으세요. 여기서는 같은 학교 그룹에 그대로 릴레이합니다.
          broadcastToSchool(context.socket.schoolId, envelope, context.socket);
          return true;

        case 'schedule.updated':
          // Windows 클라이언트(LmsAgent)가 학사 일정을 등록/수정/삭제했다는 알림입니다.
          // 같은 학교의 웹페이지(브라우저) 클라이언트들에게 그대로 릴레이해서
          // 웹 쪽 학사달력 화면이 새로고침되도록 합니다.
          broadcastToSchool(
            context.socket.schoolId,
            { type: 'schedule.updated', id: envelope.id, payload: envelope.payload },
            context.socket,
          );
          return true;

        default:
          return false; // 이 모듈이 모르는 메시지 타입 — 다른 모듈이 처리하도록 넘깁니다.
      }
    },
  };
}

module.exports = { createSchoolWorkModule };
