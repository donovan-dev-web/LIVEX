"""Instrumentation ECHOS ph5 (ECHOS-050/051/052).

Couvre le package ``echos.instrumentation`` — preuve J5 étendue :
- ``EchosLogger`` : journalisation structurée JSON Lines (format §1/§4,
  déterministe : clés triées), traces et profilage séparés ;
- ``build_decision_trace`` : fusion d'un ``decision_made`` SYNE avec le
  contexte BDI du snapshot (valeur d'analyse causale, schéma v3) ;
- profiling : ``compute_all_profiled`` retourne les mêmes résultats que
  ``compute_all`` + le coût des 8 moteurs (sortie §5, déterminisme intact).
"""

import json
import re

import pytest

from echos.analysis import compute_all
from echos.ingestion.models import ExternalEvent
from echos.instrumentation import EchosLogger, ProfileMarkers
from echos.instrumentation import build_decision_trace, compute_all_profiled
from echos.instrumentation import logging as logging_module

SNAPSHOT = {
    "tick": 1,
    "runId": "run-9",
    "version": "0.4.0",
    "agents": [
        {
            "id": "1",
            "species": "Entité A",
            "energy": 50,
            "hunger": 30.0,
            "thirst": 20.0,
            "fatigue": 10.5,
            "currentAction": "SeekFood",
            "beliefs": [
                {"subject": "water", "predicate": "safe", "value": "true",
                 "confidence": 0.9}
            ],
            "goals": [{"kind": "SeekFood", "age": 10}],
            "memoryCount": 8,
        }
    ],
    "resources": [],
}


def _decision(**value) -> ExternalEvent:
    return ExternalEvent(
        type="decision_made",
        tick=1,
        agent_id="1",
        action="SeekWater",
        cause="hunger=100,thirst=100,fatigue=61.5",
        value={"intention": "SeekWater", "utility": 61.7933890505355, **value},
    )


class TestEchosLogger:
    def test_structured_writes_deterministic_jsonl(self, tmp_path):
        logger = EchosLogger(tmp_path)
        logger.structured("run-9", 1, {"EmergenceIndicators": {"EmergenceScore": 0.5}})
        logger.structured("run-9", 2, {"EmergenceIndicators": {"EmergenceScore": 0.6}})

        lines = (tmp_path / "structured-run-9.jsonl").read_text().splitlines()
        assert len(lines) == 2
        first, second = (json.loads(line) for line in lines)
        assert first["event"] == "tick_metrics"
        assert first["run_id"] == "run-9"
        assert first["tick"] == 1
        assert first["metrics"]["EmergenceIndicators"]["EmergenceScore"] == 0.5
        # format Identique d'un tick à l'autre : seulement les valeurs changent
        assert list(first) == list(second)
        assert set(first["metrics"]) == set(second["metrics"])

    def test_structured_keys_are_sorted(self, tmp_path):
        logger = EchosLogger(tmp_path)
        logger.decision(trace("run-9"))
        line = json.loads((tmp_path / "decision-traces-run-9.jsonl").read_text())
        assert list(line) == sorted(line)  # clés triées → hash d'export stable

    def test_profiling_writes_report(self, tmp_path):
        logger = EchosLogger(tmp_path)
        logger.profiling("run-9", 1, {"Perception": {"calls": 1, "totalMs": 2.0, "avgMs": 2.0}})
        payload = json.loads((tmp_path / "profilage-run-9.jsonl").read_text())
        assert payload["event"] == "engine_profiling"
        assert payload["engines"]["Perception"]["totalMs"] == 2.0

    def test_text_level_tags_sse_v2(self, tmp_path):
        logger = EchosLogger(tmp_path)
        logger.info("démarrage de l'analyse")
        files = list(tmp_path.glob("echos-*.log"))
        assert len(files) == 1
        content = files[0].read_text()
        assert f"[{logging_module._APP_TAG}]" in content  # ligne taggée SSE-V2
        assert "démarrage de l'analyse" in content

    def test_from_env_uses_echos_log_dir(self, tmp_path, monkeypatch):
        monkeypatch.setenv("ECHOS_LOG_DIR", str(tmp_path / "logs-ci"))
        logger = EchosLogger.from_env()
        assert logger.log_dir == tmp_path / "logs-ci"
        assert logger.log_dir.is_dir()


