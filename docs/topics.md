#Topics

Writable items have a /Set topic. Take care and write only values you exactly know! Some of the values cannot be changed until the config value ExpertMode is set to true.
I'm sorry about the english/german mix. Finally only german names would be better due to german source documents.

## ComfoBox.Config.Comfofond.CoolingHysteresis
### MQTT Topics
* ComfoBox/Config/Comfofond/CoolingHysteresis
* ComfoBox/Config/Comfofond/CoolingHysteresis/Set

### Description
Min: 2, Max: 20

Hysterese beim Ein- und Ausschalten der Kühlung über das Comfofond-L. z.B. Sollwert ist bei 22°C
-> falls Kühlung aus, wird sie erst >24°C eingeschaltet
-> falls Kühlung ein, wird sie erst kleiner 20°C ausgeschaltet


## ComfoBox.Config.Comfofond.CoolingSetPoint
### MQTT Topics
* ComfoBox/Config/Comfofond/CoolingSetPoint
* ComfoBox/Config/Comfofond/CoolingSetPoint/Set

### Description
Min: -50, Max: 999

Sollwert Kühlung: Ab dieser Temperatur wird die Luft über das Comfofond-L gekühlt, falls der Raumsollwert der Lüftung tiefer liegt


## ComfoBox.Config.Comfofond.HeatingHysteresis
### MQTT Topics
* ComfoBox/Config/Comfofond/HeatingHysteresis
* ComfoBox/Config/Comfofond/HeatingHysteresis/Set

### Description
Min: 2, Max: 20

Hysterese beim Ein- und Ausschalten der Heizung über das Comfofond-L z.B. Sollwert ist bei 2°C;
-> falls Heizung aus, wird sie erst unter 0°C eingeschaltet
-> falls Heizung ein, wird sie erst oberhalb 4°C ausgeschaltet


## ComfoBox.Config.Comfofond.HeatingSetPoint
### MQTT Topics
* ComfoBox/Config/Comfofond/HeatingSetPoint
* ComfoBox/Config/Comfofond/HeatingSetPoint/Set

### Description
Min: -50, Max: 999

Sollwert Heizung: Ab dieser Temperatur wird die Luft über das Comfofond-L im Winter gewärmt


## ComfoBox.Config.EnergyComsumer.CoolingZone.AbsMinVorlauftemp
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/AbsMinVorlauftemp

### Description
Min: 0, Max: 99

Abs min Vorlauftemp Kühlen


## ComfoBox.Config.EnergyComsumer.CoolingZone.Cooling
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/Cooling

### Description
Min: 0, Max: 3

Kühlen:
Keine Funktion = 0
Kühlen = 1
Nur Kühlen = 3


## ComfoBox.Config.EnergyComsumer.CoolingZone.FixpunktRaumsoll
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/FixpunktRaumsoll
* ComfoBox/Config/EnergyComsumer/CoolingZone/FixpunktRaumsoll/Set

### Description
Min: 10, Max: 30

Fixpunkt Raumsoll Küh (20°C)


## ComfoBox.Config.EnergyComsumer.CoolingZone.KühlraumsollAbstNormal
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/KühlraumsollAbstNormal

### Description
Min: 0, Max: 10

Kühlraumsoll’abst normal


## ComfoBox.Config.EnergyComsumer.CoolingZone.KühlraumsollAbstReduziert
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/KühlraumsollAbstReduziert

### Description
Min: 0, Max: 10

Kühlraumsoll’abst reduziert


## ComfoBox.Config.EnergyComsumer.CoolingZone.KühlraumsollAbstStandby
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/KühlraumsollAbstStandby

### Description
Min: 0, Max: 10

Kühlraumsoll’abst standby


## ComfoBox.Config.EnergyComsumer.CoolingZone.MinVorlTemp20
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/MinVorlTemp20

### Description
Min: 0, Max: 99

Min Vorl’temp Kühlen (20°C)


## ComfoBox.Config.EnergyComsumer.CoolingZone.MinVorlTemp40
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/MinVorlTemp40

### Description
Min: 0, Max: 99

Min Vorl’temp Kühlen (40°C)


## ComfoBox.Config.EnergyComsumer.CoolingZone.RaumeinflussBeiKühlen
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/RaumeinflussBeiKühlen
* ComfoBox/Config/EnergyComsumer/CoolingZone/RaumeinflussBeiKühlen/Set

### Description
Min: 100, Max: 999

Raumeinfluss bei Kühlen


## ComfoBox.Config.EnergyComsumer.CoolingZone.Sommerkühlgrenze
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/Sommerkühlgrenze

### Description
Min: -10, Max: 10

Sommerkühlgrenze


