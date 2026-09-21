"""Ingestion — contrats d'observation et de pilotage de SYNE.

- :mod:`models` : modèles pydantic des contrats `WorldSnapshot` / `ExternalEvent`
  (JSON camelCase, API_CONTRACTS.md §2) + :func:`parse_message` déterministe.
- :mod:`ws_client` : consommateur WebSocket :5180 (transport injectable).
- :mod:`control_client` : client HTTP :5181 (start / pause / resume / reset).
"""

from .control_client import ControlClient, ControlError, DEFAULT_BASE_URL
from .models import (
    Agent,
    ExternalEvent,
    InvalidMessageError,
    Message,
    parse_message,
    Position,
    Resource,
    WorldSnapshot,
)
from .ws_client import WsClient, WsTransport

__all__ = [
    "Agent",
    "ControlClient",
    "ControlError",
    "DEFAULT_BASE_URL",
    "ExternalEvent",
    "InvalidMessageError",
    "Message",
    "Position",
    "Resource",
    "WorldSnapshot",
    "WsClient",
    "WsTransport",
    "parse_message",
]
