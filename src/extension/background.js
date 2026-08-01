// Pew Pew Assistant Bridge - Background Service Worker (Manifest V3).
// This is transport setup only. It cannot issue browser actions or inspect DOM.

const PROHIBITED_SCHEMES = ['chrome:', 'chrome-extension:', 'file:', 'about:', 'edge:'];

function isOriginAllowed(url) {
  if (!url) return false;
  try {
    const parsed = new URL(url);
    if (PROHIBITED_SCHEMES.includes(parsed.protocol)) {
      return false;
    }
    return parsed.protocol === 'https:' || parsed.protocol === 'http:';
  } catch (e) {
    return false;
  }
}

let nativePort = null;

function connectNativeTransport() {
  if (nativePort) return { connected: true };
  try {
    nativePort = chrome.runtime.connectNative('com.pewpew.assistant.bridge');
    nativePort.onDisconnect.addListener(() => { nativePort = null; });
    nativePort.postMessage({ version: 1, kind: 'connect', correlationId: crypto.randomUUID() });
    return { connected: true };
  } catch (error) {
    nativePort = null;
    return { connected: false, reason: 'native_transport_unavailable' };
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'CHECK_ORIGIN') {
    sendResponse({ allowed: false, reason: 'desktop_policy_required' });
    return true;
  }
  if (message.type === 'GET_STATUS') {
    sendResponse({ status: nativePort ? 'connected' : 'disconnected', version: '1.0.0' });
    return true;
  }
  if (message.type === 'CONNECT_NATIVE_TRANSPORT') {
    sendResponse(connectNativeTransport());
    return true;
  }
});