## ComfoBox.Config.EnergyComsumer.CoolingZone.SteilheitRaumsollSchiebung
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/SteilheitRaumsollSchiebung
* ComfoBox/Config/EnergyComsumer/CoolingZone/SteilheitRaumsollSchiebung/Set

### Description
Min: 0, Max: 5

Steilheit Raumsoll-Schiebung


## ComfoBox.Config.EnergyComsumer.CoolingZone.UmschaltungRaumfühler
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/CoolingZone/UmschaltungRaumfühler
* ComfoBox/Config/EnergyComsumer/CoolingZone/UmschaltungRaumfühler/Set

### Description
Min: 0, Max: 2

Umschaltung Raumfühler:
Keine Funktion = 0
Heiz'n mit Tr1, Kühl'n mit Tr2 = 1
Heiz'n mit Tr2, Kühl'n mit Tr1 = 2



## ComfoBox.Config.EnergyComsumer.HeatPump.FlowMaxTemperature
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeatPump/FlowMaxTemperature
* ComfoBox/Config/EnergyComsumer/HeatPump/FlowMaxTemperature/Set

### Description
Min: 0, Max: 58

WP-Vorlauf Maximal


## ComfoBox.Config.EnergyComsumer.HeatPump.MinHeatpumpRunningTime
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeatPump/MinHeatpumpRunningTime
* ComfoBox/Config/EnergyComsumer/HeatPump/MinHeatpumpRunningTime/Set

### Description
Min: 0, Max: 30

Min WP-Laufzeit


## ComfoBox.Config.EnergyComsumer.HeatPump.RestartDelay
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeatPump/RestartDelay
* ComfoBox/Config/EnergyComsumer/HeatPump/RestartDelay/Set

### Description
Min: 20, Max: 60

Wiedereinschaltverzögerung


## ComfoBox.Config.EnergyComsumer.HeatPump.SwitchingDifferenceStage1
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeatPump/SwitchingDifferenceStage1

### Description
Min: 2, Max: 20

Schaltdifferenz Stufe 1


## ComfoBox.Config.EnergyComsumer.HeizenZone.MaxVorhaltezeitAbsenken
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/MaxVorhaltezeitAbsenken

### Description
Min: 0, Max: 120

Max Vorhaltezeit Absenken


## ComfoBox.Config.EnergyComsumer.HeizenZone.MaxVorhaltezeitHeizen
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/MaxVorhaltezeitHeizen

### Description
Min: 0, Max: 180

Max Vorhaltezeit Heizen


## ComfoBox.Config.EnergyComsumer.HeizenZone.OptimierungHeizschaltzeiten
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/OptimierungHeizschaltzeiten

### Description
Min: 0, Max: 1

Optimierung Heizschaltzeiten:
Keine Funktion = 0
EIN = 1



## ComfoBox.Config.EnergyComsumer.HeizenZone.Raumeinfluss
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/Raumeinfluss
* ComfoBox/Config/EnergyComsumer/HeizenZone/Raumeinfluss/Set

### Description
Min: 0, Max: 150

Raumeinfluss


## ComfoBox.Config.EnergyComsumer.HeizenZone.RaumsollKorrZone
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/RaumsollKorrZone
* ComfoBox/Config/EnergyComsumer/HeizenZone/RaumsollKorrZone/Set

### Description
Min: -5, Max: 5

Raumsoll-Korr Zo


## ComfoBox.Config.EnergyComsumer.HeizenZone.Tagesheizgrenze
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/Tagesheizgrenze
* ComfoBox/Config/EnergyComsumer/HeizenZone/Tagesheizgrenze/Set

### Description
Min: 0, Max: 1

Tagesheizgrenze:
AUS = 0
EIN = 1

Die Tages-Heizgrenzenautomatik ist eine kurzfristig einsetzende Sparfunktion. Wenn bei
Mischerkreisen der Vorlauftemperatursollwert nur noch ca. 3K (Wert gerechnet aus Steilheit der
Heizkennlinie) grösser ist als der Raumtemperatursollwert oder wenn bei direktem Heizkreis der
Rücklauftemperatursollwert unter den Raumtemperatursollwert sinkt, schaltet der Heizbetrieb
aus. Die Grenze kann mit „Tagesheizgrenze Offset“ (3569) verschoben werden, um bei Bauten
mit sehr tiefem Energiebedarf den Ausschaltpunkt zu definieren.


## ComfoBox.Config.EnergyComsumer.HeizenZone.TagesheizgrenzeOffset
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/TagesheizgrenzeOffset
* ComfoBox/Config/EnergyComsumer/HeizenZone/TagesheizgrenzeOffset/Set

### Description
Min: -5, Max: 5

Tagesheizgrenze Offset


