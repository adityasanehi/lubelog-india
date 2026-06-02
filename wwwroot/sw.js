const CACHE_NAME = 'lubelogger-india-v1';
const SHELL_ASSETS = [
    '/',
    '/css/site.css',
    '/js/shared.js',
    '/js/loader.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/defaults/lubelogger_icon_192.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL_ASSETS))
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    // Only cache GET requests for same-origin shell assets
    if (event.request.method !== 'GET' || !event.request.url.startsWith(self.location.origin)) return;
    const url = new URL(event.request.url);
    // Don't cache API or dynamic routes
    if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/Vehicle/') || url.pathname.startsWith('/Home/')) return;
    event.respondWith(
        caches.match(event.request).then(cached => cached || fetch(event.request))
    );
});

self.addEventListener('push', event => {
    let data = { title: 'LubeLogger', body: 'You have a new notification.', url: '/' };
    if (event.data) {
        try { data = JSON.parse(event.data.text()); } catch (e) {}
    }
    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: '/defaults/lubelogger_icon_192.png',
            badge: '/defaults/lubelogger_maskable_icon_72.png',
            tag: 'lubelogger-reminder',
            data: { url: data.url }
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = event.notification.data?.url || '/';
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
            for (const client of windowClients) {
                if (client.url === url && 'focus' in client) return client.focus();
            }
            if (clients.openWindow) return clients.openWindow(url);
        })
    );
});
