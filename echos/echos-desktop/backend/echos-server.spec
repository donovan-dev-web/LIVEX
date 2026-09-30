# -*- mode: python ; coding: utf-8 -*-
"""Spec PyInstaller — backend ECHOS, mode *onedir* (ADR-003 ECHOS).

Produit ``echos-desktop/resources/backend/echos-server/`` (dossier complet
avec l'interpréteur et les dépendances). Le shell Electron embarque ce
dossier via ``extraResources`` et lance ``echos-server`` comme enfant.

Choix *onedir* plutôt que *onefile* : le binaire onefile s'extrait dans un
dossier temporaire à chaque lancement (démarrage à froid de plusieurs
secondes) et se fait plus souvent marquer par les antivirus Windows.
"""

from pathlib import Path

from PyInstaller.utils.hooks import collect_all, collect_submodules

SPEC_DIR = Path(SPECPATH).resolve()
PROJECT_ROOT = SPEC_DIR.parents[1]  # echos/ (contient le paquet echos/)

datas = []
binaries = []
hiddenimports = []

# uvicorn charge ses implémentations (h11, websockets, boucles) par nom :
# sans import explicite, PyInstaller ne les voit pas.
for package in ("uvicorn", "websockets"):
    hiddenimports += collect_submodules(package)

# Le registre des moteurs d'analyse charge ses modules par importlib
# (echos/analysis/_common.py::load_engines — noms construits à l'exécution) :
# invisible de l'analyse statique de PyInstaller, d'où un crash au démarrage
# du binaire (« No module named 'echos.analysis.cognitive_diversity' ») sans
# cette collecte explicite du paquet.
hiddenimports += collect_submodules("echos")

# pyarrow n'est importé que par le stockage Parquet : collecte complète des
# modules compilés + métadonnées, sinon l'ingestion échoue dans le binaire.
pa_datas, pa_binaries, pa_hiddenimports = collect_all("pyarrow")
datas += pa_datas
binaries += pa_binaries
hiddenimports += pa_hiddenimports

a = Analysis(
    [str(SPEC_DIR / "echos-server.py")],
    pathex=[str(PROJECT_ROOT)],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=["tkinter", "pytest", "flake8", "matplotlib"],
    noarchive=False,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="echos-server",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    name="echos-server",
)
