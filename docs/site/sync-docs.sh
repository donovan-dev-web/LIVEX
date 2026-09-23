#!/usr/bin/env bash
# Synchronise les « docs clés » du monorepo vers articles/ (source unique
# conservée là d'origine ; copie régénérée à chaque build DocFX).
# Usage : bash docs/site/sync-docs.sh  (depuis la racine du dépôt)
set -euo pipefail

SITE="docs/site"
ART="$SITE/articles"

TRANSVERSE=(README.md VISION.md ARCHITECTURE.md ROADMAP.md CHANGELOG.md \
  GLOSSARY.md INSTALLATION.md VERSIONING.md FAQ.md \
  CONTRIBUTING.md CODE_OF_CONDUCT.md SECURITY.md LICENSE)
SYNE=(README.md VISION.md ARCHITECTURE.md API_CONTRACTS.md SYSTEMS_SPEC.md \
  CONFIGURATION.md TESTING.md DETERMINISM.md)
ECHOS=(README.md VISION.md ARCHITECTURE.md ROADMAP.md TESTING.md \
  METRICS_SPEC.md API_REST.md FRONTEND_VISION.md UI_DESIGN.md)
PRISM=(README.md ARCHITECTURE.md RENDERING_SPEC.md SCENE_SPEC.md \
  TRANSPORT_API.md ASSETS_CONVENTIONS.md)

mkdir -p "$ART/transverse" "$ART/syne" "$ART/echos" "$ART/prism"
rm -f "$ART"/*/*

for f in "${TRANSVERSE[@]}"; do
  [ -f "$f" ] && cp "$f" "$ART/transverse/$f"
done
for f in "${SYNE[@]}"; do
  [ -f "docs/docs-syne/$f" ] && cp "docs/docs-syne/$f" "$ART/syne/$f"
done
for f in "${ECHOS[@]}"; do
  [ -f "docs/docs-echos/$f" ] && cp "docs/docs-echos/$f" "$ART/echos/$f"
done
for f in "${PRISM[@]}"; do
  [ -f "docs/docs-prism/$f" ] && cp "docs/docs-prism/$f" "$ART/prism/$f"
done

echo "docs clés synchronisées dans $ART"