## ComfoBox.Config.EnergyComsumer.HeizenZone.VorlaufMaximal
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/VorlaufMaximal
* ComfoBox/Config/EnergyComsumer/HeizenZone/VorlaufMaximal/Set

### Description
Min: 0, Max: 125

Vorlauf Maximal


## ComfoBox.Config.EnergyComsumer.HeizenZone.VorlaufMinimal
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/VorlaufMinimal
* ComfoBox/Config/EnergyComsumer/HeizenZone/VorlaufMinimal/Set

### Description
Min: 0, Max: 99

Vorlauf Minimal


## ComfoBox.Config.EnergyComsumer.HeizenZone.Winterheizgrenze
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/HeizenZone/Winterheizgrenze
* ComfoBox/Config/EnergyComsumer/HeizenZone/Winterheizgrenze/Set

### Description
Min: 0, Max: 10

Winterheizgrenze:
Die Sommer/Winter-Heizgrenzenautomatik ist eine mittelfristig einsetzende Sparfunktion. Wenn
der Raumtemperatursollwert nur noch um den hier eingestellten Wert grösser ist als die
gedämpfte Aussentemperatur (Zeitkonstante 21 Std.), schaltet der Heizbetrieb aus. Die Funktion
wird nur in den automatischen Betriebsarten („Normal/Reduziert“ und „Normal/Frostschutz“)
ausgeführt.


## ComfoBox.Config.EnergyComsumer.Heizkurve.AdaptierterFixpunkt
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Heizkurve/AdaptierterFixpunkt
* ComfoBox/Config/EnergyComsumer/Heizkurve/AdaptierterFixpunkt/Set

### Description
Min: 10, Max: 40

Adaptierter Fixpunkt


## ComfoBox.Config.EnergyComsumer.Heizkurve.AdaptVorlauftempImAuslegepunkt
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Heizkurve/AdaptVorlauftempImAuslegepunkt
* ComfoBox/Config/EnergyComsumer/Heizkurve/AdaptVorlauftempImAuslegepunkt/Set

### Description
Min: 20, Max: 99

Adapt Vorl’temp im Auslegep


## ComfoBox.Config.EnergyComsumer.Heizkurve.AussentempImAuslegepunkt
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Heizkurve/AussentempImAuslegepunkt
* ComfoBox/Config/EnergyComsumer/Heizkurve/AussentempImAuslegepunkt/Set

### Description
Min: -30, Max: 0

Aussentemp im Auslegepunkt


## ComfoBox.Config.EnergyComsumer.Heizkurve.Fixpunkt
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Heizkurve/Fixpunkt
* ComfoBox/Config/EnergyComsumer/Heizkurve/Fixpunkt/Set

### Description
Min: 10, Max: 40

Fixpunkt


## ComfoBox.Config.EnergyComsumer.Heizkurve.Heizkennlinienadaption
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Heizkurve/Heizkennlinienadaption
* ComfoBox/Config/EnergyComsumer/Heizkurve/Heizkennlinienadaption/Set

### Description
Min: 0, Max: 2

Heizkennlinienadaption:
Keine Funktion = 0
Manuell, auto mit Raum'füh = 1
Manuell, Korrektureingabe = 2


## ComfoBox.Config.EnergyComsumer.Heizkurve.VorlauftempImAuslegepunkt
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Heizkurve/VorlauftempImAuslegepunkt
* ComfoBox/Config/EnergyComsumer/Heizkurve/VorlauftempImAuslegepunkt/Set

### Description
Min: 20, Max: 99

Vorlauftemp im Auslegepunkt


## ComfoBox.Config.EnergyComsumer.Protection.KondensatorFrostschutz.KondensatFrostschutz
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondensatFrostschutz
* ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondensatFrostschutz/AsNumber

### Description
Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen. Parameter „Kondensat’frostschutz“ definiert, welcher Fühler für die Schutzfunktion zuständig ist.


## ComfoBox.Config.EnergyComsumer.Protection.KondensatorFrostschutz.KondFrostschutzSchatdiff
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondFrostschutzSchatdiff

### Description
Min: 2, Max: 10

Kond’frostschutztemp


## ComfoBox.Config.EnergyComsumer.Protection.KondensatorFrostschutz.KondFrostschutzTemp
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Protection/KondensatorFrostschutz/KondFrostschutzTemp

### Description
Min: -20, Max: 30

Kond’frostschutztemp


## ComfoBox.Config.EnergyComsumer.Warmwater.EnergieerzAnhebungFühler
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/EnergieerzAnhebungFühler

### Description
Min: 2, Max: 60

Energieerz’anhebung (Fühler)


## ComfoBox.Config.EnergyComsumer.Warmwater.EnergieerzSollThermostat
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/EnergieerzSollThermostat

### Description
Min: 0, Max: 99

Energieerz’soll (Thermostat)


