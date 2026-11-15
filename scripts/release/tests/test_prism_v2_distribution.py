# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later

"""Prism V2 dans la distribution (INSTALLER-200).

Vérifie que l'hôte, le viewer et le pack de contenu publiés par
`publish_prism_v2.py` produisent trois composants mesurables : une archive par
composant, une empreinte SHA-256 et une taille, les dépendances déclarées
(viewer → hôte → pack) et la version du pack.
"""

import json
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_distribution as bd


def _components() -> dict:
    def entry(component, version, requires, install_root, **extra):
        return {
            "id": component,
            "version": version,
            "mandatory": False,
            "optional": True,
            "external": None,
            "nameFr": component,
            "nameEn": component,
            "descriptionFr": "description",
            "descriptionEn": "description",
            "license": "GPL-3.0-or-later",
            "requires": requires,
            "installRoot": install_root,
            **extra,
        }

    return {
        "version": "1.0.0",
        "components": [
            entry(
                "prism-host",
                "2.0.0-preview.1",
                ["prism-content"],
                "components/prism-host",
                manifest="prism/component.json",
                executables={
                    "win-x64": "bin/prism-host.exe",
                    "linux-x64": "bin/prism-host",
                },
            ),
            entry(
                "prism-viewer",
                "2.0.0-preview.1",
                ["prism-host"],
                "components/prism-viewer",
                executables={
                    "win-x64": "bin/Prism.Viewer.exe",
                    "linux-x64": "bin/Prism.Viewer",
                },
            ),
            entry("prism-content", "0.1.0", [], "components/prism-content"),
        ],
    }


def _write(path: Path, content: str = "binaire") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def test_les_trois_composants_v2_sont_mesures_et_distribues(tmp_path, monkeypatch):
    staging = tmp_path / "staging"
    _write(staging / "setup" / "livex-setup")
    _write(staging / "prism-host" / "bin" / "prism-host")
    (staging / "prism-host" / "component.json").write_text(
        (bd.ROOT / "prism" / "component.json").read_text(encoding="utf-8"), encoding="utf-8"
    )
    _write(staging / "prism-viewer" / "bin" / "Prism.Viewer")
    _write(
        staging / "prism-content" / "pack.json",
        json.dumps({"id": "livex_minimal", "version": "0.1.0"}),
    )
    _write(staging / "prism-content" / "recipes" / "bread.json", "{}")

    components = tmp_path / "components.json"
    components.write_text(json.dumps(_components()), encoding="utf-8")
    output = tmp_path / "out"
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "build_distribution.py",
            "--rid",
            "linux-x64",
            "--staging",
            str(staging),
            "--components",
            str(components),
            "--dist-inputs",
            str(tmp_path / "inputs"),
            "--setup-exe",
            str(staging / "setup" / "livex-setup"),
            "--output",
            str(output),
        ],
    )

    assert bd.main() == 0

    manifest = json.loads((output / "distribution.json").read_text(encoding="utf-8"))
    by_id = {component["id"]: component for component in manifest["components"]}
    assert set(by_id) == {"prism-host", "prism-viewer", "prism-content"}

    for component_id, listed in by_id.items():
        acquisition = listed["acquisition"]
        assert acquisition["kind"] == "bundled-archive", component_id
        assert acquisition["asset"] == f"packages/{component_id}-{listed['version']}-linux-x64.zip"
        assert acquisition["size"] and acquisition["size"] > 0
        assert len(acquisition["sha256"]) == 64
        assert (output / acquisition["asset"]).is_file()

    # Options de la distribution : rien n'est imposé, et la chaîne de dépendance
    # est celle que l'installateur vérifiera (facultatif → facultatif).
    assert not any(component["mandatory"] for component in by_id.values())
    assert by_id["prism-viewer"]["requires"] == ["prism-host"]
    assert by_id["prism-host"]["requires"] == ["prism-content"]
    assert by_id["prism-content"]["version"] == "0.1.0"

    # Le manifeste du host est normalisé pour la plateforme, sans réécriture du
    # dépôt : l'archive installée porte l'exécutable du RID distribué.
    with zipfile.ZipFile(output / by_id["prism-host"]["acquisition"]["asset"]) as archive:
        host_manifest = json.loads(archive.read("component.json"))
    assert host_manifest["executable"]["linux"] == "bin/prism-host"


def test_le_manifeste_du_host_est_aligne_sur_le_rid(tmp_path):
    staging = tmp_path / "staging"
    _write(staging / "prism-host" / "bin" / "prism-host")
    (staging / "prism-host" / "component.json").write_text(
        (bd.ROOT / "prism" / "component.json").read_text(encoding="utf-8"), encoding="utf-8"
    )
    work = tmp_path / "work"
    work.mkdir()

    staged = bd.stage_component(
        next(c for c in _components()["components"] if c["id"] == "prism-host"),
        staging,
        "linux-x64",
        work,
    )

    manifest = json.loads((staged / "component.json").read_text(encoding="utf-8"))
    assert manifest["executable"]["linux"] == "bin/prism-host"
    assert manifest["version"] == "2.0.0-preview.1"
    assert manifest["type"] == "immersion"
