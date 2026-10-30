#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Assemble une distribution LIVEX et écrit distribution.json (ARCHITECTURE §4.3)."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import tarfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_SIZE = int(1.9 * 1024 * 1024 * 1024)
SOURCE_DATE_EPOCH = int(os.environ.get("SOURCE_DATE_EPOCH", "1735689600"))


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def verify_external_inputs(external_inputs_path: Path, dist_inputs: Path) -> list[str]:
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


def extract_zip(src: Path, dst: Path) -> None:
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True, exist_ok=True)
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


def make_archive(payload_root: Path, out_path: Path) -> None:
    if out_path.suffix == ".zip":
        with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as zf:
            for path in sorted(payload_root.rglob("*")):
                if path.is_file():
                    arc = path.relative_to(payload_root).as_posix()
                    info = zipfile.ZipInfo(arc, (2025, 1, 1, 0, 0, 0))
                    info.compress_type = zipfile.ZIP_DEFLATED
                    info.external_attr = (0o100644 & 0xFFFF) << 16
                    with path.open("rb") as f:
                        zf.writestr(info, f.read())
    else:
        with tarfile.open(out_path, "w:gz", format=tarfile.PAX_FORMAT) as tf:
            for path in sorted(payload_root.rglob("*")):
                if path.is_file():
                    arc = path.relative_to(payload_root).as_posix()
                    ti = tf.gettarinfo(str(path), arcname=arc)
                    ti.mtime = SOURCE_DATE_EPOCH
                    ti.uid = ti.gid = 0
                    ti.uname = ti.gname = ""
                    ti.mode = 0o644
                    with path.open("rb") as f:
                        tf.addfile(ti, f)


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
    parser.add_argument("--output", default=None)
    args = parser.parse_args()

    rid = args.rid
    profile = args.profile
    tag = args.tag
    version = args.version
    dist_inputs = Path(args.dist_inputs)
    external_inputs_path = Path(args.external_inputs)
    components_path = Path(args.components)

    errors = verify_external_inputs(external_inputs_path, dist_inputs)
    if errors:
        for e in errors:
            print("ERREUR:", e)
        return 1

    components_meta = load_json(components_path)
    staging = Path(args.staging) if args.staging else ROOT / "dist" / "staging" / rid
    output = Path(args.output) if args.output else ROOT / "dist" / rid
    payload_root = output / "payload"
    if output.exists():
        shutil.rmtree(output)
    payload_root.mkdir(parents=True, exist_ok=True)

    # Charge utile : composants staging.
    for comp in components_meta["components"]:
        cid = comp["id"]
        if cid in ("prism", "prism-ldk"):
            continue
        src = staging / cid
        if not src.exists():
            if comp.get("mandatory", False):
                print(f"ERREUR: composant obligatoire absent du staging : {cid}")
                return 1
            continue
        if comp["installRoot"] == "root":
            # copie le contenu à la racine
            for child in src.iterdir():
                target = payload_root / child.name
                if child.is_dir():
                    copy_tree(child, target)
                else:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(child, target)
        else:
            copy_tree(src, payload_root / comp["installRoot"])

    # Entrées externes.
    ei = load_json(external_inputs_path)
    ei_by_id = {e["id"]: e for e in ei["inputs"]}
    # prism
    prism_comp = next(c for c in components_meta["components"] if c["id"] == "prism")
    if (staging / "prism").exists():
        copy_tree(staging / "prism", payload_root / prism_comp["installRoot"])
    else:
        entry = ei_by_id["prism-win-x64" if rid == "win-x64" else "prism-linux-x64"]
        archive = dist_inputs / entry["file"]
        extract_zip(archive, payload_root / prism_comp["installRoot"])
    # ldk : archive bit à bit
    ldk_entry = ei_by_id["prism-ldk"]
    ldk_dest = payload_root / next(c for c in components_meta["components"] if c["id"] == "prism-ldk")["installRoot"]
    ldk_dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dist_inputs / ldk_entry["file"], ldk_dest)

    # Construit le manifeste.
    all_files = build_file_list(payload_root)
    file_set = {f["path"] for f in all_files}

    def files_for_prefix(prefix: str) -> list[dict]:
        if prefix == "root":
            # fichiers à la racine ne commençant pas par components/ ou plugins/
            res = []
            for f in all_files:
                p = f["path"]
                if "/" not in p:
                    res.append(f)
            return res
        return [f for f in all_files if f["path"].startswith(prefix + "/") or f["path"] == prefix]

    # URL pour les release-assets.
    def asset_url(filename: str) -> str:
        return f"https://github.com/donovan-dev-web/LIVEX/releases/download/{tag}/{filename}"

    comp_descriptors = []
    for comp in components_meta["components"]:
        cid = comp["id"]
        optional = comp.get("optional", False)
        external = comp.get("external")
        install_root = comp["installRoot"]
        prefix = "" if install_root == "root" else install_root
        comp_files = files_for_prefix(prefix) if prefix else files_for_prefix("root")
        # Pour les composants non-root, filtrer correctement.
        if install_root != "root":
            comp_files = [f for f in all_files if f["path"].startswith(install_root + "/")]
        else:
            comp_files = [f for f in all_files if "/" not in f["path"]]

        # acquisition kind
        if external:
            if profile == "full":
                kind = "payload"
                asset = None
                url = None
                size = None
                sha = None
            else:
                kind = "release-asset"
                ext_id = "prism-win-x64" if (external == "prism" and rid == "win-x64") else ("prism-linux-x64" if external == "prism" else "prism-ldk")
                entry = ei_by_id[ext_id]
                asset = entry["file"]
                url = asset_url(entry["file"])
                size = entry["size"]
                sha = entry["sha256"]
                if not validate_url(url, tag):
                    print(f"ERREUR: URL hors tag courant pour {cid}: {url}")
                    return 1
                comp_files = []
        elif optional and profile == "core":
            # module facultatif publié comme archive de composant
            kind = "release-asset"
            asset_name = f"{cid}-{comp['version']}-{rid}.zip" if cid != "syne-mock" else f"syne-mock-{comp['version']}-{rid}.tar.gz"
            # Simplification : zip pour tous
            asset_name = f"{cid}-{comp['version']}-{rid}.zip"
            url = asset_url(asset_name)
            if not validate_url(url, tag):
                print(f"ERREUR: URL hors tag courant pour {cid}: {url}")
                return 1
            asset = asset_name
            size = None
            sha = None
            comp_files = []
        else:
            kind = "payload"
            asset = None
            url = None
            size = None
            sha = None

        if kind == "payload" and comp.get("mandatory", False) and not comp_files and install_root != "root":
            # autoriser si composant présent mais aucun fichier ? non
            pass

        acquisition = {
            "kind": kind,
            "asset": asset,
            "url": url,
            "size": size,
            "sha256": sha,
            "format": "zip" if kind == "release-asset" else None,
            "stripComponents": None,
            "reason": None if kind != "unavailable" else "non disponible",
        }

        descriptor = {
            "id": cid,
            "version": comp["version"],
            "mandatory": comp.get("mandatory", False),
            "nameFr": comp["nameFr"],
            "nameEn": comp["nameEn"],
            "descriptionFr": comp["descriptionFr"],
            "descriptionEn": comp["descriptionEn"],
            "license": comp["license"],
            "requires": comp.get("requires", []),
            "files": [{"path": f["path"], "size": f["size"], "sha256": f["sha256"]} for f in comp_files],
            "acquisition": acquisition,
        }
        comp_descriptors.append(descriptor)

    # Vérification taille globale.
    for f in all_files:
        if f["size"] > MAX_SIZE:
            print(f"ERREUR: fichier > 1.9 GiB : {f['path']}")
            return 1

    manifest = {
        "schema": 1,
        "product": "LIVEX",
        "version": version,
        "channel": "stable",
        "profile": profile,
        "platform": rid,
        "releasedAt": "2026-10-30T00:00:00Z",
        "components": comp_descriptors,
        "files": all_files,
        "signature": None,
    }

    output.mkdir(parents=True, exist_ok=True)
    (output / "distribution.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    # Archives de composants (zip).
    for comp in components_meta["components"]:
        cid = comp["id"]
        if comp["installRoot"] == "root":
            continue
        root_dir = payload_root / comp["installRoot"]
        if not root_dir.exists():
            continue
        archive_name = f"{cid}-{comp['version']}-{rid}.zip"
        make_archive(root_dir, output / archive_name)

    # Archive installateur (payload complet).
    ext = ".zip" if rid.startswith("win") else ".tar.gz"
    installer_name = f"livex-{version}-{rid}-{profile}{ext}"
    make_archive(payload_root, output / installer_name)

    print(f"Distribution assemblée dans {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
