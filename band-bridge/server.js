// ---------------------------------------------------------------------------
// CivicOps Command — Band Bridge (Node sidecar)
//
// This service is the *real* Band client. The five CivicOps agents coordinate
// through the in-process Band broker on the C# side (so the demo always runs)
// and, when Band:Mode=Live, relay every message here. This sidecar republishes
// each message into a real Band room using the official Band SDK
// (`@band-ai/sdk` + `@thenvoi/rest-client`) — so the same multi-agent transcript
// lands in a hosted Band workspace that judges can open.
//
// Identity model:
//   • Set THENVOI_API_KEY for a single Band identity that creates the room and
//     posts every message (each line is prefixed with the CivicOps agent name).
//   • OR set THENVOI_AGENT_KEYS (JSON map of civic agentId -> Band API key) to
//     have each of the five agents post under its OWN Band identity — true
//     multi-agent collaboration in one shared Band room. Any agent without its
//     own key falls back to the primary identity.
//
// It degrades gracefully: with no key, or if the SDK can't load, it runs in
// "stub" mode and logs what it *would* publish, so the bridge never blocks the
// workflow. The C# broker stays the source of truth either way.
//
//   npm install
//   THENVOI_API_KEY=...  npm start
// ---------------------------------------------------------------------------
import express from "express";

const PORT = process.env.PORT || 8787;
const REST_URL = process.env.THENVOI_REST_URL || "https://app.thenvoi.com";
const WORKSPACE = process.env.BAND_WORKSPACE || "civicops-command";
const PRIMARY_KEY = process.env.THENVOI_API_KEY || process.env.BAND_API_KEY || "";

// Optional per-agent Band keys → true multi-identity. JSON: {"agent.intake":"key",...}
let AGENT_KEYS = {};
try {
  if (process.env.THENVOI_AGENT_KEYS) AGENT_KEYS = JSON.parse(process.env.THENVOI_AGENT_KEYS);
} catch (err) {
  console.warn(`[band-bridge] THENVOI_AGENT_KEYS is not valid JSON (${err.message}) — ignoring.`);
}

let ThenvoiClient = null;     // from @thenvoi/rest-client
let FernRestAdapter = null;   // from @band-ai/sdk/rest
let mode = "stub";            // "live" once the SDK + a key are wired

const restByKey = new Map();      // apiKey  -> FernRestAdapter
const identityByAgent = new Map();// agentId -> resolved Band identity (best-effort)
const chatByRoom = new Map();     // C# roomId -> { chatId, participants:Set }

// --- Bring the real Band SDK online ----------------------------------------
async function initBand() {
  if (!PRIMARY_KEY && Object.keys(AGENT_KEYS).length === 0) {
    console.warn("[band-bridge] No THENVOI_API_KEY / THENVOI_AGENT_KEYS set → STUB mode (messages logged only).");
    return;
  }
  try {
    const restClient = await import("@thenvoi/rest-client");
    const bandRest = await import("@band-ai/sdk/rest");
    ThenvoiClient = restClient.ThenvoiClient;
    FernRestAdapter = bandRest.FernRestAdapter;
    if (!ThenvoiClient || !FernRestAdapter) throw new Error("SDK did not export ThenvoiClient / FernRestAdapter");
    mode = "live";
    console.log(`[band-bridge] Band SDK online → ${REST_URL} (workspace '${WORKSPACE}', ${Object.keys(AGENT_KEYS).length} per-agent identities).`);
  } catch (err) {
    console.warn(`[band-bridge] Could not load Band SDK (${err.message}) → STUB mode.`);
    ThenvoiClient = FernRestAdapter = null;
    mode = "stub";
  }
}

// A REST client bound to a specific API key (one Band identity).
function restForKey(apiKey) {
  if (!apiKey || mode !== "live") return null;
  if (!restByKey.has(apiKey)) {
    restByKey.set(apiKey, new FernRestAdapter(new ThenvoiClient({ apiKey, baseUrl: REST_URL })));
  }
  return restByKey.get(apiKey);
}

// Resolve the REST client for a given CivicOps agent: its own identity if it has
// a dedicated key, otherwise the shared primary identity.
function restForAgent(agentId) {
  const ownKey = AGENT_KEYS[agentId];
  return restForKey(ownKey) || restForKey(PRIMARY_KEY);
}

