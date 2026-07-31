// Pew Pew Assistant Bridge - Content Script
// Inspects active tab DOM targets when requested by Desktop Core.

(() => {
  console.log('[PewPew Bridge] Content script initialized on target page.');

  window.addEventListener('message', (event) => {
    if (event.source !== window) return;
    if (event.data?.source === 'PEWPEW_ASSISTANT') {
      console.log('[PewPew Bridge] Message received from assistant core.');
    }
  });
})();
