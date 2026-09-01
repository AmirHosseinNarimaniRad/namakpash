// Offline-first cache for the whole app shell. Launching from the home screen with no network
// is the normal case for this app, not an edge case: the landing page promises «بعد از آن کاملاً
// آفلاین کار می‌کند», and the data has always been local — only the shell needed the network.
//
// Everything is precached on install, so the first visit must be online; after that every launch
// is served from the cache and the network is never on the critical path.

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'namakpash-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

// .wasm carries the runtime and the assemblies, .dat is ICU, .woff2 is Vazirmatn — an app that
// falls back to a system font offline would not look like the same app.
const offlineAssetsInclude = [/\.wasm$/, /\.js$/, /\.json$/, /\.webmanifest$/, /\.css$/, /\.woff2?$/, /\.html$/, /\.png$/, /\.ico$/, /\.dat$/, /\.blat$/];
const offlineAssetsExclude = [/service-worker\.js$/];

// StaticWebAssetBasePath already puts an "app/" prefix on every url in the assets manifest, so
// these resolve from the origin root. Resolving them against the worker's own scope (/app/)
// instead would ask for /app/app/... — cache.addAll rejects on the first 404 and then nothing is
// cached at all, which fails silently until the next launch with no network.
const baseUrl = new URL('/', self.origin);
const assetUrl = asset => new URL(asset.url, baseUrl).href;
const manifestUrlList = self.assetsManifest.assets.map(assetUrl);

async function onInstall() {
    // Without this a new version installs and then waits for every window to close before it
    // takes over. A home-screen app is rarely "closed", so a shipped fix could sit unused
    // indefinitely — measured: the update stayed in `waiting` across repeated launches.
    self.skipWaiting();

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        // no-cache so a precache never inherits a half-stale HTTP cache entry.
        .map(asset => new Request(assetUrl(asset), { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate() {
    // Drop every previous version's cache; the version is part of the name.
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // Take over the open pages too, so the next launch is served the new version rather than
    // whatever the previous worker was still holding. Safe here because Blazor loads every
    // assembly at boot: a page already running keeps what it has and picks the rest up on
    // its next load.
    await self.clients.claim();
}

async function onFetch(event) {
    let cachedResponse = null;

    if (event.request.method === 'GET') {
        // Deep links (/app/trip/1) are the router's business, so every navigation is answered
        // with index.html — the same job docs/404.html does for the server.
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? new URL('app/index.html', baseUrl).href : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}
