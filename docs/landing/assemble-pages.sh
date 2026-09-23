#!/usr/bin/env bash
# assemble-pages.sh — assemble landing SaaS (racine) + DocFX _site sous /docs/
# Usage : bash docs/landing/assemble-pages.sh [output_dir]   (defaut: docs/pages-dist)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LANDING="$ROOT/docs/landing"
SITE="$ROOT/docs/site/_site"
OUT="${1:-$ROOT/docs/pages-dist}"
[[ -d "$SITE" ]] || { echo "!! _site DocFX manquant — lancez le build DocFX d'abord" >&2; exit 1; }
rm -rf "$OUT"; mkdir -p "$OUT/docs"
echo "==> landing → racine   ($LANDING → $OUT)"
cp -R "$LANDING"/. "$OUT/"
echo "==> DocFX _site → /docs/   ($SITE → $OUT/docs)"
cp -R "$SITE"/. "$OUT/docs/"
echo "==> favicon/img cohérence"
echo "    racine:  $( [ -f "$OUT/index.html" ] && echo OK index.html ) $( [ -d "$OUT/assets" ] && echo +assets )"
echo "    docs:    $( [ -f "$OUT/docs/index.html" ] && echo OK docs/index.html )  articles:$(ls "$OUT/docs/articles" 2>/dev/null | tr '\n' ' ')"
echo "==> ASSEMBLAGE terminee — prêt pour GitHub Pages"
