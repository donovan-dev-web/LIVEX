#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Produit les archives déterministes des entrées externes PRISM et LDK.

Usage :
    python scripts/release/package_prism.py --version 1.0.0

Sorties dans dist/inputs/ :
    prism-<version>-win-x64.zip
    prism-<version>-linux-x64.tar.gz
    prism-ldk-<version>.zip

Met à jour installer/build/external-inputs.json avec tailles et SHA-256.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import time
import tarfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BUILD_WIN = ROOT / "prism" / "build" / "win"
BUILD_LINUX = ROOT / "prism" / "build" / "linux"
LDK_ZIP = ROOT / "prism" / "LDK_Package" / "PrismLdk.zip"
DIST_INPUTS = ROOT / "dist" / "inputs"
EXTERNAL_INPUTS = ROOT / "installer" / "build" / "external-inputs.json"

SOURCE_DATE_EPOCH = int(os.environ.get("SOURCE_DATE_EPOCH", "1735689600"))  # 2025-01-01 UTC


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def deterministic_zip(src_dir: Path, out_path: Path) -> None:
    """Archive déterministe d'un répertoire (entrées triées, mtime fixé)."""
    with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as zf:
        entries = sorted(
            (p for p in src_dir.rglob("*") if p.is_file()),
            key=lambda p: p.relative_to(src_dir).as_posix(),
        )
        for path in entries:
            arcname = path.relative_to(src_dir).as_posix()
            info = zipfile.ZipInfo(arcname, time.localtime(SOURCE_DATE_EPOCH)[:6])
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = (0o100644 & 0xFFFF) << 16
            info.create_system = 3
            with path.open("rb") as f:
                zf.writestr(info, f.read())


def deterministic_tar_gz(src_dir: Path, out_path: Path) -> None:
    with tarfile.open(out_path, "w:gz", format=tarfile.PAX_FORMAT) as tf:
        entries = sorted(
            (p for p in src_dir.rglob("*")),
            key=lambda p: p.relative_to(src_dir).as_posix(),
        )
        for path in entries:
            arcname = path.relative_to(src_dir).as_posix()
            if not arcname:
                continue
            ti = tf.gettarinfo(str(path), arcname=arcname)
            ti.mtime = SOURCE_DATE_EPOCH
            ti.uid = 0
            ti.gid = 0
            ti.uname = ""
            ti.gname = ""
            if path.is_dir():
                ti.type = tarfile.DIRTYPE
                ti.mode = 0o755
                tf.addfile(ti)
            elif path.is_file():
                ti.type = tarfile.REGTYPE
                ti.mode = 0o644
                with path.open("rb") as f:
                    tf.addfile(ti, f)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True, help="version PRISM")
    parser.add_argument("--ldk-version", default=None, help="version PRISM-LDK (défaut: --version)")
    args = parser.parse_args()
    version = args.version
    ldk_version = args.ldk_version or version

    DIST_INPUTS.mkdir(parents=True, exist_ok=True)

    win_name = f"prism-{version}-win-x64.zip"
    linux_name = f"prism-{version}-linux-x64.tar.gz"
    ldk_name = f"prism-ldk-{ldk_version}.zip"

    if not BUILD_WIN.is_dir():
        raise SystemExit(f"build PRISM Windows absent : {BUILD_WIN}")
    if not BUILD_LINUX.is_dir():
        raise SystemExit(f"build PRISM Linux absent : {BUILD_LINUX}")
    if not LDK_ZIP.is_file():
        raise SystemExit(f"archive LDK absente : {LDK_ZIP}")

    win_out = DIST_INPUTS / win_name
    linux_out = DIST_INPUTS / linux_name
    ldk_out = DIST_INPUTS / ldk_name

    deterministic_zip(BUILD_WIN, win_out)
    deterministic_tar_gz(BUILD_LINUX, linux_out)
    # LDK publié bit à bit, sans ré-archivage.
    ldk_out.write_bytes(LDK_ZIP.read_bytes())

    inputs = [
        {
            "id": "prism-win-x64",
            "version": version,
            "file": win_name,
            "size": win_out.stat().st_size,
            "sha256": sha256_file(win_out),
        },
        {
            "id": "prism-linux-x64",
            "version": version,
            "file": linux_name,
            "size": linux_out.stat().st_size,
            "sha256": sha256_file(linux_out),
        },
        {
            "id": "prism-ldk",
            "version": ldk_version,
            "file": ldk_name,
            "size": ldk_out.stat().st_size,
            "sha256": sha256_file(ldk_out),
        },
    ]

    EXTERNAL_INPUTS.parent.mkdir(parents=True, exist_ok=True)
    EXTERNAL_INPUTS.write_text(
        json.dumps({"inputs": inputs}, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    # Vérification bit-à-bit du LDK.
    assert sha256_file(ldk_out) == sha256_file(LDK_ZIP), "LDK non identique"

    print(json.dumps(inputs, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
