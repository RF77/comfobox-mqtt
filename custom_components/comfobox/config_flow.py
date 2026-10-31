"""Config Flow der ComfoBox-Integration."""

from __future__ import annotations

import logging
from typing import Any

import voluptuous as vol

from homeassistant.config_entries import ConfigFlow, ConfigFlowResult
from homeassistant.const import CONF_HOST, CONF_TOKEN, CONF_VERIFY_SSL
from homeassistant.helpers.aiohttp_client import async_create_clientsession

from .api import AuthenticationError, ComfoBoxApiClient, ConnectionFailed
from .const import DEFAULT_HOST, DEFAULT_SCAN_INTERVAL, DOMAIN

_LOGGER = logging.getLogger(__name__)

STEP_USER_SCHEMA = vol.Schema(
    {
        vol.Required(CONF_HOST, default=DEFAULT_HOST): str,
        vol.Required(CONF_TOKEN): str,
        vol.Optional(CONF_VERIFY_SSL, default=True): bool,
    }
)


class ComfoBoxConfigFlow(ConfigFlow, domain=DOMAIN):
    """Handle den Config Flow der ComfoBox-Integration."""

    VERSION = 1

    async def async_step_user(
        self, user_input: dict[str, Any] | None = None
    ) -> ConfigFlowResult:
        """Fragt Adresse und Token ab."""
        errors: dict[str, str] = {}

        if user_input is not None:
            host = user_input[CONF_HOST].rstrip("/")
            token = user_input[CONF_TOKEN]

            session = async_create_clientsession(
                self.hass, verify_ssl=user_input.get(CONF_VERIFY_SSL, True)
            )
            client = ComfoBoxApiClient(session, host, token)

            try:
                status = await client.get_status()
            except AuthenticationError:
                errors["base"] = "invalid_auth"
            except ConnectionFailed:
                errors["base"] = "cannot_connect"
            except Exception:  # noqa: BLE001 - jede unerwartete Exception ist ein Connect-Fehler
                _LOGGER.exception("Unerwarteter Fehler beim Verbinden mit dem Gateway")
                errors["base"] = "unknown"
            else:
                await self.async_set_unique_id(status.device_id)
                self._abort_if_unique_id_configured()
                return self.async_create_entry(
                    title=f"ComfoBox ({host})",
                    data={
                        CONF_HOST: host,
                        CONF_TOKEN: token,
                        CONF_VERIFY_SSL: user_input.get(CONF_VERIFY_SSL, True),
                    },
                    options={"scan_interval": DEFAULT_SCAN_INTERVAL},
                )

        return self.async_show_form(
            step_id="user",
            data_schema=STEP_USER_SCHEMA,
            errors=errors,
        )

    async def async_step_reauth(
        self, entry_data: dict[str, Any]
    ) -> ConfigFlowResult:
        """Fordert einen neuen Token an, wenn der alte abgelaufen ist."""
        return await self.async_step_reauth_confirm()

    async def async_step_reauth_confirm(
        self, user_input: dict[str, Any] | None = None
    ) -> ConfigFlowResult:
        """Bestätigt die Reauthentifizierung mit einem neuen Token."""
        errors: dict[str, str] = {}
        entry = self._get_reauth_entry()

        if user_input is not None:
            session = async_create_clientsession(self.hass)
            client = ComfoBoxApiClient(
                session, entry.data[CONF_HOST], user_input[CONF_TOKEN]
            )
            try:
                await client.get_status()
            except AuthenticationError:
                errors["base"] = "invalid_auth"
            except ConnectionFailed:
                errors["base"] = "cannot_connect"
            else:
                return self.async_update_reload_and_abort(
                    entry, data_updates={CONF_TOKEN: user_input[CONF_TOKEN]}
                )

        return self.async_show_form(
            step_id="reauth_confirm",
            data_schema=vol.Schema({vol.Required(CONF_TOKEN): str}),
            errors=errors,
        )
