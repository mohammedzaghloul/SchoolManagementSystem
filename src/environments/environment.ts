// environments/environment.ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5033',
  signalRUrl: 'http://localhost:5033/chathub',
  qrRefreshInterval: 30000, // 30 seconds
  defaultLanguage: 'ar',
  supportedLanguages: ['ar', 'en']
};
