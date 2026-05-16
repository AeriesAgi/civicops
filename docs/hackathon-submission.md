# CivicOps Hackathon Submission

CivicOps is a public-facing civic AI platform for mobile/PWA and web reporting, AI-assisted routing, reference tracking, public alerts and connector readiness.

## Product story
Residents submit messy reports through the mobile/PWA app or web portal. CivicOps validates and structures them, then Gemini or deterministic fallback classifies, prioritizes and routes them to department queues. Residents receive reference numbers and status tracking. Area alerts and weather/context improve civic resilience.

## Gemini
Gemini is the AI agent layer. It is event/action-triggered only: report submission, voice-note transcript analysis, WhatsApp inbound processing, or explicit staff/judge agent buttons. It does not run on startup, page load, dashboards, connector pages, mobile app opening, background timers or smoke tests.

Model plan: premium judge summary starts with `gemini-2.5-flash`; routine classification uses `gemini-3.1-flash-lite`; fallback chain is `gemini-3.1-flash-lite`, `gemini-2.5-flash-lite`, `gemini-2.0-flash-lite`, `gemini-2.0-flash`; deterministic fallback remains active.

## WhatsApp
WhatsApp Cloud API is connector-ready for sandbox/live-test and future production pilots. Production use requires WhatsApp Business setup, opt-in/templates and billing. CivicOps does not depend on WhatsApp.

## Safety and honesty
CivicOps uses synthetic scenario data, makes no official municipal partnership claim, is human-in-the-loop, and is not an emergency services replacement.
