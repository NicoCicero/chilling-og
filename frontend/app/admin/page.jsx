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

      // DEBUG: para confirmar que llegó hasta acá
      setMsg("Subiendo...");

      const res = await fetch("/api/admin/import", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "x-admin-key": adminKey,
        },
        body: JSON.stringify({ season, csv }),
        cache: "no-store",
      });

      // Leemos como texto primero para NO romper si no es JSON
      const text = await res.text();

      let json = {};
      try {
        json = JSON.parse(text);
      } catch {
        // no era JSON, queda vacío
      }

      if (!res.ok) {
        // Mostramos el error real si viene en json.error
        // o el texto crudo si el backend devolvió otra cosa
        setMsg(json?.error || text || "Error al importar el CSV.");
        return;
      }

      setMsg(`Importado ✅ (${json.imported ?? 0} usuarios)`);
    } catch (error) {
      setMsg(`Error al importar el CSV: ${String(error?.message || error)}`);
    }
  }

  return (
    <div>
      {/* IMPORTANTE: esto evita que la capa "smoke" tape los clicks */}
      <div className="smoke" style={{ pointerEvents: "none" }} />

      <div className="container" style={{ position: "relative", zIndex: 1 }}>
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
              {file ? (
                <div className="muted" style={{ marginTop: 6 }}>
                  Archivo seleccionado: <b>{file.name}</b>
                </div>
              ) : null}
            </div>

            <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
              <button
                className="button"
                type="button"
                onClick={() => {
                  upload();
                }}
              >
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
