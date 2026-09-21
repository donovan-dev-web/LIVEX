import tomllib
from pathlib import Path

from echos import __version__


def test_version_matches_pyproject():
    pyproject = tomllib.loads(
        (Path(__file__).resolve().parents[2] / "pyproject.toml").read_text()
    )
    assert __version__ == pyproject["project"]["version"]
