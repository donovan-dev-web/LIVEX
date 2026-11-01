#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
#
# Construit localement la distribution LIVEX et son ZIP public (version Linux).
# Équivalent de build-release.ps1 : mêmes étapes, même sortie dans dist/.
#
# Usage :
#   scripts/release/build-release.sh --version 1.0.0 [--tag livex-v1.0.0]
#       [--rid linux-x64|win-x64] [--profile core|full] [--upload] [--skip-components]
#
# Aucune compilation n'a lieu dans GitHub Actions. Le téléversement passe par gh.

set -euo pipefail

usage() {
    sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

VERSION=""
TAG=""
RID="linux-x64"
PROFILE="core"
UPLOAD=0
SKIP_COMPONENTS=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="${2:?--version attend une valeur}"; shift 2 ;;
        --tag) TAG="${2:?--tag attend une valeur}"; shift 2 ;;
        --rid) RID="${2:?--rid attend une valeur}"; shift 2 ;;
        --profile) PROFILE="${2:?--profile attend une valeur}"; shift 2 ;;
        --upload) UPLOAD=1; shift ;;
        --skip-components) SKIP_COMPONENTS=1; shift ;;
        -h|--help) usage 0 ;;
        *) echo "Option inconnue : $1" >&2; usage 1 ;;
    esac
done

[[ -n "$VERSION" ]] || { echo "--version est obligatoire." >&2; usage 1; }
[[ -n "$TAG" ]] || TAG="livex-v$VERSION"
case "$RID" in win-x64|linux-x64) ;; *) echo "RID invalide : $RID" >&2; exit 1 ;; esac
case "$PROFILE" in core|full) ;; *) echo "Profil invalide : $PROFILE" >&2; exit 1 ;; esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STAGING="$ROOT/dist/staging/$RID"
DIST_INPUTS="$ROOT/dist/inputs"
PYTHON="${PYTHON:-python3}"

step() { printf '\n==> %s\n' "$1"; }

# Copie le contenu d'un dossier source vers une cible, en excluant certains noms.
copy_filtered() {
    local src="$1" dst="$2"; shift 2
    local excludes=("$@")
    rm -rf "$dst"
    mkdir -p "$dst"
    local entry name skip
    shopt -s dotglob nullglob
    for entry in "$src"/*; do
        name="$(basename "$entry")"
        skip=0
        for ex in "${excludes[@]}"; do
            [[ "$name" == "$ex" ]] && { skip=1; break; }
        done
        (( skip )) || cp -R "$entry" "$dst/"
    done
    shopt -u dotglob nullglob
}

publish_dotnet() {
    local project="$1" output="$2"
    rm -rf "$output"
    dotnet publish "$project" -c Release -r "$RID" --self-contained true -o "$output"
}

mkdir -p "$STAGING" "$DIST_INPUTS"

if (( ! SKIP_COMPONENTS )); then
    step "Publication du Launcher"
    publish_dotnet "$ROOT/launcher/Launcher.App/Launcher.App.csproj" "$STAGING/launcher"

    step "Publication de l'installateur"
    publish_dotnet "$ROOT/installer/Setup.App/Setup.App.csproj" "$STAGING/setup"

    step "Publication de SYNE"
    publish_dotnet "$ROOT/syne/Simulation.Console/Simulation.Console.csproj" "$STAGING/syne"
    # Le manifeste de composant accompagne toujours le binaire publié.
    cp -f "$ROOT/syne/component.json" "$STAGING/syne/component.json"

    step "Staging d'ECHOS"
    TARGET="$STAGING/echos"
    copy_filtered "$ROOT/echos" "$TARGET" .venv .pytest_cache __pycache__ .coverage htmlcov
    VENV_PY="$TARGET/.venv/bin/python"
    if [[ ! -x "$VENV_PY" ]]; then
        "$PYTHON" -m venv "$TARGET/.venv"
    fi
    "$VENV_PY" -m pip install --upgrade pip
    "$VENV_PY" -m pip install -r "$TARGET/requirements.txt"

    step "Staging de SYNE-Mock"
    copy_filtered "$ROOT/syne-mock" "$STAGING/syne-mock" node_modules .git test
fi

# Les actifs externes (PRISM, LDK) peuvent être absents d'un poste de build :
# le script les déclare alors « indisponibles » au lieu d'échouer.
step "Vérification des entrées externes"
if ! "$PYTHON" "$ROOT/scripts/release/check_assets.py" --dist-dir "$DIST_INPUTS"; then
    echo "AVERTISSEMENT : des actifs externes sont absents ou non conformes : ils seront marqués indisponibles." >&2
fi

if [[ "$RID" == "win-x64" ]]; then
    SETUP_EXE="$STAGING/setup/livex-setup.exe"
else
    SETUP_EXE="$STAGING/setup/livex-setup"
fi

step "Assemblage de la distribution"
"$PYTHON" "$ROOT/scripts/release/build_distribution.py" \
    --rid "$RID" \
    --profile "$PROFILE" \
    --tag "$TAG" \
    --version "$VERSION" \
    --staging "$STAGING" \
    --dist-inputs "$DIST_INPUTS" \
    --setup-exe "$SETUP_EXE"

FINAL_ZIP="$ROOT/dist/LIVEX-$VERSION-$RID-$PROFILE.zip"
if [[ ! -f "$FINAL_ZIP" ]]; then
    echo "ZIP public introuvable : $FINAL_ZIP" >&2
    exit 1
fi

echo "ZIP public : $FINAL_ZIP"
ls -l "$FINAL_ZIP"

if (( UPLOAD )); then
    step "Téléversement du ZIP public"
    gh release upload "$TAG" "$FINAL_ZIP" --clobber

    if [[ "$PROFILE" == "core" ]]; then
        EXTERNAL="$ROOT/installer/build/external-inputs.json"
        if [[ -f "$EXTERNAL" ]]; then
            mapfile -t FILES < <(
                "$PYTHON" -c 'import json,sys; [print(i["file"]) for i in json.load(open(sys.argv[1]))["inputs"]]' "$EXTERNAL" \
                | while IFS= read -r f; do
                    [[ -f "$DIST_INPUTS/$f" ]] && echo "$DIST_INPUTS/$f"
                  done
            )
            if (( ${#FILES[@]} )); then
                step "Téléversement des actifs externes"
                gh release upload "$TAG" "${FILES[@]}" --clobber
            fi
        fi
    fi
fi
