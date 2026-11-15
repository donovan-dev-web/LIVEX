#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Réécrit les chemins d'exécutable d'un manifeste de composant pour un staging à plat.

Le manifeste du dépôt déclare des chemins relatifs au projet
(``Simulation.Console/bin/Release/.../publish/Simulation.Console.exe``) ; le
staging publie les binaires à la racine du composant. Copié tel quel, le
manifeste fait chercher au Launcher un binaire inexistant et SYNE est déclaré
« binaire absent ».
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", help="manifeste du composant dans le staging")
    parser.add_argument("--windows", required=True, help="nom de l'exécutable Windows, relatif")
    parser.add_argument("--linux", required=True, help="nom de l'exécutable Linux, relatif")
    args = parser.parse_args()

    path = Path(args.manifest)
    manifest = json.loads(path.read_text(encoding="utf-8"))
    manifest["executable"] = {"windows": args.windows, "linux": args.linux}
    path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
