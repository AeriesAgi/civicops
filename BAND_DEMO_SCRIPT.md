# CivicOps Band Demo Script

## 0:00-0:20 - Problem and intro

"CivicOps is a multi-agent civic and emergency operations platform. Citizen reports arrive through web, WhatsApp-style messages, or voice transcripts, but the real challenge is coordinating the response: intake, triage, routing, public updates and audit need to happen quickly without losing accountability."

Show the home page, then open `/demo/band`.

## 0:20-0:50 - Citizen report and intake

"This is the Band demo console. I am launching the seeded water-main incident: a voice-note style WhatsApp report about a burst pipe at 14 Palmview Road in Phoenix, with flooding, road damage, contact information and urgency."

Select `Burst water main threatening homes`, keep auto-confirm on for a fast recording, and click `Launch in Band`.

## 0:50-1:30 - Band multi-agent workflow

"CivicOps opens one Band room for this incident. The Intake Agent receives the raw report. The Triage Agent extracts location, category, severity, missing information and response type. The Dispatch/Routing Agent reads that structured incident from Band and proposes the correct municipal workflow and available unit."

Point to the live stream, agent names, lifecycle checklist and room members.

## 1:30-2:15 - Dispatch, public status and audit

"The human dispatcher confirmation is recorded in the same room. The Public Status Agent prepares a citizen-friendly WhatsApp/public update. The Audit/Supervisor Agent records dispatch decisions, SLA risk, escalation and final summary. The transcript becomes evidence, not just chat."

Wait for the room to reach dispatch, monitoring, resolution and summary.

## 2:15-2:45 - Impact and scalability

"This pattern scales across water, power, road hazards, fire risk and public safety incidents. Municipal teams get faster routing, citizens get clearer updates, and supervisors get an audit trail for every decision."

Show `/api/integrations/status` if useful to prove configured/fallback partner status without exposing secrets.

## 2:45-3:00 - Close

"CivicOps uses Band as the collaboration layer for the agents. It runs fully in deterministic fallback mode for judging, and can mirror the same transcript to live Band when credentials are supplied through environment variables only."

Close on the resolved Band room summary.