def trace(run_id: str) -> dict:
    """Trace minimale valide au schéma v3 (helper des tests JSONL)."""
    return {
        "run_id": run_id,
        "tick": 3,
        "agent_id": "1",
        "chosen_action": "SeekWater",
        "utility": 0.9,
        "deliberated": False,
        "interrupted": False,
        "cause": "hunger=100",
        "beliefs_count": 0,
        "goals_count": 0,
        "memory_count": 0,
        "needs": {},
    }


class TestBuildDecisionTrace:
    def test_merges_decision_with_bdi_context(self):
        trace = build_decision_trace("run-9", 1, _decision(), SNAPSHOT)
        assert trace["agent_id"] == "1"
        assert trace["chosen_action"] == "SeekWater"
        assert trace["utility"] == 61.7933890505355
        assert trace["cause"] == "hunger=100,thirst=100,fatigue=61.5"
        # contexte BDI lu sur le snapshot (jamais écrit dans le monde)
        assert trace["beliefs_count"] == 1
        assert trace["goals_count"] == 1
        assert trace["memory_count"] == 8
        assert trace["needs"] == {
            "hunger": 30.0,
            "thirst": 20.0,
            "fatigue": 10.5,
            "energy": 50.0,
        }

    def test_deliberated_and_interrupted_flags(self):
        deliberate = build_decision_trace(
            "run-9", 1, _decision(deliberated=True, interrupted=False), SNAPSHOT
        )
        interrupted = build_decision_trace(
            "run-9", 1, _decision(deliberated=True, interrupted=True), SNAPSHOT
        )
        assert deliberate["deliberated"] is True
        assert deliberate["interrupted"] is False
        assert interrupted["interrupted"] is True

    def test_unknown_agent_defaults_to_zero_context(self):
        event = ExternalEvent(
            type="decision_made",
            tick=1,
            agent_id="ghost",
            action="Idle",
            value={"intention": "Idle"},
        )
        trace = build_decision_trace("run-9", 1, event, SNAPSHOT)
        assert trace["beliefs_count"] == 0
        assert trace["goals_count"] == 0
        assert trace["memory_count"] == 0
        assert trace["needs"] == {}
        assert trace["chosen_action"] == "Idle"

    def test_rejects_non_decision_events(self):
        event = ExternalEvent(type="message_sent", tick=1, agent_id="1")
        with pytest.raises(ValueError, match="decision_made"):
            build_decision_trace("run-9", 1, event, SNAPSHOT)


class TestProfiling:
    def test_profiled_results_match_compute_all(self):
        results, profile = compute_all_profiled(SNAPSHOT)
        assert results == compute_all(SNAPSHOT)  # sortie bit-à-bit inchangée

    def test_covers_all_engines_with_single_call(self):
        _, profile = compute_all_profiled(SNAPSHOT)
        assert len(profile) == 8  # 7 moteurs + EmergenceIndicators
        for name, stats in profile.items():
            assert stats["calls"] == 1
            assert stats["totalMs"] >= 0.0
            assert stats["avgMs"] == pytest.approx(stats["totalMs"], abs=1e-6)

    def test_report_format_matches_spec(self):
        markers = ProfileMarkers()
        markers._total_ms = {"Perception": 240.51, "Decisions": 12.34}
        markers._calls = {"Perception": 1, "Decisions": 5}
        lines = markers.formatted()
        assert len(lines) == 2
        assert lines[0].startswith("Decisions")  # tri alphabétique du résumé
        assert lines[1].startswith("Perception")
        for line in lines:
            assert re.fullmatch(
                r"\S+\s*:\s+[\d.]+\s+ms total,\s+[\d.]+\s+ms avg", line
            )

    def test_markers_accumulate_calls(self):
        markers = ProfileMarkers()
        for _ in range(3):
            with markers.measure("Perception"):
                pass
        assert markers.summary()["Perception"]["calls"] == 3
