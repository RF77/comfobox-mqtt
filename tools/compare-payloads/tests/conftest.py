"""Gemeinsame Hilfen fuer die Tests des Vergleichstools."""

from __future__ import annotations

import sys
from pathlib import Path

# Der Ordner, in dem das Paket ``cbcompare`` liegt, muss im Suchpfad sein.
TOOL_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOL_ROOT))

#: Der Repo-Root (drei Ebenen ueber ``tests/``).
REPO_ROOT = TOOL_ROOT.parents[1]

#: Die eingefrorene Test-Fixture mit allen MQTT-Topics.
TOPICS_JSON = REPO_ROOT / "docs" / "topics.json"
