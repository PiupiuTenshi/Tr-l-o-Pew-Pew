const connectButton = document.getElementById('connect');
const connectionStatus = document.getElementById('connection');

connectButton.addEventListener('click', () => {
  chrome.runtime.sendMessage({ type: 'CONNECT_NATIVE_TRANSPORT' }, (response) => {
    if (chrome.runtime.lastError || !response?.connected) {
      const reason = response?.reason || chrome.runtime.lastError?.message || 'unknown_error';
      connectionStatus.textContent = `Native host connection failed: ${reason}`;
      return;
    }

    connectionStatus.textContent = 'Native host connected for this browser session.';
  });
});
