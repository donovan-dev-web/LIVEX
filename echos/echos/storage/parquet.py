"""Séries temporelles lourdes en Parquet (ECHOS-013).

Chaque agent/tick (énergie, besoins, fatigue, action, position) est écrit en
Parquet columnar. La **jointure SQLite ↔ Parquet est garantie par cohérence
d'identifiants** : toute clé ``(run_id, tick, agent_id)`` du Parquet doit
exister dans ``tick_summaries`` SQLite pour le même ``(run_id, tick)``
(PK du schéma ECHOS-012).
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Iterator

import pyarrow as pa
import pyarrow.parquet as pq

from echos.ingestion.stream import TickSegment

_SCHEMA = pa.schema(
    [
        pa.field("run_id", pa.string()),
        pa.field("tick", pa.int64()),
        pa.field("agent_id", pa.string()),
        pa.field("species", pa.string(), nullable=True),
        pa.field("position_x", pa.float64()),
        pa.field("position_y", pa.float64()),
        pa.field("energy", pa.float64()),
        pa.field("hunger", pa.float64()),
        pa.field("thirst", pa.float64()),
        pa.field("fatigue", pa.float64(), nullable=True),
        pa.field("current_action", pa.string(), nullable=True),
    ]
)


@dataclass(frozen=True)
class AgentSeriesRow:
    """Une ligne d'état d'un agent à un tick (clé = run_id + tick + agent_id)."""

    run_id: str
    tick: int
    agent_id: str
    position_x: float
    position_y: float
    energy: float
    hunger: float
    thirst: float
    fatigue: float | None
    current_action: str | None
    species: str | None = None


def agent_rows(segment: TickSegment) -> list[AgentSeriesRow]:
    """État complet des agents d'un tick, en ordre déterministe du snapshot."""
    snapshot = segment.snapshot
    rows: list[AgentSeriesRow] = []
    for agent in snapshot.agents:
        rows.append(
            AgentSeriesRow(
                run_id=snapshot.run_id,
                tick=snapshot.tick,
                agent_id=agent.id,
                position_x=agent.position.x,
                position_y=agent.position.y,
                energy=agent.energy,
                hunger=agent.hunger,
                thirst=agent.thirst,
                fatigue=agent.fatigue,
                current_action=agent.current_action,
                species=agent.species,
            )
        )
    return rows


def write_agent_series(path: str | Path, rows: Iterable[AgentSeriesRow]) -> None:
    """Écrit les lignes en Parquet (snappy), écrasant le fichier si présent."""
    rows = list(rows)
    columns: dict[str, list] = {field.name: [] for field in _SCHEMA}
    for row in rows:
        columns["run_id"].append(row.run_id)
        columns["tick"].append(row.tick)
        columns["agent_id"].append(row.agent_id)
        columns["species"].append(row.species)
        columns["position_x"].append(row.position_x)
        columns["position_y"].append(row.position_y)
        columns["energy"].append(row.energy)
        columns["hunger"].append(row.hunger)
        columns["thirst"].append(row.thirst)
        columns["fatigue"].append(row.fatigue)
        columns["current_action"].append(row.current_action)
    table = pa.Table.from_pydict(columns, schema=_SCHEMA)
    pq.write_table(table, str(path), compression="snappy")


def read_agent_series(path: str | Path) -> Iterator[AgentSeriesRow]:
    """Relit les lignes Parquet dans l'ordre d'écriture."""
    table = pq.read_table(str(path))
    for batch in table.to_batches():
        pa_dict = {field: batch.column(field).to_pylist() for field in table.column_names}
        for index in range(batch.num_rows):
            yield AgentSeriesRow(
                run_id=pa_dict["run_id"][index],
                tick=pa_dict["tick"][index],
                agent_id=pa_dict["agent_id"][index],
                species=pa_dict["species"][index],
                position_x=pa_dict["position_x"][index],
                position_y=pa_dict["position_y"][index],
                energy=pa_dict["energy"][index],
                hunger=pa_dict["hunger"][index],
                thirst=pa_dict["thirst"][index],
                fatigue=pa_dict["fatigue"][index],
                current_action=pa_dict["current_action"][index],
            )


def coherence_errors(
    rows: Iterable[AgentSeriesRow], indexed_ticks: set[int]
) -> list[str]:
    """Erreurs de jointure SQLite ↔ Parquet.

    ``indexed_ticks`` = numéros de ticks du run présents dans
    ``tick_summaries`` (SQLite). Toute ligne dont le tick n'y figure pas rompt
    la cohérence de jointure.
    """
    errors = [row.tick for row in rows if row.tick not in indexed_ticks]
    return [f"tick {tick} présent dans le Parquet mais absent de SQLite" for tick in errors]
