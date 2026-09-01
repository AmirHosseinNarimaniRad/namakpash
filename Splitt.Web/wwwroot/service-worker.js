// Development only: an empty service worker so `dotnet run` never serves stale assets while
// editing. The real one is service-worker.published.js, which the SDK swaps in on publish.
self.addEventListener('fetch', () => { });
