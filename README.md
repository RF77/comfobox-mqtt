# ComfoBox MQTT client

Connect to your Zehnder ComfoBox with an ELESTA controller over RS485/BACnet MSTP and control it (read/write).

## Installation

Download [here](https://github.com/RF77/comfobox-mqtt/releases) the installer and install it.

The files are installed in your %ProgramFiles%/ComfoBox and contains the following components:

### ComfoBoxLib

This is .NET class library to read/write BACnet items from your ComboBox. If you don't want to access your ComfoBox over MQTT, create a new project and use this library.

### Demo Client

This is a simple client that uses the ComfoBoxLib.
Configuration: DemoClient.exe.config

### ComfoBox MQTT Console Client

This is The MQTT client as console application. Usefull to see the logs directly.
Configuration: ComfoBoxMqttConsole.exe.config. Please change the port name to your port which is attached with the ComfoBox.

### ComfoBox MQTT Windows Service

The same as the console application but as windows service. After the installation the service will be started on demand. At first change the configuration and start the service afterwards. Optionally change the the service startup from demand to auto.

Configuration: ComfoboxService.exe.config. Please change the port name to your port which is attached with the ComfoBox.

## MQTT Topics

[Here](Added the [Mono compatibility test](https://rawgit.com/RF77/comfobox-mqtt/master/docs/topics.txt) is a list of all MQTT topics.

### Write values
Use ../Set topics to write a value to your ComfoBox. In the topics.txt you can see the writable topics

### Enum values
Use ../AsNumber topics to read enums as number. Use the /AsNumber/Set topics to write a enum as number

### Special topics
All topics under ComfoBox/Special are not ComfoBox values, but special values calculated by the MQTT service itself

## Platforms
* Windows Vista and higher
* probably Linux and Mac over the Mono Framework (small changes required, see #11)

>Try with care and on your **own risk**. You also may loose your warranty.
>Keep in mind that you write the values to an EEPROM (1'000'000 writing cycles).
