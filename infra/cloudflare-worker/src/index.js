// Host-rewrite proxy: lets the App Service run on the Free (F1) tier, which
// forbids custom hostname bindings. Cloudflare terminates TLS for
// app.appointme.dev and this Worker forwards to the azurewebsites origin,
// carrying the public hostname in X-Original-Host (mapped by
// ForwardedHeadersOptions in Program.cs).
const ORIGIN_HOST = 'app-appointme-devtest-ze5tkm.azurewebsites.net';
const PUBLIC_HOST = 'app.appointme.dev';

export default {
    async fetch(request) {
        const url = new URL(request.url);
        url.hostname = ORIGIN_HOST;
        const upstream = new Request(url, request);
        upstream.headers.set('X-Original-Host', PUBLIC_HOST);
        return fetch(upstream);
    },
};
