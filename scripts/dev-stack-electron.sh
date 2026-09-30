#!/usr/bin/env bash
set -Eeuo pipefail

# Stack de développement ECHOS en mode **bureau** : le shell Electron remplace
# l'interface web Vite du `dev-stack.sh`. SYNE et l'ingestion sont identiques ;
# seul le mode de présentation de l'UI change :
#
#   dev-stack.sh           → uvicorn + Vite (navigateur) + SYNE + ingestion
#   dev-stack-electron.sh  → SYNE + ingestion + shell Electron (fenêtre ECHOS)
#
# Le shell Electron (echos/echos-desktop, ADR-003) choisit lui-même un port
# libre, lance le backend Python comme enfant, attend `/health` puis ouvre la
# fenêtre — ce script n'a donc ni uvicorn ni port d'API à fixer. Il partage
# avec le shell la base d'analyse (ECHOS_ANALYTICS_DB) pour que l'ingestion
# alimente exactement ce que la fenêtre affiche.
#
# Pré-requis identiques à dev-stack.sh ; en plus, le build UI doit exister
# (il est servi par le backend, pas par Vite) : le script le produit si absent.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

for tool in dotnet python3 node npm; do
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
if [[ ! -d echos/echos-desktop/node_modules/electron ]]; then
  echo "Installation des dépendances du shell Electron…"
  (cd echos/echos-desktop && npm ci)
fi
if [[ ! -f echos/echos-ui/dist/index.html ]]; then
  echo "Build de l'interface (servie par le backend dans le shell)…"
  (cd echos/echos-ui && npm run build)
fi

echo "Compilation Release de SYNE…"
dotnet build syne/Syne.sln --configuration Release
SYNE_BIN="$ROOT/syne/Simulation.Console/bin/Release/net10.0/Simulation.Console"

mkdir -p echos/data
export ECHOS_ANALYTICS_DB="$ROOT/echos/data/livex-analytics.sqlite"
export PYTHONPATH="$ROOT/echos${PYTHONPATH:+:$PYTHONPATH}"

PIDS=()

cleanup() {
  local pid
  # Ordre inverse : d'abord le shell Electron (SIGTERM à son enfant backend,
  # cf. ADR-003), puis ingestion et SYNE — jamais l'inverse, sinon le backend
  # survivrait à la fenêtre.
  for (( idx=${#PIDS[@]}-1 ; idx>=0 ; idx-- )) ; do
    pid="${PIDS[$idx]}"
    kill -- "-$pid" 2>/dev/null || kill "$pid" 2>/dev/null || true
  done
  for pid in "${PIDS[@]}"; do
    wait "$pid" 2>/dev/null || true
  done
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

setsid "$SYNE_BIN" \
  --serve &
CONTROL_PID="$!"
PIDS+=("$CONTROL_PID")
wait_for_url "http://127.0.0.1:5181/api/control/status" \
  "Serveur SYNE (contrôle + WebSocket)" "$CONTROL_PID"

INGEST_STARTED_FILE="${TMPDIR:-/tmp}/livex-ingest-started.$$"
rm -f "$INGEST_STARTED_FILE"
LIVEX_WS_STARTED_FILE="$INGEST_STARTED_FILE" \
  setsid "$VENV/bin/python" -m echos.dev_ingest &
INGEST_PID="$!"
PIDS+=("$INGEST_PID")

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

echo "Ingestion ECHOS connectée au WebSocket SYNE, en attente du prochain Start…"

# Le shell Electron : il gère son backend enfant, le port libre, la fenêtre.
# La base d'analyse est imposée via ECHOS_ANALYTICS_DB (lu par main.js).
#
# Parade sandbox Chromium (dev uniquement) : dans node_modules, chrome-sandbox
# n'est pas setuid root — Chromium abort sans cette option (l'app installée,
# elle, reçoit le setuid via le postinst du paquet). On n'utilise pas la
# variable ELECTRON_DISABLE_SANDBOX qui désactiverait aussi le sandbox GPU.
(
  cd echos/echos-desktop
  exec setsid npx electron . --no-sandbox
) &
ELECTRON_PID="$!"
PIDS+=("$ELECTRON_PID")

echo
echo "Fenêtre ECHOS ouverte (fermez-la pour arrêter toute la pile)."
echo "Contrôle SYNE : http://127.0.0.1:5181/api/control/status"
echo "SYNE est en attente d'une commande Start de l'interface."
echo "Arrêtez l'ensemble avec Ctrl+C ou en fermant la fenêtre."
wait "$ELECTRON_PID"
