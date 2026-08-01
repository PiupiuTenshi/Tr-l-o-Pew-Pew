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
let sessionToken = null;
let pollTimer = null;
let pendingConnection = null;

const ALLOWED_ACTIONS = new Set(['play', 'pause', 'mute', 'unmute']);

function metadataOnlyReadback(action, result) {
  return {
    version: 1,
    kind: 'action_readback',
    correlationId: crypto.randomUUID(),
    sessionToken,
    nonce: crypto.randomUUID(),
    timestampUtc: new Date().toISOString(),
    targetOrigin: result.origin,
    tabId: String(result.tabId),
    snapshotId: result.snapshotId,
    snapshotVersion: result.snapshotVersion,
    navigationGeneration: result.navigationGeneration,
    commandId: result.commandId,
    payloadHash: result.payloadHash,
    readbackStatus: result.status
  };
}

async function executeTypedMediaCommand(command) {
  if (!command || command.sessionToken !== sessionToken || !ALLOWED_ACTIONS.has(command.actionKind)) {
    return;
  }

  const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (!tab || String(tab.id) !== command.tabId || !isOriginAllowed(tab.url)) {
    nativePort?.postMessage(metadataOnlyReadback(command.actionKind, {
      origin: command.targetOrigin, tabId: command.tabId, snapshotId: command.snapshotId,
      snapshotVersion: command.snapshotVersion, navigationGeneration: command.navigationGeneration,
      commandId: command.commandId, payloadHash: command.payloadHash, status: 'binding_denied'
    }));
    return;
  }

  const currentOrigin = new URL(tab.url).origin;
  if (currentOrigin !== command.targetOrigin) {
    nativePort?.postMessage(metadataOnlyReadback(command.actionKind, {
      origin: command.targetOrigin, tabId: command.tabId, snapshotId: command.snapshotId,
      snapshotVersion: command.snapshotVersion, navigationGeneration: command.navigationGeneration,
      commandId: command.commandId, payloadHash: command.payloadHash, status: 'origin_mismatch'
    }));
    return;
  }

  try {
    const [injection] = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: (action) => {
        const videos = Array.from(document.querySelectorAll('video')).filter((video) => {
          const box = video.getBoundingClientRect();
          const style = getComputedStyle(video);
          return box.width >= 160 && box.height >= 90 && style.display !== 'none' && style.visibility !== 'hidden';
        });
        if (videos.length !== 1) return { status: 'target_ambiguous' };
        const video = videos[0];
        if (action === 'play') void video.play();
        if (action === 'pause') video.pause();
        if (action === 'mute') video.muted = true;
        if (action === 'unmute') video.muted = false;
        const mediaState = video.paused ? 'paused' : 'playing';
        const status = action === 'play' ? `observed_${mediaState}`
          : action === 'pause' ? `observed_${mediaState}`
          : action === 'mute' ? (video.muted ? 'observed_muted' : 'observed_unmuted')
          : (video.muted ? 'observed_muted' : 'observed_unmuted');
        return { status };
      },
      args: [command.actionKind]
    });
    nativePort?.postMessage(metadataOnlyReadback(command.actionKind, {
      origin: command.targetOrigin, tabId: command.tabId, snapshotId: command.snapshotId,
      snapshotVersion: command.snapshotVersion, navigationGeneration: command.navigationGeneration,
      commandId: command.commandId, payloadHash: command.payloadHash,
      status: injection?.result?.status ?? 'readback_unknown'
    }));
  } catch (_) {
    nativePort?.postMessage(metadataOnlyReadback(command.actionKind, {
      origin: command.targetOrigin, tabId: command.tabId, snapshotId: command.snapshotId,
      snapshotVersion: command.snapshotVersion, navigationGeneration: command.navigationGeneration,
      commandId: command.commandId, payloadHash: command.payloadHash, status: 'execution_unavailable'
    }));
  }
}

function startCommandPolling() {
  if (pollTimer || !nativePort || !sessionToken) return;
  pollTimer = setInterval(async () => {
    try {
      const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
      if (!tab || !isOriginAllowed(tab.url)) return;
      nativePort?.postMessage({
        version: 1,
        kind: 'command_poll',
        correlationId: crypto.randomUUID(),
        sessionToken,
        nonce: crypto.randomUUID(),
        timestampUtc: new Date().toISOString(),
        targetOrigin: new URL(tab.url).origin,
        tabId: String(tab.id)
      });
    } catch (_) {
      // A polling failure never broadens browser access or retries an action.
    }
  }, 500);
}

function connectNativeTransport() {
  if (nativePort && sessionToken) return Promise.resolve({ connected: true });
  if (pendingConnection) return pendingConnection;

  pendingConnection = new Promise((resolve) => {
    let settled = false;
    const finish = (response) => {
      if (settled) return;
      settled = true;
      clearTimeout(timeout);
      pendingConnection = null;
      resolve(response);
    };
    const timeout = setTimeout(() => finish({
      connected: false,
      reason: 'native_host_connection_timed_out'
    }), 6000);

    try {
      nativePort = chrome.runtime.connectNative('com.pewpew.assistant.bridge');
      nativePort.onDisconnect.addListener(() => {
        const reason = chrome.runtime.lastError?.message || 'native_host_disconnected';
        nativePort = null;
        sessionToken = null;
        if (pollTimer) clearInterval(pollTimer);
        pollTimer = null;
        finish({ connected: false, reason });
      });
      nativePort.onMessage.addListener((message) => {
        if (message?.sessionToken) {
          sessionToken = message.sessionToken;
          startCommandPolling();
          finish({ connected: true });
        }
        if (message?.browserCommand) void executeTypedMediaCommand(message.browserCommand);
      });
      nativePort.postMessage({ version: 1, kind: 'connect', correlationId: crypto.randomUUID() });
    } catch (error) {
      nativePort = null;
      const reason = error instanceof Error && error.message
        ? error.message
        : 'native_host_connection_failed';
      finish({ connected: false, reason });
    }
  });

  return pendingConnection;
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
    connectNativeTransport().then(sendResponse);
    return true;
  }
});
