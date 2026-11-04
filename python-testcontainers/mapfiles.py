import json
from collections.abc import Callable
from pathlib import Path

from envilder import MapFileParser, ParsedMapFile

# The map files live at the repo root, shared by every example
ROOT = Path(__file__).parent.parent


def with_unique_secrets(path: Path, unique: Callable[[str], str]) -> ParsedMapFile:
    """Reads a shared map file and gives every mapped secret a name no other
    test uses, keeping $config as committed."""
    map_file = json.loads(path.read_text())
    for variable, secret in map_file.items():
        if not variable.startswith("$"):
            map_file[variable] = unique(secret)
    return MapFileParser().parse(json.dumps(map_file))
