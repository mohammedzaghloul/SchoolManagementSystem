// environments/environment.ts
export const environment = {
  production: false,
  apiUrl: (window as any).__env?.apiUrl || 'http://localhost:5033',
  signalRUrl: (window as any).__env?.signalRUrl || 'http://localhost:5033/chathub',
  centralAuthPortalUrl: (window as any).__env?.centralAuthPortalUrl || 'http://localhost:4300/dashboard/',
  qrRefreshInterval: 5000, // 5 seconds
  defaultLanguage: 'ar',
  supportedLanguages: ['ar', 'en'],
  paymentGateway: {
    demoMode: true,
    vodafoneCashNumber: (window as any).__env?.vodafoneCashNumber || '0100',
    supportPhone: (window as any).__env?.supportPhone || (window as any).__env?.vodafoneCashNumber || '0100'
  }
};
