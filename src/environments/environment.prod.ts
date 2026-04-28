type RuntimeEnvironment = {
  apiUrl?: string;
  signalRUrl?: string;
  centralAuthPortalUrl?: string;
  qrRefreshInterval?: number;
  defaultLanguage?: string;
  supportedLanguages?: string[];
  vodafoneCashNumber?: string;
  supportPhone?: string;
};

const runtimeEnvironment =
  typeof globalThis !== 'undefined' && '__env' in globalThis
    ? ((globalThis as typeof globalThis & { __env?: RuntimeEnvironment }).__env ?? {})
    : {};

const normalizeUrl = (value?: string): string => {
  const normalizedValue = value?.trim() ?? '';
  if (!normalizedValue || normalizedValue === '/') {
    return normalizedValue;
  }

  return normalizedValue.replace(/\/+$/, '');
};

const apiUrl = normalizeUrl(runtimeEnvironment.apiUrl) || '';
const signalRUrl = normalizeUrl(runtimeEnvironment.signalRUrl)
  || (apiUrl ? `${apiUrl}/chathub` : '/chathub');
const centralAuthPortalUrl = normalizeUrl(runtimeEnvironment.centralAuthPortalUrl)
  || 'https://resplendent-cooperation-production-eeb8.up.railway.app/dashboard';

export const environment = {
  production: true,
  apiUrl,
  signalRUrl,
  centralAuthPortalUrl,
  qrRefreshInterval: runtimeEnvironment.qrRefreshInterval ?? 30000,
  defaultLanguage: runtimeEnvironment.defaultLanguage ?? 'ar',
  supportedLanguages: runtimeEnvironment.supportedLanguages ?? ['ar', 'en'],
  paymentGateway: {
    demoMode: true,
    vodafoneCashNumber: runtimeEnvironment.vodafoneCashNumber?.trim() || '0100',
    supportPhone:
      runtimeEnvironment.supportPhone?.trim()
      || runtimeEnvironment.vodafoneCashNumber?.trim()
      || '0100'
  }
};
