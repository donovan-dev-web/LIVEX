import json

import httpx
import pytest

from echos.ingestion import ControlClient, ControlError


def _recording_client(responses: list[httpx.Response]):
    requests: list[httpx.Request] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        return responses.pop(0)

    client = ControlClient(transport=httpx.MockTransport(handler))
    return client, requests


def test_start_sends_seed_only():
    client, requests = _recording_client([httpx.Response(200, json={"ok": True})])

    result = client.start(seed=42)

    request = requests[0]
    assert request.method == "POST"
    assert request.url == "http://127.0.0.1:5181/api/control/start"
    assert json.loads(request.content or b"{}") == {"seed": 42}
    assert result == {"ok": True}


def test_start_sends_seed_and_config():
    client, requests = _recording_client([httpx.Response(200, json={})])

    client.start(seed=1, config={"maxTicks": 100})

    assert json.loads(requests[0].content or b"{}") == {"seed": 1, "config": {"maxTicks": 100}}


def test_start_without_options_sends_empty_body():
    client, requests = _recording_client([httpx.Response(200)])

    assert client.start() == {}
    assert json.loads(requests[0].content or b"{}") == {}


def test_pause_and_resume_send_no_body():
    client, requests = _recording_client([httpx.Response(200), httpx.Response(200)])

    client.pause()
    client.resume()

    assert [r.url.path for r in requests] == [
        "/api/control/pause",
        "/api/control/resume",
    ]
    assert all(r.content == b"" for r in requests)


def test_reset_payload_uses_contract_keys():
    client, requests = _recording_client([httpx.Response(200, json={})])

    client.reset(seed=7, run_id="run-xyz")

    assert json.loads(requests[0].content or b"{}") == {"seed": 7, "runId": "run-xyz"}


def test_non_2xx_status_raises_control_error_with_status():
    client, _ = _recording_client([httpx.Response(500, text="boom")])

    with pytest.raises(ControlError) as exc_info:
        client.start(seed=1)

    assert exc_info.value.status == 500
    assert "boom" in exc_info.value.detail
    assert "/api/control/start" in str(exc_info.value)


def test_transport_error_is_wrapped():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("refusé", request=request)

    client = ControlClient(transport=httpx.MockTransport(handler))

    with pytest.raises(ControlError, match="transport"):
        client.pause()


def test_control_client_close_and_context_manager():
    client, _ = _recording_client([httpx.Response(200)])
    client.pause()

    client.close()

    entered, requests = _recording_client([httpx.Response(200)])
    with entered:
        entered.pause()
        assert requests[0].url.path == "/api/control/pause"
    assert entered._http.is_closed is True


def test_control_client_deterministic_requests():
    expected = [{"seed": 42}, {}]
    for _ in range(2):
        client, requests = _recording_client(
            [httpx.Response(200, json={}), httpx.Response(200, json={})]
        )
        client.start(seed=42)
        client.resume()
        assert [json.loads(r.content or b"{}") for r in requests] == expected
