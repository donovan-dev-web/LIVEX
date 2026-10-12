"""Worker d'ingestion local (``echos.dev_ingest``) : configuration d'environnement.

La configuration est lue et validée **avant** toute connexion. Sans cette
validation, ``ECHOS_ANALYSIS_EVERY=abc`` échouait sur un ``int()`` sans nommer la
variable, et ``ECHOS_ANALYSIS_EVERY=0`` était accepté ici puis rejeté par
``consume`` — c'est-à-dire après l'ouverture de la base et la connexion au
flux SYNE.
"""

import pytest

from echos.dev_ingest import (
    ConfigurationError,
    _reconnect_delay,
    read_config,
)


def test_defaults_are_used_when_the_environment_is_empty():
    config = read_config({})

    assert config["database"] == "echos/data/livex-analytics.sqlite"
    assert config["ws_url"] == "ws://127.0.0.1:5180/"
    assert config["parquet_path"] is None
    assert config["analysis_every"] == 1
    assert config["parquet_flush_every"] is None
    assert config["started_file"] is None
    assert config["stop_after_disconnect"] is False


def test_environment_overrides_are_read():
    config = read_config(
        {
            "ECHOS_ANALYTICS_DB": "/tmp/x.sqlite",
            "SYNE_OBSERVABILITY_URL": "ws://localhost:6000/",
            "ECHOS_PARQUET_PATH": "/tmp/x.parquet",
            "ECHOS_ANALYSIS_EVERY": "5",
            "ECHOS_PARQUET_FLUSH_EVERY": "10",
            "LIVEX_WS_STARTED_FILE": "/tmp/started",
            "LIVEX_INGEST_ONCE": "1",
        }
    )

    assert config["database"] == "/tmp/x.sqlite"
    assert config["ws_url"] == "ws://localhost:6000/"
    assert config["parquet_path"] == "/tmp/x.parquet"
    assert config["analysis_every"] == 5
    assert config["parquet_flush_every"] == 10
    assert config["started_file"] == "/tmp/started"
    assert config["stop_after_disconnect"] is True


@pytest.mark.parametrize(
    "environ",
    [
        {"ECHOS_ANALYSIS_EVERY": "abc"},
        {"ECHOS_ANALYSIS_EVERY": "1.5"},
        {"ECHOS_PARQUET_FLUSH_EVERY": "many"},
    ],
)
def test_non_numeric_cadence_names_the_offending_variable(environ):
    with pytest.raises(ConfigurationError) as error:
        read_config(environ)
    message = str(error.value)
    assert next(iter(environ)) in message
    assert "entier" in message


@pytest.mark.parametrize(
    "environ",
    [
        {"ECHOS_ANALYSIS_EVERY": "0"},
        {"ECHOS_ANALYSIS_EVERY": "-3"},
        {"ECHOS_PARQUET_FLUSH_EVERY": "0"},
        {"ECHOS_PARQUET_FLUSH_EVERY": "-10"},
    ],
)
def test_non_positive_cadence_is_rejected_before_connecting(environ):
    with pytest.raises(ConfigurationError) as error:
        read_config(environ)
    assert ">= 1" in str(error.value)


def test_empty_value_falls_back_to_the_default():
    """``ECHOS_PARQUET_FLUSH_EVERY=`` (export vide) reste « non défini »."""
    config = read_config({"ECHOS_PARQUET_FLUSH_EVERY": "", "ECHOS_ANALYSIS_EVERY": ""})

    assert config["parquet_flush_every"] is None
    assert config["analysis_every"] == 1


def test_reconnect_delay_grows_exponentially_and_is_capped():
    assert _reconnect_delay(0) == 0.5
    assert _reconnect_delay(1) == 1.0
    assert _reconnect_delay(2) == 2.0
    assert _reconnect_delay(20) == 30.0
    assert _reconnect_delay(64) == 30.0


def test_read_config_restores_the_process_environment(monkeypatch):
    monkeypatch.setenv("ECHOS_ANALYSIS_EVERY", "7")

    assert read_config({"ECHOS_ANALYSIS_EVERY": "3"})["analysis_every"] == 3

    import os

    assert os.environ["ECHOS_ANALYSIS_EVERY"] == "7"
