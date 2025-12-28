"use client";

export const dynamic = "force-dynamic";
export const fetchCache = "force-no-store";


import { useState } from "react";

export default function AdminPage() {
  const [adminKey, setAdminKey] = useState("");
  const [season, setSeason] = useState("2025-W52");
  const [file, setFile] = useState(null);
  const [msg, setMsg] = useState("");

  async function upload() {
    setMsg("");
    if (!adminKey || !season || !file) {
      setMsg("Falta Admin Key, season o archivo.");
      return;
    }
    try {
      const csv = await file.text();
      const res = await fetch("/admin/import", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "x-admin-key": adminKey,
        },
        body: JSON.stringify({ season, csv }),
      });

      const json = await res.json().catch(() => ({}));

      if (!res.ok) {
        setMsg(json?.error || "Error al importar el CSV.");
        return;
      }

      setMsg(`Importado ✅ (${json.imported || 0} usuarios)`);
    } catch (error) {
      setMsg("Error al importar el CSV.");
    }
  }

  return (
    <div>
      <div className="smoke" />
      <div className="container">
        <div className="header">
          <div className="brand">
            <div className="logo">🛠️</div>
            <div>
              <div className="title">CHILLING.OG — Admin</div>
              <div className="subtitle">Subí un CSV para actualizar el leaderboard</div>
            </div>
          </div>
          <div className="pills">
            <a className="pill" href="/" style={{ color: "rgba(255,255,255,.9)" }}>
              Volver
            </a>
          </div>
        </div>

        <div className="glass card">
          <div style={{ display: "grid", gap: 10 }}>
            <div>
              <div style={{ fontWeight: 900, marginBottom: 6 }}>Admin Key</div>
              <input
                className="input"
                style={{ width: "100%" }}
                value={adminKey}
                onChange={(e) => setAdminKey(e.target.value)}
                placeholder="Admin__Key"
              />
            </div>

            <div>
              <div style={{ fontWeight: 900, marginBottom: 6 }}>Season</div>
              <input
                className="input"
                style={{ width: "100%" }}
                value={season}
                onChange={(e) => setSeason(e.target.value)}
                placeholder="2025-W52"
              />
            </div>

            <div>
              <div style={{ fontWeight: 900, marginBottom: 6 }}>CSV</div>
              <input
                className="input"
                style={{ width: "100%" }}
                type="file"
                accept=".csv"
                onChange={(e) => setFile(e.target.files?.[0] || null)}
              />
            </div>

            <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
              <button className="button" type="button" onClick={upload}>
                Subir
              </button>
              {msg ? <span className="muted">{msg}</span> : null}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
