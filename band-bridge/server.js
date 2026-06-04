// ---------------------------------------------------------------------------
// CivicOps Command — Band Bridge (Node sidecar)
//
// This service is the *real* Band client. The C# agents coordinate through the
// in-process Band broker (so the demo always runs) and, when Band:Mode=Live,
// relay every message here. This sidecar republishes each message to the real
// Band platform using the official `@band-sdk/core` SDK — so the same
// multi-agent transcript lands in a hosted Band room.
//
// It degrades gracefully: if @band-sdk/core isn't installed or no API key is
// set, it runs in "stub" mode and logs what it *would* publish, so the bridge
// itself never blocks the workflow.
//
//   npm install        # pulls @band-sdk/core + express
//   BAND_API_KEY=...  npm start
// ---------------------------------------------------------------------------
import express from "express";

const PORT = process.env.PORT || 8787;
const API_KEY = process.env.BAND_API_KEY || "";
const WORKSPACE = process.env.BAND_WORKSPACE || "civicops-command";

let band = null;       // the live Band client (if available)
let mode = "stub";     // "live" once the SDK + key are wired
const rooms = new Map();   // roomId -> band room handle
const agents = new Map();  // agentId -> band agent/identity handle

// --- Try to bring the real Band SDK online ---------------------------------
async function initBand() {
  if (!API_KEY) {
    console.warn("[band-bridge] No BAND_API_KEY set → running in STUB mode (messages logged only).");
    return;
  }
  try {
    const sdk = await import("@band-sdk/core");
    const Band = sdk.Band || sdk.default || sdk.Client;
    if (!Band) throw new Error("@band-sdk/core did not export a known client constructor");

    // The SDK surface is initialised here; we keep calls defensive because the
    // exact shape may evolve. connect() establishes the workspace session.
    band = new Band({ apiKey: API_KEY, workspace: WORKSPACE });
    if (typeof band.connect === "function") await band.connect();

    mode = "live";
    console.log(`[band-bridge] Connected to Band workspace '${WORKSPACE}' via @band-sdk/core.`);
  } catch (err) {
    console.warn(`[band-bridge] Could not initialise @band-sdk/core (${err.message}) → STUB mode.`);
    band = null;
    mode = "stub";
  }
}

// Resolve (creating if needed) a Band agent identity.
async function ensureAgent(id, name, role) {
  if (agents.has(id)) return agents.get(id);
  let handle = { id, name, role };
  if (band) {
    try {
      // Likely SDK shapes: band.agent({...}) or band.createAgent({...}).
      const factory = band.agent || band.createAgent || band.registerAgent;
      if (typeof factory === "function") {
        handle = await factory.call(band, { id, name, role });
      }
    } catch (err) {
      console.warn(`[band-bridge] ensureAgent('${id}') fell back: ${err.message}`);
    }
  }
  agents.set(id, handle);
  return handle;
}

// Resolve (creating if needed) a per-incident Band room.
async function ensureRoom(roomId) {
  if (rooms.has(roomId)) return rooms.get(roomId);
  let handle = { id: roomId };
  if (band) {
    try {
      const factory = band.room || band.joinRoom || band.createRoom || band.openRoom;
      if (typeof factory === "function") {
        handle = await factory.call(band, roomId);
      }
    } catch (err) {
      console.warn(`[band-bridge] ensureRoom('${roomId}') fell back: ${err.message}`);
    }
  }
  rooms.set(roomId, handle);
  return handle;
}

// Publish one message into a Band room through the SDK.
async function publish(roomId, msg) {
  await ensureAgent(msg.agentId, msg.agentName, msg.role);
  const room = await ensureRoom(roomId);

  const payload = {
    agent: msg.agentId,
    agentName: msg.agentName,
    role: msg.role,
    type: msg.type,
    text: msg.text,
    handoffTo: msg.handoffTo || null,
    data: msg.data || {},
    sentAt: msg.sentAt || new Date().toISOString(),
  };

  if (band && room) {
    // Likely SDK shapes: room.post(payload) / room.send(payload) / room.message(payload).
    const send = room.post || room.send || room.message || room.publish;
    if (typeof send === "function") {
      await send.call(room, payload);
      return "live";
    }
  }

  console.log(`[band-bridge:STUB] room=${roomId} ${msg.agentName} → ${msg.type}: ${String(msg.text).slice(0, 120)}`);
  return "stub";
}

// --- HTTP surface the C# BandHttpGateway calls -----------------------------
const app = express();
app.use(express.json({ limit: "1mb" }));

app.get("/health", (_req, res) => res.json({ ok: true, mode, workspace: WORKSPACE }));

app.post("/agents", async (req, res) => {
  const { id, name, role } = req.body || {};
  if (!id) return res.status(400).json({ error: "id required" });
  await ensureAgent(id, name, role);
  res.json({ ok: true, mode });
});

app.post("/rooms/:roomId/messages", async (req, res) => {
  try {
    const delivered = await publish(req.params.roomId, req.body || {});
    res.json({ ok: true, delivered });
  } catch (err) {
    console.error("[band-bridge] publish error:", err);
    res.status(500).json({ ok: false, error: err.message });
  }
});

await initBand();
app.listen(PORT, () => {
  console.log(`[band-bridge] listening on http://localhost:${PORT} (mode=${mode}, workspace=${WORKSPACE})`);
});