## ComfoBox.Config.EnergyComsumer.Warmwater.Entladeschutz
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Entladeschutz
* ComfoBox/Config/EnergyComsumer/Warmwater/Entladeschutz/Set

### Description
Min: 0, Max: 1

WW-Entladeschutz:
Keine Funktion = 0
EIN = 1



## ComfoBox.Config.EnergyComsumer.Warmwater.Freigabe
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Freigabe
* ComfoBox/Config/EnergyComsumer/Warmwater/Freigabe/Set

### Description
Min: 0, Max: 2

WW-Freigabe:
Nach Schaltuhr = 0
1h vor Zonenbeginn = 1
WW dauernd = 2



## ComfoBox.Config.EnergyComsumer.Warmwater.Legionellenschutzfunktion
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Legionellenschutzfunktion
* ComfoBox/Config/EnergyComsumer/Warmwater/Legionellenschutzfunktion/Set

### Description
Min: 0, Max: 8

Legionellenschutzfunktion:
Keine Funktion = 0
Legionellenschutz am Mo = 1
Legionellenschutz am Di = 2
Legionellenschutz am Mi = 3
Legionellenschutz am Do = 4
Legionellenschutz am Fr = 5
Legionellenschutz am Sa = 6
Legionellenschutz am So = 7
Legionellenschutz täglich = 8




## ComfoBox.Config.EnergyComsumer.Warmwater.Nachlaufzeit
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Nachlaufzeit
* ComfoBox/Config/EnergyComsumer/Warmwater/Nachlaufzeit/Set

### Description
Min: 0, Max: 10

WW-Nachlaufzeit


## ComfoBox.Config.EnergyComsumer.Warmwater.Schaltdifferenz
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Schaltdifferenz
* ComfoBox/Config/EnergyComsumer/Warmwater/Schaltdifferenz/Set

### Description
Min: 1, Max: 10

WW-Schaltdifferenz


## ComfoBox.Config.EnergyComsumer.Warmwater.SollwertMaximal
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/SollwertMaximal
* ComfoBox/Config/EnergyComsumer/Warmwater/SollwertMaximal/Set

### Description
Min: 5, Max: 99

WW-Sollwert Maximal


## ComfoBox.Config.EnergyComsumer.Warmwater.SollwertMaximalWP
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/SollwertMaximalWP

### Description
Min: 0, Max: 58

WW-Sollwert Maximal WP


## ComfoBox.Config.EnergyComsumer.Warmwater.Vorrang
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Vorrang
* ComfoBox/Config/EnergyComsumer/Warmwater/Vorrang/Set

### Description
Min: 0, Max: 2

WW-Vorrang:
Kein Vorrang = 0
Teilvorrang = 1
Voller Vorrang = 2



## ComfoBox.Config.EnergyComsumer.Warmwater.Zwangsladung
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Warmwater/Zwangsladung
* ComfoBox/Config/EnergyComsumer/Warmwater/Zwangsladung/Set

### Description
Min: 0, Max: 1

WW-Zwangsladung
Keine Funktion = 0
Täglich bei erster WW-Ladung = 1


## ComfoBox.Config.EnergyComsumer.Zone.Gebäudeträgheit
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Zone/Gebäudeträgheit
* ComfoBox/Config/EnergyComsumer/Zone/Gebäudeträgheit/Set

### Description
Min: 0, Max: 3

Gebäudeträgheit:
Ohne Trägheit (Testzwecke) = 0
Leichte Bauweise = 1
Normale Bauweise = 2
Schwere Bauweise = 3



## ComfoBox.Config.EnergyComsumer.Zone.Mischerlaufzeit
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Zone/Mischerlaufzeit

### Description
Min: 1, Max: 30

Mischerlaufzeit


## ComfoBox.Config.EnergyComsumer.Zone.NachlaufzeitZonenpumpe
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Zone/NachlaufzeitZonenpumpe

### Description
Min: 0, Max: 30

Nachlaufzeit Zonenpumpe


## ComfoBox.Config.EnergyComsumer.Zone.RaumsollüberhSol
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Zone/RaumsollüberhSol

### Description
Min: 0, Max: 6

Raumsoll’überh Sol


## ComfoBox.Config.EnergyComsumer.Zone.ÜberhöhVorlaufEnergieerz
### MQTT Topics
* ComfoBox/Config/EnergyComsumer/Zone/ÜberhöhVorlaufEnergieerz

### Description
Min: 0, Max: 30

Überhöh Vorlauf/Energieerz


## ComfoBox.Config.EnergyProducer.PrimaryPumpPostRunningTime
### MQTT Topics
* ComfoBox/Config/EnergyProducer/PrimaryPumpPostRunningTime

### Description
Min: 0, Max: 99

