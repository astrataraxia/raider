// Raider 정적 디자인 프로토타입을 로컬에서 제공한다.
const fs = require("fs");
const http = require("http");
const path = require("path");

const root = __dirname;
const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
};

http
  .createServer((request, response) => {
    const requestPath = request.url === "/" ? "index.html" : request.url.slice(1);
    const filePath = path.join(root, requestPath);

    fs.readFile(filePath, (error, contents) => {
      if (error) {
        response.writeHead(404);
        response.end("Not found");
        return;
      }

      response.setHeader("Content-Type", contentTypes[path.extname(filePath)] ?? "application/octet-stream");
      response.end(contents);
    });
  })
  .listen(4173, "127.0.0.1");
