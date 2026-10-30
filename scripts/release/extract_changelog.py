#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Extrait la section d'une version depuis CHANGELOG.md."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def extract(text: str, version: str) -> str:
    # Accepte "1.0.0" ou "[1.0.0]".
    target = version.strip().strip("[]")
    lines = text.splitlines()
    start = None
    for i, line in enumerate(lines):
        m = re.match(r"^##\s*\[([^\]]+)\]", line)
        if m and m.group(1).strip() == target:
            start = i
            break
    if start is None:
        raise SystemExit(f"version {target} introuvable dans le CHANGELOG")
    end = len(lines)
    for j in range(start + 1, len(lines)):
        if re.match(r"^##\s*\[", lines[j]):
            end = j
            break
    return "\n".join(lines[start:end]).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("version")
    parser.add_argument("--file", default=str(ROOT / "CHANGELOG.md"))
    parser.add_argument("--output", default=None)
    args = parser.parse_args()

    text = Path(args.file).read_text(encoding="utf-8")
    section = extract(text, args.version)
    if args.output:
        Path(args.output).write_text(section, encoding="utf-8")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stdout.write(section)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
