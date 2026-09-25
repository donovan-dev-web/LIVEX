from echos.reporting import build_report, json_report, markdown_report
from echos.storage.aggregation import TickRecord
from echos.storage.sqlite import AnalyticsStore


def _store(tmp_path):
    store = AnalyticsStore(tmp_path / "analytics.db")
    store.record_run("run-1", "0.1", seed="42")
    store.append_tick(TickRecord("run-1", "0.1", 1, 10, 2, 2, 5, 6, 7, 8, 1))
    store.append_event("run-1", 1, "decision_made", agent_id="a", action="Explore",
                       value='{"utility": 0.5}')
    store.append_tick_metrics("run-1", 1, {"Engine": {"score": 0.5}})
    store.append_tick_context("run-1", 1, "phenomena", [{"type": "formation"}])
    store.append_decision_trace("run-1", 1, {
        "agent_id": "a", "chosen_action": "Explore", "utility": 0.5,
        "deliberated": False, "interrupted": False, "cause": "",
        "beliefs_count": 0, "goals_count": 0, "memory_count": 0, "needs": {},
    })
    return store


def test_report_contains_all_store_surfaces_and_is_deterministic(tmp_path):
    with _store(tmp_path) as store:
        first = build_report(store, "run-1")
        assert first["run"]["seed"] == "42"
        assert first["run"]["tickRange"] == {"first": 1, "last": 1, "count": 1}
        assert first["events"][0]["value"] == {"utility": 0.5}
        assert first["metrics"]["series"]["Engine"]["score"] == [
            {"tick": 1, "value": 0.5}
        ]
        assert first["phenomena"][0]["payload"][0]["type"] == "formation"
        assert first["detectedPhenomena"] == []
        assert json_report(store, "run-1") == json_report(store, "run-1")
        assert "# ECHOS run `run-1`" in markdown_report(store, "run-1")
        assert build_report(store, "run-1", seed_override=99)["run"]["seed"] == "99"
        markdown = markdown_report(store, "run-1")
        assert "## Métriques principales" in markdown
        assert "## Évolution par tick" in markdown
        assert '"beliefs"' not in markdown


def test_report_aggregates_detected_phenomena_by_identifier(tmp_path):
    with _store(tmp_path) as store:
        store.append_tick_context(
            "run-1",
            2,
            "phenomena",
            {
                "detected": [
                    {
                        "identifier": "CommunityFormation",
                        "label": "Formation de communauté",
                        "description": "Deux communautés émergent.",
                        "signals": [{"metric": "NumberOfCommunities", "value": 3}],
                    }
                ],
                "disclaimer": "observe-only",
            },
        )
        report = build_report(store, "run-1")
        assert report["detectedPhenomena"][0]["identifier"] == "CommunityFormation"
        assert report["detectedPhenomena"][0]["firstTick"] == 2
        assert "Formation de communauté" in markdown_report(store, "run-1")
