# ComfoBox MQTT client

Connect to your Zehnder ComfoBox with an ELESTA controller over RS485/BACnet MSTP and control it (read/write).

You can use eighter the plain .NET class library or the MQTT client (console app or Windows service). With the MQTT client you can easily bind the ComfoBox to your automation system (e.g. openHAB)

Currently it is still under development. It isn't stable yet. So try with care and on your own risk. You also may loose your warranty.

Keep in mind that you write the values to an EEPROM (> 1'000'000 writing cycles).

Platforms
---------
* Windows Vista and higher
* probably Linux and Mac over the Mono Framework (small changes required, see #11)


