#!/usr/bin/env bash
# Publishes PRISM host and viewer as self-contained binaries.
#
# Usage: ./scripts/publish.sh [runtime-identifier] [output-directory]
#   runtime-identifier: linux-x64 (default) or win-x64
#   output-directory:   defaults to ./artifacts/publish/<rid>
#
# The Linux/Windows validation of the published binaries is intentionally not
# performed here: it belongs to the deferred multi-platform validation phase.
set -euo pipefail

rid="${1:-linux-x64}"
output="${2:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/artifacts/publish/${rid}}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

case "${rid}" in
  linux-x64|linux-arm64|win-x64|win-arm64|osx-x64|osx-arm64) ;;
  *)
    echo "Unsupported runtime identifier: ${rid}" >&2
    exit 2
    ;;
esac

mkdir -p "${output}"
for project in Prism.Host Prism.Viewer; do
  echo "Publishing ${project} for ${rid}..."
  dotnet publish "${root}/${project}/${project}.csproj" \
    --configuration Release \
    --runtime "${rid}" \
    --self-contained true \
    --output "${output}/${project}" \
    -p:PublishReadyToRun=false
done

echo "Published binaries: ${output}"
