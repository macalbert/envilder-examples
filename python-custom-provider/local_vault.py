"""A demo-only "secret store": a JSON file with AES-256-GCM encrypted values.

Each value is base64(iv | ciphertext | auth tag). The same format is used by
the TypeScript and .NET examples, so a vault sealed by one opens in the
others. Do NOT use this as a real secret store.
"""

from __future__ import annotations

import base64
import json
import os
from pathlib import Path

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

_IV_LENGTH = 12


def generate_key() -> bytes:
    return AESGCM.generate_key(bit_length=256)


def seal(key: bytes, plaintext: str) -> str:
    iv = os.urandom(_IV_LENGTH)
    # AESGCM appends the 16-byte auth tag to the ciphertext.
    ciphertext_and_tag = AESGCM(key).encrypt(iv, plaintext.encode(), None)
    return base64.b64encode(iv + ciphertext_and_tag).decode()


def open_sealed(key: bytes, sealed: str) -> str:
    data = base64.b64decode(sealed)
    iv, ciphertext_and_tag = data[:_IV_LENGTH], data[_IV_LENGTH:]
    return AESGCM(key).decrypt(iv, ciphertext_and_tag, None).decode()


def write_vault(path: Path, vault: dict[str, str]) -> None:
    path.write_text(json.dumps(vault, indent=2), encoding="utf-8")


def read_vault(path: Path) -> dict[str, str]:
    return json.loads(path.read_text(encoding="utf-8"))
