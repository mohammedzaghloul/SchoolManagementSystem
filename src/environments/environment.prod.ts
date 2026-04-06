type RuntimeEnvironment = {
  apiUrl?: string;
  signalRUrl?: string;
  qrRefreshInterval?: number;
  defaultLanguage?: string;
  supportedLanguages?: string[];
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

export const environment = {
  production: true,
  apiUrl,
  signalRUrl,
  qrRefreshInterval: runtimeEnvironment.qrRefreshInterval ?? 30000,
  defaultLanguage: runtimeEnvironment.defaultLanguage ?? 'ar',
  supportedLanguages: runtimeEnvironment.supportedLanguages ?? ['ar', 'en']
};
