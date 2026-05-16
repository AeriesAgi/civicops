#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${1:-http://localhost:5000}"
failures=0
post_json() {
  local path="$1" payload="$2" expected="${3:-200}"
  local code
  code=$(curl -ksS -o /tmp/civicops-api.out -w '%{http_code}' -H 'Content-Type: application/json' -d "$payload" "$BASE_URL$path" || true)
  if [[ "$code" == "$expected" ]]; then echo "PASS POST $path -> $code"; else echo "FAIL POST $path -> $code" >&2; cat /tmp/civicops-api.out >&2 || true; failures=$((failures+1)); fi
}
get_json() {
  local path="$1" expected="${2:-200}"
  local code
  code=$(curl -ksS -o /tmp/civicops-api.out -w '%{http_code}' "$BASE_URL$path" || true)
  if [[ "$code" == "$expected" ]]; then echo "PASS GET $path -> $code"; else echo "FAIL GET $path -> $code" >&2; cat /tmp/civicops-api.out >&2 || true; failures=$((failures+1)); fi
}

get_json "/api/connectors/gemini/test"
get_json "/api/agent/scenarios"
post_json "/api/agent/run" '{"scenario":"latest-report"}'
post_json "/api/agent/generate-response" '{"scenario":"citizen-response"}'
post_json "/api/agent/recommend-alert" '{"scenario":"area-alert"}'
post_json "/api/reports" '{"sourceChannel":2,"description":"Blocked storm drain flooding the main road near the clinic","suburb":"Chatsworth","ward":"Ward 68","contactName":"API Smoke Test","locationNotes":"Clinic entrance"}'
code=$(curl -ksS -o /tmp/civicops-wa.out -w '%{http_code}' "$BASE_URL/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=invalid&hub.challenge=ok" || true)
if [[ "$code" == "403" || "$code" == "401" ]]; then echo "PASS WhatsApp invalid token rejected -> $code"; else echo "FAIL WhatsApp invalid token -> $code" >&2; failures=$((failures+1)); fi
exit "$failures"
