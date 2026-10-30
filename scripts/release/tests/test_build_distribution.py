import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_distribution as bd

def test_validate_url_foreign():
    assert not bd.validate_url("https://evil.example/x", "livex-v1.0.0")

def test_validate_url_wrong_tag():
    assert not bd.validate_url("https://github.com/donovan-dev-web/LIVEX/releases/download/livex-v0.9.0/a.zip", "livex-v1.0.0")

def test_validate_url_ok():
    assert bd.validate_url("https://github.com/donovan-dev-web/LIVEX/releases/download/livex-v1.0.0/a.zip", "livex-v1.0.0")

def test_verify_external_inputs_missing(tmp_path):
    ei = tmp_path / "ei.json"
    ei.write_text(json.dumps({"inputs": [{"id": "x", "file": "missing.zip", "size": 1, "sha256": "a"}]}))
    errs = bd.verify_external_inputs(ei, tmp_path)
    assert any("absente" in e for e in errs)
