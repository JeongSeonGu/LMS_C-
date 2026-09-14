'use strict';

/**
 * wsServer.js / schoolWorkSocket.js를 기존 서버에 연결하는 방법을 보여주는 예시입니다.
 * 실제 프로젝트에 그대로 붙여넣지 말고, 기존 서버 구성(express, https 인증서 등)에 맞게
 * http/https 서버 생성 부분만 유지한 채 아래 웹소켓 연결 부분만 참고해서 이식하세요.
 */

const http = require('http');
// const express = require('express');
// const app = express();

const { createWsServer } = require('./wsServer');
const { createSchoolWorkModule } = require('./schoolWorkSocket');

// const server = http.createServer(app); // 기존에 express 등을 쓰고 있다면 이렇게 감싸서 사용하세요.
const server = http.createServer();

const ws = createWsServer(server, { path: '/ws' });

ws.use(createSchoolWorkModule({
  // 실제 인증 방식에 맞게 구현하세요. 예: 로그인 시 발급한 토큰을 쿼리스트링/헤더로 받아 검증.
  getSchoolIdForSocket: (context) => {
    const url = new URL(context.request.url, 'http://localhost');
    return url.searchParams.get('schoolId') || 'default';
  },
}));

const port = process.env.PORT || 3000;
server.listen(port, () => {
  console.log(`WorkSupport 웹소켓 서버가 ${port}번 포트에서 대기 중입니다 (경로: /ws)`);
});
