#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Assemble la distribution LIVEX publique.

Le ZIP final ne contient qu'un petit nombre de fichiers :

    distribution.json
    livex-setup.exe
    LICENSE, NOTICE, THIRD-PARTY-NOTICES.md, LISEZ-MOI.txt
    assets/
    packages/<composant>-<version>-<rid>.zip

Chaque composant embarqué est compressé une seule fois dans ``packages/`` ;
l'installateur extrait ces archives au lieu de copier des milliers de fichiers.
Les actifs externes (PRISM, LDK) restent des ``release-asset`` téléchargés
depuis la release, et ne sont pas inclus dans le ZIP public.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import tarfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_SIZE = int(1.9 * 1024 * 1024 * 1024)
SOURCE_DATE_EPOCH = int(os.environ.get("SOURCE_DATE_EPOCH", "1735689600"))
LEGAL_FILES = ("LICENSE", "NOTICE", "THIRD-PARTY-NOTICES.md")
README_NAME = "LISEZ-MOI.txt"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def verify_external_inputs(external_inputs_path: Path, dist_inputs: Path) -> list[str]:
    """Vérifie les entrées externes présentes : taille, plafond et empreinte."""
    errors: list[str] = []
    if not external_inputs_path.is_file():
        return [f"external-inputs.json absent : {external_inputs_path}"]
    data = load_json(external_inputs_path)
    for entry in data.get("inputs", []):
        path = dist_inputs / entry["file"]
        if not path.is_file():
            errors.append(f"entrée externe absente : {entry['file']}")
            continue
        if path.stat().st_size != entry["size"]:
            errors.append(f"taille différente : {entry['file']}")
        if path.stat().st_size > MAX_SIZE:
            errors.append(f"asset trop gros : {entry['file']}")
        if sha256_file(path) != entry["sha256"]:
            errors.append(f"empreinte différente : {entry['file']}")
    return errors


def validate_url(url: str, tag: str) -> bool:
    """Refuse une URL hors dépôt et hors tag courant."""
    if not url:
        return False
    prefix = f"https://github.com/donovan-dev-web/LIVEX/releases/download/{tag}/"
    return url.startswith(prefix)


def copy_tree(src: Path, dst: Path) -> None:
    if dst.exists():
        shutil.rmtree(dst)
    if src.is_dir():
        shutil.copytree(src, dst)
    elif src.is_file():
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)


def extract_archive(src: Path, dst: Path) -> None:
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True, exist_ok=True)
    if str(src).endswith(".tar.gz") or str(src).endswith(".tgz"):
        with tarfile.open(src, "r:gz") as tf:
            tf.extractall(dst)
    else:
        with zipfile.ZipFile(src) as zf:
            zf.extractall(dst)


def build_file_list(payload_root: Path) -> list[dict]:
    files = []
    for path in sorted(payload_root.rglob("*")):
        if path.is_file():
            rel = path.relative_to(payload_root).as_posix()
            files.append({
                "path": rel,
                "size": path.stat().st_size,
                "sha256": sha256_file(path),
            })
    return files


