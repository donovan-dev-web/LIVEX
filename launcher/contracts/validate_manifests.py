#!/usr/bin/env python3
"""Validate the published schema and every workspace component manifest."""

import json
from pathlib import Path
import subprocess
import sys

from jsonschema import Draft202012Validator


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CONTRACTS_ROOT = Path(__file__).resolve().parent
SCHEMA_PATH = CONTRACTS_ROOT / "component-manifest-v1.schema.json"


def main() -> int:
    schema_document = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema_document)
    validator = Draft202012Validator(schema_document)

    valid_example = CONTRACTS_ROOT / "examples" / "component-manifest-v1.valid.json"
    validator.validate(json.loads(valid_example.read_text(encoding="utf-8")))

    invalid_example = CONTRACTS_ROOT / "examples" / "component-manifest-v1.invalid-path.json"
    invalid_errors = list(
        validator.iter_errors(json.loads(invalid_example.read_text(encoding="utf-8")))
    )
    if not invalid_errors:
        print(f"Expected invalid example to fail schema validation: {invalid_example}")
        return 1

    workspace_files = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=REPOSITORY_ROOT,
        check=True,
        capture_output=True,
        text=False,
    ).stdout.split(b"\0")
    manifests = sorted(
        REPOSITORY_ROOT / Path(path.decode("utf-8"))
        for path in workspace_files
        if path and Path(path.decode("utf-8")).name == "component.json"
    )
    if not manifests:
        print("No component.json manifests found in the workspace.")
        return 1

    failures = []
    for manifest_path in manifests:
        relative_path = manifest_path.relative_to(REPOSITORY_ROOT)
        errors = sorted(
            validator.iter_errors(json.loads(manifest_path.read_text(encoding="utf-8"))),
            key=lambda error: list(map(str, error.absolute_path)),
        )
        for error in errors:
            location = ".".join(map(str, error.absolute_path)) or "<root>"
            failures.append(f"{relative_path}:{location}: {error.message}")

    if failures:
        print("\n".join(failures))
        return 1

    print(
        f"Schema valid; accepted example valid; rejected example invalid; "
        f"{len(manifests)} workspace component manifest(s) valid."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
