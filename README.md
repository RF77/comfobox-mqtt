# ComfoBox MQTT client

Connect to your Zehnder ComfoBox over RS485/BACnet MSTP and control it (read/write).

You can use eighter the plain .NET class library or the MQTT client (console app or Windows service). With the MQTT client you can easily bind the ComfoBox to your automation system (e.g. openHAB)

Currently it is still under development with a lot of breaking changes. It isn't stable yet. So try with care and on your own risk. You also may loose your warranty.

Currently unknown are the number of write cycles of the EEPROM/Flash. Maybe there are only 10000 cycles, so don't write too many values.

Platforms
---------
* Windows Vista and higher
* probably Linux and Mac over the Mono Framework (small changes required, see #11)


