'use strict';

const { WebSocketServer } = require('ws');

/**
 * 접속/해제 관리만 담당하는 순수 웹소켓 코어입니다.
 * 메시지 내용 해석이나 라우팅은 하지 않고, 등록된 모듈들에게 위임합니다.
 *
 * 이렇게 나눠 둔 이유: 학사업무(SchoolWork) 관련 메시지 처리 로직이 자주 바뀌더라도
 * 이 파일(연결 관리 자체)은 건드릴 필요가 없도록 하기 위해서입니다.
 * 새 기능이 필요하면 이 파일을 고치지 말고 schoolWorkSocket.js 같은 모듈을 새로 만들어
 * `.use(module)`로 등록하세요.
 *
 * @param {import('http').Server} server  기존 HTTP(S) 서버(예: express app을 감싼 http.createServer(app))
 * @param {{ path?: string }} options     업그레이드를 받을 경로(기본 "/ws")
 */
function createWsServer(server, options = {}) {
  const wss = new WebSocketServer({ server, path: options.path || '/ws' });
  const modules = [];

  wss.on('connection', (socket, request) => {
    socket.isAlive = true;
    socket.on('pong', () => {
      socket.isAlive = true;
    });

    const context = { socket, request, wss };

    for (const mod of modules) {
      mod.onConnection?.(context);
    }

    socket.on('message', (raw) => {
      let envelope;
      try {
        envelope = JSON.parse(raw.toString());
      } catch (err) {
        socket.send(JSON.stringify({
          type: 'error',
          id: null,
          payload: { message: '잘못된 메시지 형식입니다.' },
        }));
        return;
      }

      for (const mod of modules) {
        // 모듈이 true를 반환하면 이 메시지를 처리했다는 뜻이므로 다음 모듈로 넘기지 않습니다.
        if (mod.onMessage?.(context, envelope) === true) {
          return;
        }
      }
    });

    socket.on('close', () => {
      for (const mod of modules) {
        mod.onClose?.(context);
      }
    });

    socket.on('error', (err) => {
      console.error('[ws] socket error:', err.message);
    });
  });

  // 응답 없는(죽은) 연결을 주기적으로 정리합니다. LmsAgent(Windows) 쪽은
  // ClientWebSocket이 연결이 끊기면 지수 백오프로 재연결하므로 안전하게 끊어도 됩니다.
  const heartbeat = setInterval(() => {
    for (const socket of wss.clients) {
      if (socket.isAlive === false) {
        socket.terminate();
        continue;
      }
      socket.isAlive = false;
      socket.ping();
    }
  }, 30_000);

  wss.on('close', () => clearInterval(heartbeat));

  return {
    wss,
    /** SchoolWork 등 기능별 메시지 처리 모듈을 등록합니다. 여러 개 등록할 수 있습니다. */
    use(mod) {
      modules.push(mod);
      return this;
    },
  };
}

module.exports = { createWsServer };
