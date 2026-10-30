#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Vérifie les assets de release : noms, taille < 1,9 GiB, SHA-256, URLs.

Usage :
    python scripts/release/check_assets.py [--dist-dir dist/inputs] [--post]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_SIZE = int(1.9 * 1024 * 1024 * 1024)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def check_external_inputs(dist_dir: Path) -> list[str]:
    errors: list[str] = []
    ei_path = ROOT / "installer" / "build" / "external-inputs.json"
    if not ei_path.is_file():
        return [f"external-inputs.json absent : {ei_path}"]
    data = json.loads(ei_path.read_text(encoding="utf-8"))
    for entry in data.get("inputs", []):
        path = dist_dir / entry["file"]
        if not path.is_file():
            errors.append(f"asset manquant : {entry['file']}")
            continue
        size = path.stat().st_size
        if size != entry["size"]:
            errors.append(f"taille différente pour {entry['file']}: {size} != {entry['size']}")
        if size > MAX_SIZE:
            errors.append(f"asset trop gros : {entry['file']} ({size} octets)")
        digest = sha256_file(path)
        if digest != entry["sha256"]:
            errors.append(f"SHA-256 différent pour {entry['file']}")
    return errors


def check_distribution(manifest_path: Path) -> list[str]:
    errors: list[str] = []
    if not manifest_path.is_file():
        return []
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    for f in data.get("files", []):
        # Vérification de forme uniquement ici ; les chemins sont relatifs.
        if f.get("size", 0) < 0:
            errors.append(f"taille négative : {f.get('path')}")
        digest = f.get("sha256", "")
        if digest and len(digest) != 64:
            errors.append(f"empreinte non conforme : {f.get('path')}")
    return errors


def post_check(urls: list[str]) -> list[str]:
    errors: list[str] = []
    for url in urls:
        try:
            req = urllib.request.Request(url, method="HEAD")
            with urllib.request.urlopen(req, timeout=30) as resp:
                if resp.status >= 400:
                    errors.append(f"URL en erreur {resp.status} : {url}")
        except Exception as exc:  # noqa: BLE001
            errors.append(f"URL inaccessible : {url} ({exc})")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dist-dir", default=str(ROOT / "dist" / "inputs"))
    parser.add_argument("--manifest", default=None)
    parser.add_argument("--post", action="store_true")
    parser.add_argument("--urls", nargs="*", default=[])
    args = parser.parse_args()

    dist_dir = Path(args.dist_dir)
    errors = []
    errors.extend(check_external_inputs(dist_dir))
    if args.manifest:
        errors.extend(check_distribution(Path(args.manifest)))
    if args.post and args.urls:
        errors.extend(post_check(args.urls))

    if errors:
        for e in errors:
            print("ERREUR:", e, file=sys.stderr)
        return 1
    print("check_assets : OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
