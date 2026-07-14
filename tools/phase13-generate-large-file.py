#!/usr/bin/env python3
"""Create the deterministic, untracked large-text fixture used by Phase 13.

The fixture deliberately lives outside the repository and application settings
directory. Its deterministic bytes make performance samples comparable without
committing a multi-megabyte artifact.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import random


DEFAULT_BYTES = 8 * 1024 * 1024
DEFAULT_SEED = 13013


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="untracked fixture file to create")
    parser.add_argument(
        "--bytes",
        type=int,
        default=DEFAULT_BYTES,
        help=f"exact output size in bytes (default: {DEFAULT_BYTES})",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=DEFAULT_SEED,
        help=f"deterministic content seed (default: {DEFAULT_SEED})",
    )
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.bytes <= 0:
        raise SystemExit("--bytes must be positive")

    rng = random.Random(args.seed)
    alphabet = b"abcdefghijklmnopqrstuvwxyz0123456789_"
    args.output.parent.mkdir(parents=True, exist_ok=True)

    digest = hashlib.sha256()
    remaining = args.bytes
    with args.output.open("wb") as stream:
        while remaining:
            content_length = min(95, remaining)
            line = bytes(rng.choice(alphabet) for _ in range(content_length))
            if remaining > content_length:
                line += b"\n"
            stream.write(line)
            digest.update(line)
            remaining -= len(line)

    print(f"path={args.output.resolve()}")
    print(f"bytes={args.bytes}")
    print(f"seed={args.seed}")
    print(f"sha256={digest.hexdigest()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
