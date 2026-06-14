# syntax=docker/dockerfile:1

# CivicOps Command — Band Bridge sidecar (Node).
# Publishes the five-agent dispatch transcript into a real Band room via the
# official Band SDK. Runs in stub mode with no key, so the image is safe to run
# unconfigured; set THENVOI_API_KEY (and/or THENVOI_AGENT_KEYS) to go live.
FROM node:22-slim
WORKDIR /app

COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund

COPY server.js ./

ENV PORT=8787
EXPOSE 8787
CMD ["node", "server.js"]