def make_archive(src: Path, out_path: Path, exclude: set[str] | None = None) -> None:
    """Compresse un répertoire (ou fichier) de façon reproductible."""
    exclude = exclude or set()
    if out_path.suffix == ".zip":
        with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as zf:
            paths = sorted(src.rglob("*")) if src.is_dir() else [src]
            for path in paths:
                if not path.is_file():
                    continue
                arc = path.relative_to(src).as_posix() if src.is_dir() else path.name
                if arc in exclude:
                    continue
                info = zipfile.ZipInfo(arc, (2025, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = (0o100644 & 0xFFFF) << 16
                with path.open("rb") as f:
                    zf.writestr(info, f.read())
    else:
        with tarfile.open(out_path, "w:gz", format=tarfile.PAX_FORMAT) as tf:
            for path in sorted(src.rglob("*")):
                if not path.is_file():
                    continue
                arc = path.relative_to(src).as_posix()
                if arc in exclude:
                    continue
                ti = tf.gettarinfo(str(path), arcname=arc)
                ti.mtime = SOURCE_DATE_EPOCH
                ti.uid = ti.gid = 0
                ti.uname = ti.gname = ""
                ti.mode = 0o644
                with path.open("rb") as f:
                    tf.addfile(ti, f)


def external_by_id(external_inputs_path: Path) -> dict:
    if not external_inputs_path.is_file():
        return {}
    return {entry["id"]: entry for entry in load_json(external_inputs_path).get("inputs", [])}


def normalize_manifest(comp: dict, rid: str) -> dict | None:
    """Charge le manifeste source du composant et aligne son exécutable."""
    manifest_rel = comp.get("manifest")
    if not manifest_rel:
        return None
    source = ROOT / manifest_rel
    if not source.is_file():
        return None

    manifest = load_json(source)
    manifest["version"] = comp["version"]

    executable = comp.get("executables", {}).get(rid)
    if executable:
        manifest.setdefault("executable", {})
        manifest["executable"]["windows" if rid.startswith("win") else "linux"] = executable
    return manifest


def stage_component(comp: dict, staging: Path, rid: str, work_dir: Path) -> Path | None:
    """Prépare le contenu d'un composant et renvoie son répertoire source."""
    if comp.get("source") == "assets":
        source = ROOT / "installer" / "build" / "assets"
    else:
        source = staging / comp["id"]

    if not source.is_dir():
        return None

    manifest = normalize_manifest(comp, rid)
    if manifest is None:
        return source

    # Le manifeste normalisé vit dans une copie de travail : le staging reste intact.
    destination = work_dir / comp["id"]
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(source, destination)
    (destination / "component.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    return destination


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rid", required=True, choices=["win-x64", "linux-x64"])
    parser.add_argument("--profile", default="core", choices=["core", "full"])
    parser.add_argument("--tag", default="livex-v1.0.0")
    parser.add_argument("--version", default="1.0.0")
    parser.add_argument("--staging", default=None)
    parser.add_argument("--dist-inputs", default=str(ROOT / "dist" / "inputs"))
    parser.add_argument("--external-inputs", default=str(ROOT / "installer" / "build" / "external-inputs.json"))
    parser.add_argument("--components", default=str(ROOT / "installer" / "build" / "components.json"))
    parser.add_argument("--setup-exe", default=None)
    parser.add_argument("--output", default=None)
    args = parser.parse_args()

    rid = args.rid
    profile = args.profile
    tag = args.tag
    version = args.version
    staging = Path(args.staging) if args.staging else ROOT / "dist" / "staging" / rid
    dist_inputs = Path(args.dist_inputs)
    external_inputs_path = Path(args.external_inputs)
    components_path = Path(args.components)
    output = Path(args.output) if args.output else ROOT / "dist" / rid

    if not staging.is_dir():
        print(f"ERREUR: staging absent : {staging}")
        return 1

    components_meta = load_json(components_path)
    ex = external_by_id(external_inputs_path)

    setup_exe = Path(args.setup_exe) if args.setup_exe else (
        staging / "setup" / ("livex-setup.exe" if rid.startswith("win") else "livex-setup")
    )
    if not setup_exe.is_file():
        print(f"ERREUR: livex-setup absent : {setup_exe}")
        return 1

    if output.exists():
        shutil.rmtree(output)
    packages = output / "packages"
    packages.mkdir(parents=True, exist_ok=True)
    work_dir = output / ".work"
    work_dir.mkdir(parents=True, exist_ok=True)

    # Un seul ZIP par composant embarqué.
    archive_names: dict[str, str] = {}
    component_dirs: dict[str, Path] = {}
    for comp in components_meta["components"]:
        if comp.get("external"):
            continue
        source = stage_component(comp, staging, rid, work_dir)
        if source is None:
            if comp.get("mandatory", False):
                print(f"ERREUR: composant obligatoire absent du staging : {comp['id']}")
                return 1
            continue

        archive_name = f"{comp['id']}-{comp['version']}-{rid}.zip"
        exclude = set(LEGAL_FILES) if comp["id"] == "launcher" else None
        make_archive(source, packages / archive_name, exclude)
        archive_names[comp["id"]] = archive_name
        component_dirs[comp["id"]] = source

    # Manifeste de distribution.
    descriptors = []
    for comp in components_meta["components"]:
        cid = comp["id"]
        install_root = comp["installRoot"]
        external = comp.get("external")

        if external:
            kind, asset, url, size, sha, reason = "unavailable", None, None, None, None, None
            ext_id = None
            if external == "prism":
                ext_id = "prism-win-x64" if rid == "win-x64" else "prism-linux-x64"
            elif external == "prism-ldk":
                ext_id = "prism-ldk"

            entry = ex.get(ext_id) if ext_id else None
            archive = dist_inputs / entry["file"] if entry else None

            if entry and archive and archive.is_file() and sha256_file(archive) == entry["sha256"]:
                if external == "prism" and profile == "full":
                    # Profil hors-ligne : PRISM devient une archive embarquée.
                    extracted = work_dir / "prism-content"
                    extract_archive(archive, extracted)
                    archive_name = f"prism-{comp['version']}-{rid}.zip"
                    make_archive(extracted, packages / archive_name)
                    kind = "bundled-archive"
                    asset = f"packages/{archive_name}"
                    size = (packages / archive_name).stat().st_size
                    sha = sha256_file(packages / archive_name)
                else:
                    kind = "release-asset"
                    asset = entry["file"]
                    url = (
                        f"https://github.com/donovan-dev-web/LIVEX/releases/download/{tag}/{entry['file']}"
                    )
                    if not validate_url(url, tag):
                        print(f"ERREUR: URL hors tag courant pour {cid}: {url}")
                        return 1
                    size = entry["size"]
                    sha = entry["sha256"]
            else:
                reason = "archive externe absente de dist/inputs"

            acquisition = {
                "kind": kind,
                "asset": asset,
                "url": url,
                "size": size,
                "sha256": sha,
                "format": "zip" if kind == "bundled-archive" else None,
                "stripComponents": None,
                "reason": reason,
            }
            files: list[dict] = []
        else:
            archive_name = archive_names.get(cid)
            if archive_name is None:
                kind = "unavailable"
                reason = "composant absent du staging"
                acquisition = {
                    "kind": kind,
                    "asset": None,
                    "url": None,
                    "size": None,
                    "sha256": None,
                    "format": None,
                    "stripComponents": None,
                    "reason": reason,
                }
                files = []
            else:
                archive = packages / archive_name
                acquisition = {
                    "kind": "bundled-archive",
                    "asset": f"packages/{archive_name}",
                    "url": None,
                    "size": archive.stat().st_size,
                    "sha256": sha256_file(archive),
                    "format": "zip",
                    "stripComponents": None,
                    "reason": None,
                }
                # Le détail fichier par fichier reste dans le ZIP du composant :
                # le manifeste public ne le duplique pas (allègement).
                files = []

        descriptors.append({
            "id": cid,
            "version": comp["version"],
            "mandatory": comp.get("mandatory", False),
            "nameFr": comp["nameFr"],
            "nameEn": comp["nameEn"],
            "descriptionFr": comp["descriptionFr"],
            "descriptionEn": comp["descriptionEn"],
            "license": comp["license"],
            "requires": comp.get("requires", []),
            "files": files,
            "acquisition": acquisition,
            "installRoot": install_root,
        })

    shutil.rmtree(work_dir, ignore_errors=True)

    manifest = {
        "schema": 1,
        "product": "LIVEX",
        "version": version,
        "channel": "stable",
        "profile": profile,
        "platform": rid,
        "releasedAt": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "components": descriptors,
        "files": [],
        "signature": None,
    }
    (output / "distribution.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    # Exécutable d'installation et mentions légales à la racine.
    shutil.copy2(setup_exe, output / setup_exe.name)
    for name in LEGAL_FILES:
        source = ROOT / name
        if source.is_file():
            shutil.copy2(source, output / name)

    (output / README_NAME).write_text(
        "LIVEX\n"
        "=====\n\n"
        "1. Décompressez ce fichier ZIP dans un dossier de votre choix.\n"
        f"2. Lancez {setup_exe.name}.\n"
        "3. Suivez l'assistant : il installe LIVEX et conserve les archives\n"
        "   des composants dans packages/.\n\n"
        "Rien n'est installé avant votre confirmation. Une installation\n"
        "existante peut être mise à jour, réparée ou désinstallée depuis\n"
        "le même exécutable.\n",
        encoding="utf-8",
    )

    # Le ZIP public final : un seul fichier à télécharger.
    ext = ".zip"
    final_name = f"LIVEX-{version}-{rid}-{profile}{ext}"
    make_archive(output, output.parent / final_name, exclude={final_name})
    print(f"Distribution assemblée dans {output}")
    print(f"Archive publique : {output.parent / final_name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
