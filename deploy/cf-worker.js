const UPSTREAM = "https://remark-close-couples-moments.trycloudflare.com";

addEventListener("fetch", (event) => {
  event.respondWith(handle(event.request));
});

async function handle(request) {
  const url = new URL(request.url);
  let target = UPSTREAM + url.pathname + url.search;
  try {
    const headers = new Headers(request.headers);
    headers.delete("host");
    const init = {
      method: request.method,
      headers,
      body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
      redirect: "follow",
    };
    const resp = await fetch(target, init, { cf: { cacheTtl: 0 } });
    return new Response(resp.body, {
      status: resp.status,
      statusText: resp.statusText,
      headers: resp.headers,
    });
  } catch (e) {
    return new Response(JSON.stringify({ error: "upstream_unreachable", detail: String(e) }), {
      status: 502,
      headers: { "Content-Type": "application/json" },
    });
  }
}