Nachlaufzeit Primär: Nach dem Ausschalten des Energieerzeugers wird die Primärpumpe bzw. der Ventilator erst nach der Nachlaufzeit ausgeschaltet.


## ComfoBox.Config.EnergyProducer.PrimaryPumpPreRunningTime
### MQTT Topics
* ComfoBox/Config/EnergyProducer/PrimaryPumpPreRunningTime

### Description
Min: 0, Max: 99

Vorlaufzeit Primär: Bei einer Energieanforderung wird die Primärpumpe bzw. der Ventilator aktiviert und der Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben


## ComfoBox.Config.Freecooling.DeltaTempForSwitching
### MQTT Topics
* ComfoBox/Config/Freecooling/DeltaTempForSwitching
* ComfoBox/Config/Freecooling/DeltaTempForSwitching/Set

### Description
Min: 0, Max: 20

DeltaT für Umsch Pas’kühlen


## ComfoBox.Config.Freecooling.DiffForSwitching
### MQTT Topics
* ComfoBox/Config/Freecooling/DiffForSwitching
* ComfoBox/Config/Freecooling/DiffForSwitching/Set

### Description
Min: 2, Max: 10

Schaltdiff Umsch Pas’kühlen


## ComfoBox.Config.Freecooling.MinTemperature
### MQTT Topics
* ComfoBox/Config/Freecooling/MinTemperature

### Description
Min: 6, Max: 99

Temp min bei Kühlen


## ComfoBox.Config.General.EntlueftungPrimaerkreis
### MQTT Topics
* ComfoBox/Config/General/EntlueftungPrimaerkreis
* ComfoBox/Config/General/EntlueftungPrimaerkreis/Set

### Description
Entlüftung Primärkreis:
Die Funktion definiert eine Zeit zur Entlüftung des Primärkreises (Energiequellen-Kreis). Während
der eingestellten Zeit läuft die Primärpumpe (resp. die Primärpumpen bei mehrstufigen
Wärmepumpen).


## ComfoBox.Config.General.EntlueftungSekundaerkreis
### MQTT Topics
* ComfoBox/Config/General/EntlueftungSekundaerkreis
* ComfoBox/Config/General/EntlueftungSekundaerkreis/Set

### Description
Entlüftung Sek’kreis:
Die Funktion definiert eine Zeit zur Entlüftung des Sekundärkreises (Energieabnahme). Während
der eingestellten Zeit laufen alle Zonenpumpen, sowie die Pufferladepumpe (resp.
Kondensatorpumpe).


## ComfoBox.Config.General.Sommerknick
### MQTT Topics
* ComfoBox/Config/General/Sommerknick

### Description
Min: 0, Max: 1

Sommerknick: Die Sommerintervallschaltung (185) verhindert das Festsitzen der Heizkreispumpen, der Energieerzeugerpumpe und der Mischer im Sommerbetrieb.
0: deaktiviert
1: Sommerkick täglich um 16:00


## ComfoBox.Config.HeatingSystem.AdaptEnergieerzeugerTempImAuslegepunkt
### MQTT Topics
* ComfoBox/Config/HeatingSystem/AdaptEnergieerzeugerTempImAuslegepunkt

### Description
Min: 20, Max: 99

Adapt. Energieerz’temp im Auslegep


## ComfoBox.Config.HeatingSystem.Anlagenfrostschutz
### MQTT Topics
* ComfoBox/Config/HeatingSystem/Anlagenfrostschutz
* ComfoBox/Config/HeatingSystem/Anlagenfrostschutz/Set

### Description
Min: -15, Max: 20

Anlagenfrostschutz: Wenn die gebäudebezogene Aussentemperatur unter den „Anlagefrostschutz“ (187) fällt, werden die Heizkreispumpen aktiviert.


## ComfoBox.Config.HeatingSystem.EnergieerzeugerTempImAuslegepunkt
### MQTT Topics
* ComfoBox/Config/HeatingSystem/EnergieerzeugerTempImAuslegepunkt

### Description
Min: 20, Max: 99

Energieerz’temp im Auslegep


## ComfoBox.Config.PrimaryPump.Max
### MQTT Topics
* ComfoBox/Config/PrimaryPump/Max

### Description
Min: 0, Max: 100

Maximale Pumpenleistung in %


## ComfoBox.Config.PrimaryPump.MaxCooling
### MQTT Topics
* ComfoBox/Config/PrimaryPump/MaxCooling

### Description
Min: 0, Max: 100

Maximale Kühl-Pumpenleistung in %


## ComfoBox.Config.PrimaryPump.MaxMode
### MQTT Topics
* ComfoBox/Config/PrimaryPump/MaxMode

### Description
Min: 0, Max: 1

