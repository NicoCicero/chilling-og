export async function POST(req) {
  const base = process.env.BACKEND_URL || "http://backend:8080";

  const r = await fetch(`${base}/admin/import`, {
    method: "POST",
    headers: {
      "content-type": req.headers.get("content-type") || "application/json",
      "x-admin-key": req.headers.get("x-admin-key") || "",
    },
    body: await req.text(),
  });

  const data = await r.text();

  return new Response(data, {
    status: r.status,
    headers: { "content-type": r.headers.get("content-type") || "application/json" },
  });
}