(function(window) {
  window.__env = window.__env || {};

  // API url - default locally, will be replaced during Railway build/start
  window.__env.apiUrl = 'http://localhost:5033';

  // SignalR url
  window.__env.signalRUrl = 'http://localhost:5033/chathub';

  // Demo payment contacts
  window.__env.vodafoneCashNumber = '0100';
  window.__env.supportPhone = '0100';

  // Additional branding config
  window.__env.appName = 'EDU';
}(this));
