#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Publie les artefacts V2 de PRISM destinés à la distribution (INSTALLER-200).

`prism-host`, `prism-viewer` et le pack de contenu sont des composants .NET
autonomes : sans publication, l'installateur ne peut ni les mesurer (taille,
SHA-256) ni les installer. Ce script produit, pour un RID donné, l'arborescence
attendue par `scripts/release/build_distribution.py` :

    dist/staging/<rid>/prism-host/bin/<prism-host>   (+ component.json)
    dist/staging/<rid>/prism-viewer/bin/<Prism.Viewer>
    dist/staging/<rid>/prism-content/<pack.json, ...>
    dist/staging/<rid>/prism-artifacts.json          (trace de publication)

Le manifeste du host est celui du dépôt (`prism/component.json`) : la
distribution l'aligne ensuite sur la version déclarée dans
`installer/build/components.json`. Le viewer n'a pas encore de manifeste
Launcher — il relève de PRISM-118 (P11) — donc seul son binaire est publié ici.

Aucune empreinte n'est calculée ici : `build_distribution.py` mesure les
archives qu'il produit. Ce script se contente de publier et de **vérifier que
les versions publiées sont celles que l'installateur déclare** — une divergence
installerait un binaire sous une autre identité que celle annoncée.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PRISM_ROOT = ROOT / "prism"
HOST_PROJECT = PRISM_ROOT / "Prism.Host" / "Prism.Host.csproj"
VIEWER_PROJECT = PRISM_ROOT / "Prism.Viewer" / "Prism.Viewer.csproj"
HOST_MANIFEST = PRISM_ROOT / "component.json"
CONTENT_ROOT = PRISM_ROOT / "content"
COMPONENTS_FILE = ROOT / "installer" / "build" / "components.json"

CONTENT_PROFILE = "minimal"
RIDS = ("win-x64", "linux-x64")


def run(command: list[str]) -> None:
    """Exécute une commande en échouant explicitement, sans masquer sa sortie."""
    print("  $" + " ".join(command))
    completed = subprocess.run(command, cwd=ROOT, text=True)
    if completed.returncode != 0:
        raise SystemExit(
            f"échec de la commande ({completed.returncode}) : {' '.join(command)}"
        )


def publish_project(project: Path, output: Path, rid: str, configuration: str) -> None:
    """Publie un projet .NET en autonome, dans un répertoire vidé au préalable."""
    if not project.is_file():
        raise SystemExit(f"projet absent : {project}")
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)
    run(
        [
            "dotnet",
            "publish",
            str(project),
            "-c",
            configuration,
            "-r",
            rid,
            "--self-contained",
            "true",
            "-o",
            str(output),
        ]
    )


def staging_stats(directory: Path) -> tuple[int, int]:
    """Nombre de fichiers et octets d'un composant publié."""
    files = [path for path in directory.rglob("*") if path.is_file()]
    return len(files), sum(path.stat().st_size for path in files)


def declared_versions() -> dict[str, str]:
    """Versions annoncées par l'installateur, par identifiant de composant."""
    data = json.loads(COMPONENTS_FILE.read_text(encoding="utf-8"))
    return {entry["id"]: entry["version"] for entry in data["components"]}


def verify_versions(published: dict[str, str]) -> None:
    """Refuse une publication dont la version diffère de celle que l'installateur déclare."""
    declared = declared_versions()
    problems = [
        f"{component} : publié {version}, déclaré {declared[component]}"
        for component, version in published.items()
        if declared.get(component) != version
    ]
    if problems:
        raise SystemExit(
            "versions incohérentes entre la publication et installer/build/components.json :\n  "
            + "\n  ".join(problems)
        )


def publish_rid(rid: str, staging: Path, configuration: str) -> dict:
    """Publie les trois composants V2 pour un RID et écrit la trace de publication."""
    if shutil.which("dotnet") is None:
        raise SystemExit("dotnet introuvable : PRISM V2 ne peut pas être publié")

    host_root = staging / "prism-host"
    publish_project(HOST_PROJECT, host_root / "bin", rid, configuration)
    shutil.copy2(HOST_MANIFEST, host_root / "component.json")
    host_version = json.loads(HOST_MANIFEST.read_text(encoding="utf-8"))["version"]

    viewer_root = staging / "prism-viewer"
    publish_project(VIEWER_PROJECT, viewer_root / "bin", rid, configuration)

    content_source = CONTENT_ROOT / CONTENT_PROFILE
    if not (content_source / "pack.json").is_file():
        raise SystemExit(f"pack de contenu absent : {content_source}")
    content_root = staging / "prism-content"
    if content_root.exists():
        shutil.rmtree(content_root)
    shutil.copytree(content_source, content_root)
    content_version = json.loads(
        (content_root / "pack.json").read_text(encoding="utf-8")
    )["version"]

    verify_versions(
        {
            "prism-host": host_version,
            "prism-viewer": host_version,
            "prism-content": content_version,
        }
    )

    entries = []
    for component, version, directory in (
        ("prism-host", host_version, host_root),
        ("prism-viewer", host_version, viewer_root),
        ("prism-content", content_version, content_root),
    ):
        files, size = staging_stats(directory)
        entries.append(
            {
                "id": component,
                "version": version,
                "path": directory.relative_to(staging).as_posix(),
                "files": files,
                "bytes": size,
            }
        )

    index = {
        "schema": 1,
        "rid": rid,
        "generator": "scripts/release/publish_prism_v2.py",
        "publishedAt": datetime.now(timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "configuration": configuration,
        "components": entries,
    }
    trace = staging / "prism-artifacts.json"
    trace.write_text(json.dumps(index, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return index


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Publie prism-host, prism-viewer et le pack de contenu (INSTALLER-200)."
    )
    parser.add_argument("--rid", default="current", choices=[*RIDS, "current"])
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--staging", default=None, help="défaut : dist/staging/<rid>")
    args = parser.parse_args()

    if args.rid == "current":
        rids = [RIDS[0] if sys.platform.startswith("win") else RIDS[1]]
    elif args.rid == "all":
        rids = list(RIDS)
    else:
        rids = [args.rid]

    for rid in rids:
        staging = Path(args.staging) if args.staging else ROOT / "dist" / "staging" / rid
        staging.mkdir(parents=True, exist_ok=True)
        print(f"==> publication PRISM V2 — {rid}")
        index = publish_rid(rid, staging, args.configuration)
        for entry in index["components"]:
            print(
                f"  {entry['id']:<14} {entry['version']:<18} "
                f"{entry['files']:>4} fichier(s)  {entry['bytes']:>12} o"
            )
        print(f"  trace : {(staging / 'prism-artifacts.json').relative_to(ROOT)}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
