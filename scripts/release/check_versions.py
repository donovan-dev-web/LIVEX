#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Vérifie la cohérence des versions entre les sources du dépôt."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[2]


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def get_uplugin_version(path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    m = re.search(r'"VersionName"\s*:\s*"([^"]+)"', text)
    return m.group(1) if m else "?"


def get_citation_version(path: Path) -> str:
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip().startswith("version:"):
            return line.split(":", 1)[1].strip().strip('"')
    return "?"


def get_props_version(path: Path) -> str:
    tree = ElementTree.parse(path)
    for v in tree.iter("Version"):
        return v.text or "?"
    return "?"


def get_init_version(path: Path) -> str:
    for line in path.read_text(encoding="utf-8").splitlines():
        m = re.search(r'__version__\s*=\s*"([^"]+)"', line)
        if m:
            return m.group(1)
    return "?"


def get_package_version(path: Path) -> str:
    return read_json(path).get("version", "?")


def main() -> int:
    report = {}
    report["syne/component.json"] = read_json(ROOT / "syne" / "component.json").get("version", "?")
    report["echos/component.json"] = read_json(ROOT / "echos" / "component.json").get("version", "?")
    report["echos/__init__.py"] = get_init_version(ROOT / "echos" / "echos" / "__init__.py")
    report["launcher/Directory.Build.props"] = get_props_version(ROOT / "launcher" / "Directory.Build.props")
    report["syne-mock/package.json"] = get_package_version(ROOT / "syne-mock" / "package.json")
    report["syne-mock/component.json"] = read_json(ROOT / "syne-mock" / "component.json").get("version", "?")
    report["prism LDK .uplugin VersionName"] = get_uplugin_version(
        ROOT / "prism" / "LDK" / "Plugins" / "PrismLdk" / "PrismLdk.uplugin"
    )
    report["CITATION.cff"] = get_citation_version(ROOT / "CITATION.cff")

    ei = ROOT / "installer" / "build" / "external-inputs.json"
    if ei.is_file():
        data = read_json(ei)
        report["external-inputs versions"] = sorted({e.get("version", "?") for e in data.get("inputs", [])})

    for key, value in report.items():
        print(f"{key}: {value}")

    # Vérifications minimales de présence.
    missing = [k for k, v in report.items() if v in ("?", None, "")]
    if missing:
        print("Versions manquantes :", ", ".join(missing), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
