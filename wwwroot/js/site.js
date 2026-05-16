(() => {
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/service-worker.js').catch(() => {
        // PWA install remains optional; never block the public reporting flow.
      });
    });
  }
})();
