#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

for tool in dotnet python3 node npm curl; do
  command -v "$tool" >/dev/null || { echo "Outil manquant : $tool" >&2; exit 1; }
done

VENV="$ROOT/echos/.venv"
if [[ ! -x "$VENV/bin/python" ]]; then
  echo "Création de l'environnement Python ECHOS…"
  python3 -m venv "$VENV"
fi
if ! "$VENV/bin/python" -m pip --version >/dev/null 2>&1; then
  echo "Initialisation de pip dans l'environnement Python ECHOS…"
  if ! "$VENV/bin/python" -m ensurepip --upgrade >/dev/null 2>&1; then
    python3 -m pip --python "$VENV" install pip >/dev/null
  fi
fi
if ! "$VENV/bin/python" -c 'import fastapi, uvicorn, websockets, pyarrow' >/dev/null 2>&1; then
  echo "Installation des dépendances ECHOS…"
  "$VENV/bin/python" -m pip install --upgrade pip
  "$VENV/bin/python" -m pip install -r echos/requirements-dev.txt
fi
if [[ ! -x echos/echos-ui/node_modules/.bin/vite ]]; then
  echo "Installation des dépendances de l'interface…"
  (cd echos/echos-ui && npm ci)
fi

echo "Compilation Release de SYNE…"
dotnet build syne/Syne.sln --configuration Release
SYNE_BIN="$ROOT/syne/Simulation.Console/bin/Release/net10.0/Simulation.Console"

mkdir -p echos/data
export ECHOS_ANALYTICS_DB="$ROOT/echos/data/livex-analytics.sqlite"
export PYTHONPATH="$ROOT/echos${PYTHONPATH:+:$PYTHONPATH}"
READY_DIR="$(mktemp -d "${TMPDIR:-/tmp}/livex-dev-stack.XXXXXX")"
INGEST_STARTED_FILE="$READY_DIR/ingest-started"
PIDS=()

cleanup() {
  local pid
  for pid in "${PIDS[@]}"; do
    kill -- "-$pid" 2>/dev/null || kill "$pid" 2>/dev/null || true
  done
  for pid in "${PIDS[@]}"; do
    wait "$pid" 2>/dev/null || true
  done
  rm -rf "$READY_DIR"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

wait_for_url() {
  local url="$1"
  local label="$2"
  local pid="${3:-}"
  for _ in $(seq 1 60); do
    if curl --silent --fail "$url" >/dev/null; then
      echo "$label prêt : $url"
      return 0
    fi
    if [[ -n "$pid" ]] && ! kill -0 "$pid" 2>/dev/null; then
      echo "$label s'est arrêté avant de devenir disponible ($url)." >&2
      return 1
    fi
    sleep 1
  done
  echo "Délai dépassé en attendant $label ($url)." >&2
  return 1
}

setsid "$VENV/bin/python" -m uvicorn echos.api.app:app --app-dir echos \
  --host 127.0.0.1 --port 5000 &
PIDS+=("$!")
wait_for_url "http://127.0.0.1:5000/health" "API ECHOS" "${PIDS[-1]}"

(
  cd echos/echos-ui
  exec setsid npm run dev -- --host 127.0.0.1
) &
PIDS+=("$!")
wait_for_url "http://127.0.0.1:5173/" "Interface ECHOS" "${PIDS[-1]}"

setsid "$SYNE_BIN" \
  --serve &
CONTROL_PID="$!"
PIDS+=("$CONTROL_PID")
wait_for_url "http://127.0.0.1:5181/api/control/status" \
  "Serveur SYNE (contrôle + WebSocket)" "$CONTROL_PID"

LIVEX_WS_STARTED_FILE="$INGEST_STARTED_FILE" \
  setsid "$VENV/bin/python" -m echos.dev_ingest &
INGEST_PID="$!"
PIDS+=("$INGEST_PID")

echo "Ingestion ECHOS connectée au WebSocket SYNE, en attente du prochain Start…"
for _ in $(seq 1 30); do
  [[ -f "$INGEST_STARTED_FILE" ]] && break
  if ! kill -0 "$INGEST_PID" 2>/dev/null; then
    echo "Le consommateur ECHOS s'est arrêté au démarrage." >&2
    exit 1
  fi
  sleep 1
done
if [[ ! -f "$INGEST_STARTED_FILE" ]]; then
  echo "Délai dépassé au démarrage du consommateur ECHOS." >&2
  exit 1
fi

echo
echo "Interface : http://127.0.0.1:5173"
echo "API ECHOS : http://127.0.0.1:5000/docs"
echo "Contrôle SYNE : http://127.0.0.1:5181/api/control/status"
echo "SYNE est en attente d'une commande Start de l'interface."
echo "Arrêtez l'ensemble avec Ctrl+C."
wait "$CONTROL_PID"
