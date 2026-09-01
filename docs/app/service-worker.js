/* Manifest version: XIaWNEIk */
// Offline-first cache for the whole app shell. Launching from the home screen with no network
// is the normal case for this app, not an edge case: the landing page promises «بعد از آن کاملاً
// آفلاین کار می‌کند», and the data has always been local — only the shell needed the network.
//
// Written for a slow, lossy connection, because that is what this audience has. The rule
// throughout: never let one failed request cost the user their offline app.

self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));
self.addEventListener('message', event => {
    // The page tells us which ICU shard the runtime actually picked; see index.html.
    if (event.data && event.data.type === 'cache' && typeof event.data.url === 'string')
        event.waitUntil(cacheUrl(event.data.url));
});

const cacheNamePrefix = 'namakpash-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

// .wasm carries the runtime and the assemblies, .woff2 is Vazirmatn — an app that falls back to
// a system font offline would not look like the same app. ICU (.dat) is deliberately absent:
// three shards are published and only the one matching the culture is ever requested, so
// precaching all of them would drag ~2.6 MB of dead weight down a slow link and add three more
// chances to fail. The one actually used gets cached by onFetch on the first load.
const offlineAssetsInclude = [/\.wasm$/, /\.js$/, /\.json$/, /\.webmanifest$/, /\.css$/, /\.woff2?$/, /\.html$/, /\.png$/, /\.ico$/];
const offlineAssetsExclude = [/service-worker\.js$/];

// StaticWebAssetBasePath already puts an "app/" prefix on every url in the assets manifest, so
// these resolve from the origin root. Resolving them against the worker's own scope (/app/)
// instead would ask for /app/app/... and nothing would ever be cached.
const baseUrl = new URL('/', self.origin);
const assetUrl = asset => new URL(asset.url, baseUrl).href;
const manifestUrlList = self.assetsManifest.assets.map(assetUrl);
const indexHtmlUrl = new URL('app/index.html', baseUrl).href;

async function onInstall() {
    // Without this a new version installs and then waits for every window to close before it
    // takes over. A home-screen app is rarely "closed", so a shipped fix could sit unused
    // indefinitely — measured: the update stayed in `waiting` across repeated launches.
    self.skipWaiting();

    const cache = await caches.open(cacheName);
    const assets = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)));

    // Deliberately NOT cache.addAll: that rejects the whole batch if a single request fails, and
    // a rejected install is discarded — leaving no cache AND no active worker, so the app is not
    // merely still online-only, it is broken offline with no sign of why. One flaky request out
    // of ~55 is close to certain on a weak connection; it happened on the first real phone that
    // tried it. Here each asset stands alone and whatever is missed is filled in by onFetch.
    await inBatches(assets, 6, asset => cacheAsset(cache, asset));
}

/// Caches one asset, retrying once. Never throws: a miss is a gap to fill later, not a failure.
async function cacheAsset(cache, asset) {
    for (let attempt = 0; attempt < 2; attempt++) {
        try {
            // The url is content-fingerprinted, so reusing the browser's own copy is safe — and
            // it saves downloading the whole app a second time right after the page loaded it,
            // which is what 'no-cache' here used to do.
            const request = new Request(assetUrl(asset), { integrity: asset.hash, cache: 'default' });
            const response = await fetch(request);
            if (response.ok) {
                await cache.put(assetUrl(asset), response);
                return true;
            }
        } catch {
            // fall through to the retry, then give up quietly
        }
    }
    return false;
}

/// Caches one url that install could not know about. Never throws.
async function cacheUrl(url) {
    try {
        const cache = await caches.open(cacheName);
        if (await cache.match(url))
            return;

        const response = await fetch(url, { cache: 'default' });
        if (response.ok)
            await cache.put(url, response);
    } catch {
        // the next load asks again
    }
}

/// Limited concurrency: a phone on a weak connection does worse with 55 requests at once.
async function inBatches(items, size, work) {
    for (let i = 0; i < items.length; i += size)
        await Promise.all(items.slice(i, i + size).map(work));
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
    if (event.request.method !== 'GET')
        return fetch(event.request);

    // Deep links (/app/trip/1) are the router's business, so every navigation is answered
    // with index.html — the same job docs/404.html does for the server.
    const shouldServeIndexHtml = event.request.mode === 'navigate'
        && !manifestUrlList.some(url => url === event.request.url);

    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(shouldServeIndexHtml ? indexHtmlUrl : event.request);
    if (cachedResponse)
        return cachedResponse;

    // Not cached yet — either install missed it, or it is the ICU shard this culture actually
    // uses. Serve it from the network and keep a copy, so the cache completes itself through
    // ordinary use instead of needing one flawless download of everything up front.
    const response = await fetch(event.request);
    if (response.ok && response.type === 'basic')
        cache.put(event.request, response.clone()).catch(() => { });

    return response;
}