Y1 Max Mode:
Die Maximalbegrenzung (412, 3614) kann in speziellen Zuständen übersteuert werden (z.B. bei
Frostschutz). Wenn dies nicht erwünscht ist, kann mit diesem Parameter die Maximalbegrenzung
als „Immer aktiv“ definiert werden.
0: Nicht aktiv bei übersteuer
1: Immer aktiv


## ComfoBox.Config.PrimaryPump.Min
### MQTT Topics
* ComfoBox/Config/PrimaryPump/Min

### Description
Min: 0, Max: 100

Minimale Pumpenleistung in %


## ComfoBox.Config.PrimaryPump.MinCooling
### MQTT Topics
* ComfoBox/Config/PrimaryPump/MinCooling

### Description
Min: 0, Max: 100

Minimale Kühl-Pumpenleistung in %


## ComfoBox.Config.PrimaryPump.StoppedMode
### MQTT Topics
* ComfoBox/Config/PrimaryPump/StoppedMode

### Description
Min: 0, Max: 1

Stopp Mode:
Definiert, in welchem Zustand die Pumpe ist, wenn sie auf 'Aus' gestellt ist
0: 0V
1: 1V


## ComfoBox.States.Controller.ControllerType
### MQTT Topics
* ComfoBox/States/Controller/ControllerType

### Description
Genauer Reglertyp


## ComfoBox.States.Controller.ExtensionType
### MQTT Topics
* ComfoBox/States/Controller/ExtensionType

### Description
Angeschlossener Erweiterungstyp


## ComfoBox.States.Controller.InitialOperationDate
### MQTT Topics
* ComfoBox/States/Controller/InitialOperationDate

### Description
Das Inbetriebnahme-Datum der Reglers wird beim Laden der Applikation gesetzt


## ComfoBox.States.Controller.LastParameterChangeDate
### MQTT Topics
* ComfoBox/States/Controller/LastParameterChangeDate

### Description
Datum der letzten Parameteränderung


## ComfoBox.States.Controller.NumberOfParameterChanges
### MQTT Topics
* ComfoBox/States/Controller/NumberOfParameterChanges

### Description
Anzahl der geänderten Parameter seit „Applikation laden…“


## ComfoBox.States.Controller.NumberOfStartUps
### MQTT Topics
* ComfoBox/States/Controller/NumberOfStartUps

### Description
Anzahl Einschaltungen Regler-Speisung


## ComfoBox.States.Controller.OperatingHours
### MQTT Topics
* ComfoBox/States/Controller/OperatingHours

### Description
Betriebsstunden des Reglers


## ComfoBox.States.Controller.SoftwareVersion
### MQTT Topics
* ComfoBox/States/Controller/SoftwareVersion

### Description
Softwareversion


## ComfoBox.States.Counters.ElectricalWarmWaterCycles
### MQTT Topics
* ComfoBox/States/Counters/ElectricalWarmWaterCycles

### Description
Anzahl Einschaltungen WW-elektrisch


## ComfoBox.States.Counters.ElectricalWarmWaterOperatingHours
### MQTT Topics
* ComfoBox/States/Counters/ElectricalWarmWaterOperatingHours

### Description
Betriebsstunden WW-elektrisch


## ComfoBox.States.Counters.HeatPumpOperatingCycles
### MQTT Topics
* ComfoBox/States/Counters/HeatPumpOperatingCycles

### Description
Anzahl Einschaltungen Stufe 1


## ComfoBox.States.Counters.HeatPumpOperatingHours
### MQTT Topics
* ComfoBox/States/Counters/HeatPumpOperatingHours

### Description
Betriebsstunden Stufe 1


## ComfoBox.States.HeatPump.CoolingFlowTemperature
### MQTT Topics
* ComfoBox/States/HeatPump/CoolingFlowTemperature

### Description
Kühlvorlauftemp: Istwert Kühlvorlauf bei stetiger Primärpumpe


## ComfoBox.States.HeatPump.CurrentFlowTemperature
### MQTT Topics
* ComfoBox/States/HeatPump/CurrentFlowTemperature

### Description
WP-Vorlauftemp: Istwert des WP-Vorlauffühlers


## ComfoBox.States.HeatPump.CurrentPower
### MQTT Topics
* ComfoBox/States/HeatPump/CurrentPower

### Description
Aktuelle WP-Leistung


## ComfoBox.States.HeatPump.HeatPumpSetPoint
### MQTT Topics
* ComfoBox/States/HeatPump/HeatPumpSetPoint

### Description
Sollwert der WP-Regelung


## ComfoBox.States.HeatPump.HeatPumpStatus
### MQTT Topics
* ComfoBox/States/HeatPump/HeatPumpStatus
* ComfoBox/States/HeatPump/HeatPumpStatus/AsNumber

