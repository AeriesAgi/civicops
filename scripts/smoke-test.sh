#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${1:-http://localhost:5000}"
failures=0
check_get() {
  local path="$1" expected="${2:-200}"
  local code
  code=$(curl -ksS -o /tmp/civicops-smoke.out -w '%{http_code}' "$BASE_URL$path" || true)
  if [[ "$code" == "$expected" || ("$expected" == "any2xx3xx" && "$code" =~ ^[23]) ]]; then
    echo "PASS GET $path -> $code"
  else
    echo "FAIL GET $path -> $code (expected $expected)" >&2
    failures=$((failures+1))
  fi
}

check_get "/"
check_get "/Home/DemoTour"
check_get "/Home/Report"
check_get "/Home/Mobile"
check_get "/app"
check_get "/app/incident/CIV-2026-0001"
check_get "/app/area/Chatsworth/thread"
check_get "/Home/Lookup"
check_get "/Home/Status?reference=CIV-2026-0001"
check_get "/Home/Status?reference=CIV-2026-0005"
check_get "/Home/Status?reference=CIV-2026-0033"
check_get "/Home/Agent"
check_get "/Home/Dashboard" "any2xx3xx"
check_get "/Home/Alerts"
check_get "/Home/Weather"
check_get "/Home/Connectors"
check_get "/Demo/WhatsAppSimulator"
check_get "/Demo/VoiceNoteSimulator"
check_get "/api/connectors/gemini/test"
code=$(curl -ksS -o /tmp/civicops-wa.out -w '%{http_code}' "$BASE_URL/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=invalid&hub.challenge=ok" || true)
if [[ "$code" == "403" || "$code" == "401" ]]; then echo "PASS WhatsApp invalid token rejected -> $code"; else echo "FAIL WhatsApp invalid token -> $code" >&2; failures=$((failures+1)); fi
exit "$failures"
