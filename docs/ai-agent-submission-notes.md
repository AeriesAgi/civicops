# AI Agent Submission Notes

The CivicOps AI Agent Command Centre (`/Home/Agent`) exposes action-triggered backend tasks:
- Analyze latest resident report
- Analyze WhatsApp sandbox report
- Analyze voice-note transcript
- Run live Gemini health test
- Generate citizen response
- Generate department brief
- Recommend area alert
- Generate judge summary

Each button calls the backend once and displays validation, category, department, priority, routing reason, citizen response, alert recommendation, source model/fallback and audit notes.

Gemini diagnostics include enabled/key-present status, primary/routine/fallback models, calls since app start, last action, last model, last result, quota-limited state and fallback-active state. Keys and prompt secrets are not exposed.