### Description
Anzeige des Betriebszustandes der Stufe 1


## ComfoBox.States.HeatPump.MaxFlowTemperature
### MQTT Topics
* ComfoBox/States/HeatPump/MaxFlowTemperature

### Description
WP-Vor’temp Min/Max: Min/Max-Begrenzung auf WP-Vorlauffühler


## ComfoBox.States.HeatPump.PrimaryTemperature
### MQTT Topics
* ComfoBox/States/HeatPump/PrimaryTemperature

### Description
Primärtemperatur: Temperatur im Primärkreis/Solekreis


## ComfoBox.States.HeatPump.SuctionAirTemp
### MQTT Topics
* ComfoBox/States/HeatPump/SuctionAirTemp

### Description
Ansauglufttemp Aktuelle Ansauglufttemperatur


## ComfoBox.States.Inputs.E6SecondRoomTemperature
### MQTT Topics
* ComfoBox/States/Inputs/E6SecondRoomTemperature

### Description
Raumtemperatur 2 E6


## ComfoBox.States.Inputs.E7HpFlowBackTemp
### MQTT Topics
* ComfoBox/States/Inputs/E7HpFlowBackTemp

### Description
WP Rücklauftemperatur E7


## ComfoBox.States.Outputs.R10AdditionalHeater
### MQTT Topics
* ComfoBox/States/Outputs/R10AdditionalHeater

### Description
Zusatzheizung R10


## ComfoBox.States.Outputs.R1FreeCooling
### MQTT Topics
* ComfoBox/States/Outputs/R1FreeCooling

### Description
Passivkühlen R1


## ComfoBox.States.Outputs.R3Verdichter
### MQTT Topics
* ComfoBox/States/Outputs/R3Verdichter

### Description
Verdichter R3


## ComfoBox.States.Outputs.R4ZonePump
### MQTT Topics
* ComfoBox/States/Outputs/R4ZonePump

### Description
Heizkreispumpe R4


## ComfoBox.States.Outputs.R5WarmWater
### MQTT Topics
* ComfoBox/States/Outputs/R5WarmWater

### Description
Warmwasser R5


## ComfoBox.States.Outputs.R7ElectricalWarmWater
### MQTT Topics
* ComfoBox/States/Outputs/R7ElectricalWarmWater

### Description
Warmwasser elektrisch R7


## ComfoBox.States.Outputs.Y1PrimaryPump
### MQTT Topics
* ComfoBox/States/Outputs/Y1PrimaryPump
* ComfoBox/States/Outputs/Y1PrimaryPump/Set

### Description
Primärpumpe Y1


## ComfoBox.States.Outputs.Y2
### MQTT Topics
* ComfoBox/States/Outputs/Y2
* ComfoBox/States/Outputs/Y2/Set

### Description
Y2


## ComfoBox.States.WarmWater.CurrentTargetTemperature
### MQTT Topics
* ComfoBox/States/WarmWater/CurrentTargetTemperature

### Description
Aktuell gültiger WW-Sollwert


## ComfoBox.States.WarmWater.Temperature
### MQTT Topics
* ComfoBox/States/WarmWater/Temperature

### Description
Istwert Warmwasser


## ComfoBox.States.Zone.CalculatedFlowTemperature
### MQTT Topics
* ComfoBox/States/Zone/CalculatedFlowTemperature

### Description
Berchnete Soll - Vorlauftemperatur


## ComfoBox.States.Zone.CurrentSetPoint
### MQTT Topics
* ComfoBox/States/Zone/CurrentSetPoint

### Description
Aktueller Sollwert Raum


## ComfoBox.States.Zone.OutdoorTemperature
### MQTT Topics
* ComfoBox/States/Zone/OutdoorTemperature

### Description
Aussentemperatur


## ComfoBox.States.Zone.OutdoorTemperatureBuilding
### MQTT Topics
* ComfoBox/States/Zone/OutdoorTemperatureBuilding

### Description
ebäudebezogene Aussentemperatur


## ComfoBox.States.Zone.RoomTemperature
### MQTT Topics
* ComfoBox/States/Zone/RoomTemperature

### Description
Istwert Raum


## ComfoBox.Tests.OutputsInactive
### MQTT Topics
* ComfoBox/Tests/OutputsInactive
* ComfoBox/Tests/OutputsInactive/Set

### Description
Ausgänge inaktiv


## ComfoBox.Tests.R4
### MQTT Topics
* ComfoBox/Tests/R4
* ComfoBox/Tests/R4/Set

### Description
Ausgang R4


## ComfoBox.Tests.Y1
### MQTT Topics
* ComfoBox/Tests/Y1
* ComfoBox/Tests/Y1/Set

### Description
Ausgang Y1


