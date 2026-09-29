/**
 * sapi.patjo.uk edge worker — Cloudflare
 *
 * Proxy for SAPI-SK .NET server (4 swagger categories: SAPI, Connector, Enterprise, Klienti)
 * running locally behind a Cloudflare quick tunnel.
 *
 * Special case: /docs is served directly from upstream /docs/index.html (Swagger UI),
 * so that the link works and redirects to the real Swagger UI (not the landing index).
 */

const UPSTREAM = "https://queue-contributing-submit-olympics.trycloudflare.com";

function corsHeaders(resp) {
  const h = new Headers(resp.headers);
  h.set("Access-Control-Allow-Origin", "*");
  h.set("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
  h.set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Peppol-Participant-Id, Idempotency-Key");
  h.set("Access-Control-Max-Age", "86400");
  return h;
}

async function fetchUpstream(url, init = {}) {
  const ctrl = new AbortController();
  const t = setTimeout(() => ctrl.abort(), 30000);
  try {
    return await fetch(url, {
      ...init,
      redirect: "manual",
      signal: ctrl.signal,
    });
  } finally {
    clearTimeout(t);
  }
}

function passthroughResponse(resp, corsHeaders) {
  return new Response(resp.body, { status: resp.status, statusText: resp.statusText, headers: corsHeaders });
}

const INDEX_HTML = `<!DOCTYPE html>
<html lang="sk">
<head><meta charset="utf-8"><title>SAPI-SK Proxy</title>
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
 body{font-family:system-ui,sans-serif;background:#0b1020;color:#dbe4ff;margin:0;padding:40px}
 h1{margin:0 0 8px}
 .sub{color:#8fa3d0;margin-bottom:24px}
 a.card{display:block;background:#151c33;border:1px solid #263155;border-radius:10px;padding:16px 20px;margin:10px 0;color:#dbe4ff;text-decoration:none}
 a.card:hover{border-color:#5b7cfa}
 code{background:#1c2743;padding:2px 6px;border-radius:4px}
</style></head>
<body>
<h1>SAPI-SK Proxy</h1>
<div class="sub">Rest API proxy — 4 kategórie Swagger dokumentácie</div>
<a class="card" href="/docs"><b>Swagger UI</b> — interaktívna dokumentácia so 4 kategóriami</a>
<a class="card" href="/swagger/sapi/swagger.json"><b>SAPI</b> — Access Point Interface (openapi)</a>
<a class="card" href="/swagger/connector/swagger.json"><b>Connector</b> — business JSON (openapi)</a>
<a class="card" href="/swagger/enterprise/swagger.json"><b>Enterprise</b> — advanced core (openapi)</a>
<a class="card" href="/swagger/klienti/swagger.json"><b>Klienti</b> — správa klientov (openapi)</a>
<p>API: <code>/sapi/*</code>, <code>/api/v1/*</code> · Health: <code>/health</code></p>
</body></html>`;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders(new Response()) });
    }

    if (url.pathname === "/") {
      return new Response(INDEX_HTML, { headers: { "content-type": "text/html; charset=utf-8" } });
    }

    // /docs and /docs/ → serve Swagger UI HTML, rewriting relative asset refs to /docs/
    if (url.pathname === "/docs" || url.pathname === "/docs/") {
      const upstream = await fetchUpstream(`${UPSTREAM}/docs/index.html`, { method: "GET" }).catch(() => null);
      if (upstream && upstream.status === 200) {
        let body = await upstream.text();
        body = body
          .replace(/href="\.\//g, 'href="/docs/')
          .replace(/src="\.\//g, 'src="/docs/')
          .replace(/href="index.css"/g, 'href="/docs/index.css"')
          .replace(/href="swagger-ui.css"/g, 'href="/docs/swagger-ui.css"')
          .replace(/src="index.js"/g, 'src="/docs/index.js"')
          .replace(/src="swagger-ui-bundle.js"/g, 'src="/docs/swagger-ui-bundle.js"')
          .replace(/src="swagger-ui-standalone-preset.js"/g, 'src="/docs/swagger-ui-standalone-preset.js"')
          .replace(/href="favicon-32x32.png"/g, 'href="/docs/favicon-32x32.png"')
          .replace(/href="favicon-16x16.png"/g, 'href="/docs/favicon-16x16.png"');
        const h = new Headers(upstream.headers);
        h.set("content-type", "text/html; charset=utf-8");
        return new Response(body, { status: 200, headers: h });
      }
      return new Response(INDEX_HTML, { headers: { "content-type": "text/html; charset=utf-8" } });
    }

    // Swagger UI assets: fetch via absolute tunnel URL (relative resolution from
    // sapi.patjo.uk fails intermittently through the tunnel connector).
    if (url.pathname.startsWith("/docs/")) {
      const asset = await fetchUpstream(`${UPSTREAM}${url.pathname}${url.search}`, { method: "GET" }).catch(() => null);
      if (asset && asset.status === 200) {
        return passthroughResponse(asset, corsHeaders(asset));
      }
      return passthroughResponse(asset || new Response("not found", { status: 404 }), corsHeaders(asset || new Response()));
    }


    // /swagger/*, /api/*, /sapi/*, /health …: forward to tunnel, following redirects there.
    let up = `${UPSTREAM}${url.pathname}${url.search}`;
    for (let i = 0; i < 5; i++) {
      const upstream = await fetchUpstream(up, {
        method: request.method,
        headers: request.headers,
        body: ["GET", "HEAD"].includes(request.method) ? undefined : request.body,
      }).catch(() => null);
      if (!upstream) {
        return new Response(JSON.stringify({ error: "upstream_unavailable", message: "tunnel or .NET server unreachable", upstream: UPSTREAM }), { status: 502, headers: { "content-type": "application/json" } });
      }
      if (upstream.status >= 300 && upstream.status < 400) {
        const loc = upstream.headers.get("location");
        if (!loc) return passthroughResponse(upstream, corsHeaders(upstream));
        // resolve redirect location against tunnel, not public host
        up = new URL(loc, up).href;
        if (!up.startsWith(UPSTREAM)) up = UPSTREAM + new URL(loc).pathname + new URL(loc).search;
        request = new Request(up, { method: "GET", headers: request.headers });
        continue;
      }
      return passthroughResponse(upstream, corsHeaders(upstream));
    }
    return passthroughResponse(await fetchUpstream(up, { method: "GET", headers: request.headers }), corsHeaders(await fetchUpstream(up, { method: "GET", headers: request.headers })));
  },
};
