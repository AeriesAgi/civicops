#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# CivicOps Command — Band multi-agent dispatch end-to-end demo
#
# Drives the full workflow through the Band coordination layer:
#   raw report -> IncidentIntakeAgent -> DispatchCoordinatorAgent
#   -> human confirm -> ResponseMonitorAgent -> resolved + summary
#
# Usage:
#   ./scripts/band-demo.sh                 # structure-fire, auto-confirm
#   BASE=http://localhost:5000 ./scripts/band-demo.sh armed-robbery
#   AUTO=false ./scripts/band-demo.sh structure-fire   # confirm in the UI yourself
# ---------------------------------------------------------------------------
set -euo pipefail

BASE="${BASE:-http://localhost:5000}"
SCENARIO="${1:-structure-fire}"
AUTO="${AUTO:-true}"

echo "▶ Launching Band scenario '${SCENARIO}' (autoConfirm=${AUTO}) at ${BASE}"

RESP=$(curl -s -X POST "${BASE}/api/band/simulate" \
  -H 'Content-Type: application/json' \
  -d "{\"scenario\":\"${SCENARIO}\",\"autoConfirm\":${AUTO}}")

ROOM=$(echo "$RESP" | sed -n 's/.*"roomId":"\([^"]*\)".*/\1/p')
if [ -z "$ROOM" ]; then echo "✗ Failed to start. Response: $RESP"; exit 1; fi

echo "✔ Band room opened: ${ROOM}"
echo "  Live viewer: ${BASE}/Band/Room/${ROOM}"
echo

LAST=0
echo "▶ Streaming Band room transcript (Ctrl-C to stop)…"
for _ in $(seq 1 40); do
  MSGS=$(curl -s "${BASE}/api/band/rooms/${ROOM}/since/${LAST}")
  # naive pretty-print of new messages
  echo "$MSGS" | grep -o '"senderName":"[^"]*","senderKind":"[^"]*"[^}]*"kind":"[^"]*","text":"[^"]*"' \
    | sed -e 's/"senderName":"/[/; s/","senderKind":"[^"]*"[^}]*"kind":"/ · /; s/","text":"/] /; s/\\n/ /g; s/"$//' || true
  NEW=$(echo "$MSGS" | grep -o '"sequence":[0-9]*' | sed 's/"sequence"://' | sort -n | tail -1 || true)
  if [ -n "${NEW:-}" ]; then LAST=$NEW; fi
  if echo "$MSGS" | grep -q '"kind":"Summary"'; then echo; echo "✔ Incident resolved and Band room summarised."; break; fi
  sleep 2
done

echo
echo "Open the full audit trail in the browser: ${BASE}/Band/Room/${ROOM}"
