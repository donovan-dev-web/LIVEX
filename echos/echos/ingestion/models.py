"""Modèles pydantic des contrats d'ingestion SYNE (API_CONTRACTS.md).

Les messages sur le WebSocket :5180 sont des JSON **camelCase** (contrat de
transport §2). Les modèles acceptent l'alias camelCase en entrée
(``populate_by_name``) tout en exposant des attributs Python snake_case, et
sérialisent en camelCase avec ``model_dump(by_alias=True)``.
"""

from __future__ import annotations

from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field, model_validator
from pydantic.alias_generators import to_camel


class Position(BaseModel):
    """Position 2D dans le monde SYNE (pixels, flottant)."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    x: float
    y: float


class Agent(BaseModel):
    """Entité observée (API_CONTRACTS.md §2.1).

    En V0.1, l'émetteur SYNE fournit ``species`` et ``fatigue`` mais pas
    ``health`` (santé non encore simulée) : ``health`` reste accepté pour la
    compatibilité ascendante avec le format documentaire.
    """

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    id: str
    species: str | None = None
    position: Position
    health: float | None = Field(default=None, ge=0)
    energy: float = Field(ge=0)
    hunger: float = Field(ge=0)
    thirst: float = Field(ge=0)
    fatigue: float | None = Field(default=None, ge=0)
    current_action: str | None = None


class Resource(BaseModel):
    """Ressource du monde (type, quantité, capacité)."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    id: str
    type: str
    position: Position
    quantity: float = Field(ge=0)
    capacity: float = Field(ge=0)


class WorldSnapshot(BaseModel):
    """Snapshot de monde — message système ``snapshot`` (API_CONTRACTS.md §2.1)."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    type: Literal["snapshot"] = "snapshot"
    version: str
    run_id: str
    tick: int = Field(ge=0)
    simulated_time_minutes: int = Field(ge=0)
    alive_count: int = Field(ge=0)
    agents: list[Agent] = Field(default_factory=list)
    resources: list[Resource] = Field(default_factory=list)


class ExternalEvent(BaseModel):
    """Événement externe — messages typés (API_CONTRACTS.md §2.2).

    Types : ``decision_made``, ``agent_spawned``, ``agent_died``,
    ``message_sent``, ``group_formed``, ``conflict``... (ADR-004).
    """

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    type: str
    tick: int = Field(ge=0)
    agent_id: str | None = None
    target_id: str | None = None
    action: str | None = None
    cause: str | None = None
    value: dict[str, Any] | None = None

    @model_validator(mode="after")
    def _reserve_snapshot(self) -> "ExternalEvent":
        if self.type == "snapshot":
            raise ValueError(
                "type='snapshot' est réservé au message système WorldSnapshot"
            )
        return self


Message = WorldSnapshot | ExternalEvent
"""Union des messages reçus sur le WebSocket :5180."""


class InvalidMessageError(ValueError):
    """Payload non conforme aux contrats — message déterministe (réception)."""

    def __init__(self, payload_type: str, detail: str) -> None:
        super().__init__(
            f"message {payload_type} invalide (conforme API_CONTRACTS.md) : {detail}"
        )
        self.payload_type = payload_type
        self.detail = detail


def parse_message(payload: str | bytes) -> Message:
    """Parse un message du WebSocket :5180 de façon déterministe.

    Le wrapper ``snapshot`` (ADR-004) est routé vers :class:`WorldSnapshot`,
    tout autre ``type`` vers :class:`ExternalEvent`.
    """
    from pydantic import ValidationError

    try:
        data: Any = (
            payload
            if isinstance(payload, str)
            else payload.decode("utf-8")
        )
        import json

        obj = json.loads(data)
    except (ValueError, UnicodeDecodeError) as exc:
        raise InvalidMessageError(
            "json", f"payload non encodable/JSON invalide ({exc})"
        ) from exc

    if not isinstance(obj, dict):
        raise InvalidMessageError("json", "le message doit être un objet JSON")

    try:
        if obj.get("type") == "snapshot":
            return WorldSnapshot.model_validate(obj)
        return ExternalEvent.model_validate(obj)
    except ValidationError as exc:
        summarized = "; ".join(
            f"{err['loc'][-1]} {err['msg']}" for err in exc.errors()
        )
        raise InvalidMessageError(
            str(obj.get("type", "unknown")), summarized
        ) from exc