const usingOwnIdentity = (agentId) => Boolean(AGENT_KEYS[agentId]);

// Open (once) a real Band room for a CivicOps incident room and remember the map.
async function ensureChat(roomId) {
  if (chatByRoom.has(roomId)) return chatByRoom.get(roomId);

  const primary = restForKey(PRIMARY_KEY) || restForKey(Object.values(AGENT_KEYS)[0]);
  let chatId = roomId;
  if (primary) {
    try {
      const chat = await primary.createChat();
      if (chat?.id) chatId = chat.id;
    } catch (err) {
      console.warn(`[band-bridge] createChat for ${roomId} failed (${err.message}); using room id as chat id.`);
    }
  }
  const entry = { chatId, participants: new Set() };
  chatByRoom.set(roomId, entry);
  return entry;
}

// Best-effort: add an agent (by its own identity) as a participant of the chat.
async function ensureParticipant(entry, agentId) {
  if (!usingOwnIdentity(agentId) || entry.participants.has(agentId)) return;
  entry.participants.add(agentId);
  const owner = restForKey(PRIMARY_KEY);
  const self = restForKey(AGENT_KEYS[agentId]);
  if (!owner || !self) return;
  try {
    let identity = identityByAgent.get(agentId);
    if (!identity) {
      identity = await self.getAgentMe();
      identityByAgent.set(agentId, identity);
    }
    if (identity?.id) {
      await owner.addChatParticipant(entry.chatId, { participantId: identity.id, role: "agent" });
    }
  } catch (err) {
    console.warn(`[band-bridge] addChatParticipant(${agentId}) skipped: ${err.message}`);
  }
}

// Publish one CivicOps message into the real Band room.
async function publish(roomId, msg) {
  const entry = await ensureChat(roomId);
  const rest = restForAgent(msg.agentId);

  if (!rest) {
    console.log(`[band-bridge:STUB] room=${roomId} ${msg.agentName} → ${msg.type}: ${String(msg.text).slice(0, 120)}`);
    return "stub";
  }

  await ensureParticipant(entry, msg.agentId);

  // When the agent posts under its own Band identity the name is already carried
  // by that identity; otherwise prefix it so the shared transcript stays legible.
  const content = usingOwnIdentity(msg.agentId)
    ? String(msg.text ?? "")
    : `[${msg.agentName}] ${String(msg.text ?? "")}`;

  await rest.createChatMessage(entry.chatId, {
    content,
    // Domain "kind" travels in metadata, not platform messageType (which the
    // platform validates against its own enum).
    metadata: {
      civicKind: msg.type,
      civicAgent: msg.agentName,
      civicAgentId: msg.agentId,
      civicRole: msg.role,
      handoffTo: msg.handoffTo || null,
      incidentRoom: roomId,
      sentAt: msg.sentAt || new Date().toISOString(),
      ...(msg.data && typeof msg.data === "object" ? { data: msg.data } : {}),
    },
    mentions: [],
  });
  return "live";
}

// --- HTTP surface the C# BandHttpGateway calls -----------------------------
const app = express();
app.use(express.json({ limit: "1mb" }));

app.get("/health", (_req, res) =>
  res.json({ ok: true, mode, workspace: WORKSPACE, restUrl: REST_URL, perAgentIdentities: Object.keys(AGENT_KEYS).length }));

app.post("/agents", (req, res) => {
  const { id } = req.body || {};
  if (!id) return res.status(400).json({ error: "id required" });
  res.json({ ok: true, mode, ownIdentity: usingOwnIdentity(id) });
});

app.post("/rooms/:roomId/messages", async (req, res) => {
  try {
    const delivered = await publish(req.params.roomId, req.body || {});
    res.json({ ok: true, delivered });
  } catch (err) {
    // Never fail the C# workflow because of a live-relay hiccup.
    console.error("[band-bridge] publish error:", err.message);
    res.json({ ok: true, delivered: "error-soft", error: err.message });
  }
});

await initBand();
app.listen(PORT, () => {
  console.log(`[band-bridge] listening on http://localhost:${PORT} (mode=${mode}, workspace=${WORKSPACE})`);
});
