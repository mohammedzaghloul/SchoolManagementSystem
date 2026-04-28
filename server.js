const fs = require('fs');
const http = require('http');
const path = require('path');

const distDir = path.join(__dirname, 'dist', 'school-management');
const indexFile = path.join(distDir, 'index.html');
const port = Number(process.env.PORT || 3000);

const mimeTypes = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.ico': 'image/x-icon',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.txt': 'text/plain; charset=utf-8',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2'
};

const normalizeUrl = (value) => {
  const normalizedValue = (value || '').trim();
  if (!normalizedValue || normalizedValue === '/') {
    return normalizedValue;
  }

  return normalizedValue.replace(/\/+$/, '');
};

const getRuntimeConfig = () => {
  const apiUrl = normalizeUrl(process.env.API_URL || process.env.PUBLIC_API_URL || '');
  const signalRUrl = normalizeUrl(process.env.SIGNALR_URL || '') || (apiUrl ? `${apiUrl}/chathub` : '/chathub');
  const supportedLanguages = (process.env.SUPPORTED_LANGUAGES || 'ar,en')
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean);

  return {
    apiUrl,
    signalRUrl,
    centralAuthPortalUrl: normalizeUrl(
      process.env.CENTRAL_AUTH_PORTAL_URL
      || process.env.CENTRAL_AUTH_FRONTEND_URL
      || 'https://resplendent-cooperation-production-eeb8.up.railway.app/dashboard/'
    ),
    qrRefreshInterval: Number(process.env.QR_REFRESH_INTERVAL || 30000),
    defaultLanguage: process.env.DEFAULT_LANGUAGE || 'ar',
    supportedLanguages: supportedLanguages.length > 0 ? supportedLanguages : ['ar', 'en']
  };
};

const sendFile = (response, filePath) => {
  const extension = path.extname(filePath).toLowerCase();
  const contentType = mimeTypes[extension] || 'application/octet-stream';

  fs.readFile(filePath, (error, content) => {
    if (error) {
      response.writeHead(500, { 'Content-Type': 'text/plain; charset=utf-8' });
      response.end('Failed to read the requested file.');
      return;
    }

    response.writeHead(200, { 'Content-Type': contentType });
    response.end(content);
  });
};

const server = http.createServer((request, response) => {
  const requestedPath = decodeURIComponent((request.url || '/').split('?')[0]);

  if (requestedPath === '/env.js') {
    response.writeHead(200, { 'Content-Type': 'application/javascript; charset=utf-8' });
    response.end(`window.__env = ${JSON.stringify(getRuntimeConfig())};`);
    return;
  }

  const relativePath = requestedPath === '/' ? 'index.html' : requestedPath.replace(/^\/+/, '');
  const resolvedPath = path.resolve(path.join(distDir, relativePath));

  if (!resolvedPath.startsWith(distDir)) {
    response.writeHead(403, { 'Content-Type': 'text/plain; charset=utf-8' });
    response.end('Forbidden');
    return;
  }

  fs.stat(resolvedPath, (error, stats) => {
    if (!error && stats.isFile()) {
      sendFile(response, resolvedPath);
      return;
    }

    sendFile(response, indexFile);
  });
});

server.listen(port, '0.0.0.0', () => {
  console.log(`Frontend server listening on port ${port}`);
});
