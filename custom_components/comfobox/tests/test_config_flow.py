"""Tests des Config Flows der ComfoBox-Integration."""

from __future__ import annotations

from unittest.mock import AsyncMock, patch

import pytest

from custom_components.comfobox.api import AuthenticationError, ConnectionFailed
from custom_components.comfobox.const import DOMAIN

from .conftest import _status_obj


async def test_user_flow_success(hass, mock_config_entry):
    """Der Normalfall legt einen Config-Eintrag an."""
    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": "user"}
    )

    with patch(
        "custom_components.comfobox.config_flow.ComfoBoxApiClient.get_status",
        AsyncMock(return_value=_status_obj({"deviceId": "comfobox1"})),
    ):
        result = await hass.config_entries.flow.async_configure(
            result["flow_id"],
            {
                "host": "http://comfobox.local:8099",
                "token": "test-token",
                "verify_ssl": True,
            },
        )
        await hass.async_block_till_done()

    assert result["type"] == "create_entry"
    assert result["title"] == "ComfoBox (http://comfobox.local:8099)"
    assert result["data"]["host"] == "http://comfobox.local:8099"


@pytest.mark.parametrize(
    ("error", "expected"),
    [
        (AuthenticationError("token"), "invalid_auth"),
        (ConnectionFailed("timeout"), "cannot_connect"),
    ],
)
async def test_user_flow_errors(hass, error, expected):
    """Fehler werden verstaendlich gemeldet."""
    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": "user"}
    )

    with patch(
        "custom_components.comfobox.config_flow.ComfoBoxApiClient.get_status",
        AsyncMock(side_effect=error),
    ):
        result = await hass.config_entries.flow.async_configure(
            result["flow_id"],
            {
                "host": "http://comfobox.local:8099",
                "token": "test-token",
                "verify_ssl": True,
            },
        )

    assert result["type"] == "form"
    assert result["errors"] == {"base": expected}