## ComfoBox.Tests.Y2
### MQTT Topics
* ComfoBox/Tests/Y2
* ComfoBox/Tests/Y2/Set

### Description
Ausgang Y2


## ComfoBox.Warmwater.DoHeatWaterNow
### MQTT Topics
* ComfoBox/Warmwater/DoHeatWaterNow
* ComfoBox/Warmwater/DoHeatWaterNow/Set

### Description
Einmalige Ladung des WW-Speichers. Ungeachtet der Schaltuhr kann durch aktivieren dieser Funktion eine Ladung des WW-Speichers erzwungen werden.


## ComfoBox.Warmwater.SetPointFrost
### MQTT Topics
* ComfoBox/Warmwater/SetPointFrost
* ComfoBox/Warmwater/SetPointFrost/Set

### Description
WW Sollwert Frost


## ComfoBox.Warmwater.SetPointLegio
### MQTT Topics
* ComfoBox/Warmwater/SetPointLegio
* ComfoBox/Warmwater/SetPointLegio/Set

### Description
WW Sollwert Legionellen


## ComfoBox.Warmwater.SetPointNormal
### MQTT Topics
* ComfoBox/Warmwater/SetPointNormal
* ComfoBox/Warmwater/SetPointNormal/Set

### Description
WW Sollwert normal


## ComfoBox.Warmwater.SetPointReduced
### MQTT Topics
* ComfoBox/Warmwater/SetPointReduced
* ComfoBox/Warmwater/SetPointReduced/Set

### Description
WW Sollwert reduziert


## ComfoBox.Zone.CalibrateTemperature
### MQTT Topics
* ComfoBox/Zone/CalibrateTemperature
* ComfoBox/Zone/CalibrateTemperature/Set

### Description
Um eine genauere Regelung zu ermöglichen, kann hier die reale, mit einem genauen Messgerät ermittelte Raumtemperatur eingestellt werden.


## ComfoBox.Zone.EcoTime
### MQTT Topics
* ComfoBox/Zone/EcoTime
* ComfoBox/Zone/EcoTime/Set

### Description
Die Funktion wird gestartet, indem die gewünschte Dauer eingestellt wird. Während der eingestellten Zeit ist dann unabhängig der Schaltuhr der Raumsollwert „Reduziert“ bzw. „Frostschutz“ gültig(abhängig von der Betriebsart).


## ComfoBox.Zone.HeatingCoolingMode
### MQTT Topics
* ComfoBox/Zone/HeatingCoolingMode
* ComfoBox/Zone/HeatingCoolingMode/Set
* ComfoBox/Zone/HeatingCoolingMode/AsNumber
* ComfoBox/Zone/HeatingCoolingMode/AsNumber/Set

### Description
Betriebsart Heizen und Kühlen


## ComfoBox.Zone.ManualAdoption
### MQTT Topics
* ComfoBox/Zone/ManualAdoption
* ComfoBox/Zone/ManualAdoption/Set

### Description
Die Adaption der Heizkennlinie korrigiert die Heizkurve bei der aktuellen Aussentemperatur. Die Korrektur sollte jeweils bei tiefer und bei hoher Aussentemperatur durchgeführt werden. Diese Funktion kann nur einmal täglich aufgerufen werden.


## ComfoBox.Zone.OperationMode
### MQTT Topics
* ComfoBox/Zone/OperationMode
* ComfoBox/Zone/OperationMode/Set
* ComfoBox/Zone/OperationMode/AsNumber
* ComfoBox/Zone/OperationMode/AsNumber/Set

### Description
Betriebsart


## ComfoBox.Zone.PartyTime
### MQTT Topics
* ComfoBox/Zone/PartyTime
* ComfoBox/Zone/PartyTime/Set

### Description
Die Funktion wird gestartet, indem die gewünschte Dauer eingestellt wird. Während der eingestellten Zeit ist dann unabhängig der Schaltuhr der Raumsollwert „Normal“ gültig.


## ComfoBox.Zone.SetPointFrost
### MQTT Topics
* ComfoBox/Zone/SetPointFrost
* ComfoBox/Zone/SetPointFrost/Set

### Description
Min: 5, Max: 30

Raumsollwert Frost 5.0..30.0


## ComfoBox.Zone.SetPointNormal
### MQTT Topics
* ComfoBox/Zone/SetPointNormal
* ComfoBox/Zone/SetPointNormal/Set

### Description
Min: 5, Max: 30

Raumsollwert normal 5.0..30.0


## ComfoBox.Zone.SetPointReduced
### MQTT Topics
* ComfoBox/Zone/SetPointReduced
* ComfoBox/Zone/SetPointReduced/Set

### Description
Min: 5, Max: 30

Raumsollwert reduziert 5.0..30.0

