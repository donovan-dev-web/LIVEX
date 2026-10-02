"""Linux Launcher adapter for ECHOS' environment-configured server."""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    """Accept the shared Launcher arguments without treating them as ECHOS options."""
    parser = argparse.ArgumentParser(description="Start ECHOS for the LIVEX Launcher.")
    parser.add_argument("--headless", action="store_true")
    parser.add_argument("--instance-id")
    parser.add_argument("--control-port", type=int)
    parser.add_argument("--work-dir")
    parser.add_argument("--log-dir")
    parser.add_argument("--correlation-id")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> None:
    args = parse_args(argv)
    os.environ["ECHOS_HOST"] = "127.0.0.1"
    if args.control_port is not None:
        if not 1 <= args.control_port <= 65535:
            raise SystemExit("--control-port must be between 1 and 65535")
        os.environ["ECHOS_PORT"] = str(args.control_port)
    from echos.server import main as serve

    serve()


if __name__ == "__main__":
    main()
