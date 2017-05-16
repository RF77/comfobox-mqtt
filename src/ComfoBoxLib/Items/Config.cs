// /*******************************************************************************
//  * Copyright (c) 2016 by RF77 (https://github.com/RF77)
//  * All rights reserved. This program and the accompanying materials
//  * are made available under the terms of the Eclipse Public License v1.0
//  * which accompanies this distribution, and is available at
//  * http://www.eclipse.org/legal/epl-v10.html
//  *
//  * Contributors:
//  *    RF77 - initial API and implementation and/or initial documentation
//  *******************************************************************************/ 

using System.ComponentModel;
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Items.Enums;
using ComfoBoxLib.Properties;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    [Description(@"Konfiguration")]
    public class Config
    {
        [Description(@"Generelle Anlageeinstellungen")]
        public class General
        {
            /// <summary>
            ///     Die Sommerintervallschaltung (185) verhindert das Festsitzen der Heizkreispumpen, der
            ///     Energieerzeugerpumpe und der Mischer im Sommerbetrieb.
            ///     0: Funktion deaktiviert
            ///     1: Sommerkick täglich um 16:00
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Sommerknick: Die Sommerintervallschaltung (185) verhindert das Festsitzen der Heizkreispumpen, der Energieerzeugerpumpe und der Mischer im Sommerbetrieb.
* 0: deaktiviert
* 1: Sommerkick täglich um 16:00")]
            public AnalogValue Sommerknick => new AnalogValue(185, "", 0, 1) {IsReadOnly = !Settings.Default.ExpertMode}
                ;

            /// <summary>
            ///     Entlüftung Primärkreis:
            ///     Die Funktion definiert eine Zeit zur Entlüftung des Primärkreises(Energiequellen-Kreis). Während
            ///     der eingestellten Zeit läuft die Primärpumpe(resp.die Primärpumpen bei mehrstufigen
            ///     Wärmepumpen).
            /// </summary>
            [Secondary]
            [Description(@"Entlüftung Primärkreis:
Die Funktion definiert eine Zeit zur Entlüftung des Primärkreises (Energiequellen-Kreis). Während
der eingestellten Zeit läuft die Primärpumpe (resp. die Primärpumpen bei mehrstufigen
Wärmepumpen).")]
            public AnalogValue EntlueftungPrimaerkreis => new AnalogValue(3398, "h");

            /// <summary>
            ///     Entlüftung Sek’kreis
            ///     Die Funktion definiert eine Zeit zur Entlüftung des Sekundärkreises(Energieabnahme). Während
            ///     der eingestellten Zeit laufen alle Zonenpumpen, sowie die Pufferladepumpe(resp.
            ///     Kondensatorpumpe).
            /// </summary>
            [Secondary]
            [Description(@"Entlüftung Sek’kreis:
Die Funktion definiert eine Zeit zur Entlüftung des Sekundärkreises (Energieabnahme). Während
der eingestellten Zeit laufen alle Zonenpumpen, sowie die Pufferladepumpe (resp.
Kondensatorpumpe).")]
            public AnalogValue EntlueftungSekundaerkreis => new AnalogValue(3400, "h");
        }

        [Description(@"Einstellung für Solekreispumpe")]
        public class PrimaryPump
        {
            /// <summary>
            ///     Minimale Pumpenleistung in %
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Minimale Pumpenleistung in %")]
            public AnalogValue Min => new AnalogValue(411, "%", 0, 100) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Maximale Pumpenleistung in %
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Maximale Pumpenleistung in %")]
            public AnalogValue Max => new AnalogValue(412, "%", 0, 100) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Minimale Kühl-Pumpenleistung in %
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Minimale Kühl-Pumpenleistung in %")]
            public AnalogValue MinCooling
                => new AnalogValue(3613, "%", 0, 100) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Maximale Kühl-Pumpenleistung in %
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Maximale Kühl-Pumpenleistung in %")]
            public AnalogValue MaxCooling
                => new AnalogValue(3614, "%", 0, 100) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Y1 Max Mode:
            ///     Die Maximalbegrenzung(412, 3614) kann in speziellen Zuständen übersteuert werden(z.B.bei
            ///     Frostschutz). Wenn dies nicht erwünscht ist, kann mit diesem Parameter die Maximalbegrenzung
            ///     als „Immer aktiv“ definiert werden.
            ///     0: Nicht aktiv bei übersteuer
            ///     1: Immer aktiv
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Y1 Max Mode:
Die Maximalbegrenzung (412, 3614) kann in speziellen Zuständen übersteuert werden (z.B. bei
Frostschutz). Wenn dies nicht erwünscht ist, kann mit diesem Parameter die Maximalbegrenzung
als „Immer aktiv“ definiert werden.
* 0: Nicht aktiv bei übersteuer
* 1: Immer aktiv")]
            public AnalogValue MaxMode => new AnalogValue(3232, "", 0, 1) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Stopp Mode:
            ///     Definiert, in welchem Zustand die Pumpe ist, wenn sie auf 'Aus' gestellt ist
            ///     0: 0V
            ///     1: 1V
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Stopp Mode:
Definiert, in welchem Zustand die Pumpe ist, wenn sie auf 'Aus' gestellt ist
* 0: 0V
* 1: 1V")]
            public AnalogValue StoppedMode
                => new AnalogValue(3096, "", 0, 1) {IsReadOnly = !Settings.Default.ExpertMode};
        }


        [Description(@"Freecooling Einstellungen")]
        public class Freecooling
        {
            /// <summary>
            ///     DeltaT für Umsch Pas’kühlen
            /// </summary>
            [Secondary]
            [Description(
                @"DeltaT für Umsch Pas’kühlen"
                )]
            public AnalogValue DeltaTempForSwitching => new AnalogValue(574, "K", 0, 20);

            /// <summary>
            ///     Schaltdiff Umsch Pas’kühlen
            /// </summary>
            [Secondary]
            [Description(
                @"Schaltdiff Umsch Pas’kühlen"
                )]
            public AnalogValue DiffForSwitching => new AnalogValue(575, "K", 2, 10);

            /// <summary>
            ///     Temp min bei Kühlen
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Temp min bei Kühlen"
                )]
            public AnalogValue MinTemperature
                => new AnalogValue(576, "°C", 6, 99) {IsReadOnly = !Settings.Default.ExpertMode};
        }


        [Description(@"Comfofond-L Einstellungen")]
        public class Comfofond
        {
            /// <summary>
            ///     Sollwert Kühlung
            ///     Ab dieser Temperatur wird die Luft über das Comfofond-L gekühlt,
            ///     falls der Raumsollwert der Lüftung tiefer liegt
            /// </summary>
            [Secondary]
            [Description(
                @"Sollwert Kühlung: Ab dieser Temperatur wird die Luft über das Comfofond-L gekühlt, falls der Raumsollwert der Lüftung tiefer liegt"
                )]
            public AnalogValue CoolingSetPoint => new AnalogValue(3441, "°C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Kühlung über das Comfofond-L
            ///     z.B. Sollwert ist bei 22°C
            ///     -> falls Kühlung aus, wird sie erst >24°C eingeschaltet
            ///     -> falls Kühlung ein, wird sie erst kleiner 20°C ausgeschaltet
            /// </summary>
            [Secondary]
            [Description(
                @"Hysterese beim Ein- und Ausschalten der Kühlung über das Comfofond-L. z.B. Sollwert ist bei 22°C
* -> falls Kühlung aus, wird sie erst >24°C eingeschaltet
* -> falls Kühlung ein, wird sie erst kleiner 20°C ausgeschaltet")]
            public AnalogValue CoolingHysteresis => new AnalogValue(3431, "K", 2, 20);

            /// <summary>
            ///     Sollwert Heizung
            ///     Ab dieser Temperatur wird die Luft über das Comfofond-L im Winter gewärmt,
            /// </summary>
            [Secondary]
            [Description(@"Sollwert Heizung: Ab dieser Temperatur wird die Luft über das Comfofond-L im Winter gewärmt")
            ]
            public AnalogValue HeatingSetPoint => new AnalogValue(3442, "°C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Heizung über das Comfofond-L
            ///     z.B. Sollwert ist bei 2°C
            ///     -> falls Heizung aus, wird sie erst unter 0°C eingeschaltet
            ///     -> falls Heizung ein, wird sie erst oberhalb 4°C ausgeschaltet
            /// </summary>
            [Secondary]
            [Description(
                @"Hysterese beim Ein- und Ausschalten der Heizung über das Comfofond-L z.B. Sollwert ist bei 2°C;
* -> falls Heizung aus, wird sie erst unter 0°C eingeschaltet
* -> falls Heizung ein, wird sie erst oberhalb 4°C ausgeschaltet")]
            public AnalogValue HeatingHysteresis => new AnalogValue(3452, "K", 2, 20);
        }

        /// <summary>
        ///     Heizen System
        /// </summary>
        [Description(@"Heizen System")]
        public class HeatingSystem
        {
            /// <summary>
            ///     Energieerz’temp im Auslegep
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Energieerz’temp im Auslegep")]
            public AnalogValue EnergieerzeugerTempImAuslegepunkt
                => new AnalogValue(163, "°C", 20, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Adapt. Energieerz’temp im Auslegep
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Adapt. Energieerz’temp im Auslegep")]
            public AnalogValue AdaptEnergieerzeugerTempImAuslegepunkt
                => new AnalogValue(166, "°C", 20, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Anlagenfrostschutz
            ///     Wenn die gebäudebezogene Aussentemperatur unter den „Anlagefrostschutz“ (187) fällt werden
            ///     die Heizkreispumpen aktiviert.
            /// </summary>
            [Secondary]
            [Description(
                @"Anlagenfrostschutz: Wenn die gebäudebezogene Aussentemperatur unter den „Anlagefrostschutz“ (187) fällt, werden die Heizkreispumpen aktiviert."
                )]
            public AnalogValue Anlagenfrostschutz => new AnalogValue(187, "°C", -15, 20);
        }


        /// <summary>
        ///     Energieerzeuger parametrieren
        /// </summary>
        [Description(@"Energieerzeuger parametrieren")]
        public class EnergyProducer
        {
            /// <summary>
            ///     Vorlaufzeit Primär
            ///     Bei einer Energieanforderung wird die Primärpumpe bzw. der Ventilator aktiviert und der
            ///     Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Vorlaufzeit Primär: Bei einer Energieanforderung wird die Primärpumpe bzw. der Ventilator aktiviert und der Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben"
                )]
            public AnalogValue PrimaryPumpPreRunningTime
                => new AnalogValue(880, "min", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Nachlaufzeit Primär
            ///     Nach dem Ausschalten des Energieerzeugers wird die Primärpumpe bzw. der Ventilator erst
            ///     nach der Nachlaufzeit ausgeschaltet.
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Nachlaufzeit Primär: Nach dem Ausschalten des Energieerzeugers wird die Primärpumpe bzw. der Ventilator erst nach der Nachlaufzeit ausgeschaltet."
                )]
            public AnalogValue PrimaryPumpPostRunningTime
                => new AnalogValue(881, "min", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};
        }

        [Description(@"Energieabnehmer parametrieren")]
        public class EnergyComsumer
        {
            [Description(@"Zone parametrieren")]
            public class Zone
            {
                /// <summary>
                ///     Gebäudeträgheit:
                ///     Ohne Trägheit (Testzwecke) = 0
                ///     Leichte Bauweise = 1
                ///     Normale Bauweise = 2
                ///     Schwere Bauweise = 3
                /// </summary>
                [Secondary]
                [Description(
                    @"Gebäudeträgheit:
* Ohne Trägheit (Testzwecke) = 0
* Leichte Bauweise = 1
* Normale Bauweise = 2
* Schwere Bauweise = 3
"
                    )]
                public AnalogValue Gebäudeträgheit => new AnalogValue(170, "", 0, 3);

                /// <summary>
                ///     Mischerlaufzeit
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Mischerlaufzeit")]
                public AnalogValue Mischerlaufzeit
                    => new AnalogValue(113, "min", 1, 30) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Überhöh Vorlauf/Energieerz
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Überhöh Vorlauf/Energieerz")]
                public AnalogValue ÜberhöhVorlaufEnergieerz
                    => new AnalogValue(168, "K", 0, 30) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Nachlaufzeit Zonenpumpe
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Nachlaufzeit Zonenpumpe")]
                public AnalogValue NachlaufzeitZonenpumpe
                    => new AnalogValue(186, "min", 0.0f, 30.0f) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Raumsoll’überh Sol
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Raumsoll’überh Sol")]
                public AnalogValue RaumsollüberhSol
                    => new AnalogValue(462, "K", 0, 6) {IsReadOnly = !Settings.Default.ExpertMode};

            }

            [Description(@"Heizkurve Zone parametrieren:
Die normierte Steilheit verläuft durch „Fixpunkt“ (160) und
Auslegepunkt. Der Auslegepunkt wird durch die „Aussentemp
im Auslegepunkt“ (161) und „Vorlauftemp im Auslegepunkt“
(162) festgelegt. Die Heizkennlinienadaption erlaubt die
Korrektur der definierten Heizkennlinie. Wenn ein Raumfühler
vorhanden ist, kann die Adaption automatisch erfolgen. In
beiden Situationen (manuell und automatisch) erfolgt die
Korrektur bei Aussentemperaturen unter 5°C beim
Auslegepunkt, bei Aussentemperaturen über 5°C wird die
Krümmung der Heizkennlinie verändert. Der korrigierte
Auslegepunkt ist in „Adaptierter Fixpunkt“ (164) und „Adapt Vorl’temp im Auslegep“ (165)
ersichtlich.")]
            public class Heizkurve
            {
                /// <summary>
                ///     Fixpunkt
                /// </summary>
                [Secondary]
                [Description(@"Fixpunkt")]
                public AnalogValue Fixpunkt => new AnalogValue(160, "°C", 10, 40);

                /// <summary>
                ///     Aussentemp im Auslegepunkt
                /// </summary>
                [Secondary]
                [Description(@"Aussentemp im Auslegepunkt")]
                public AnalogValue AussentempImAuslegepunkt => new AnalogValue(161, "°C", -30, 0);

                /// <summary>
                ///     Vorlauftemp im Auslegepunkt
                /// </summary>
                [Secondary]
                [Description(@"Vorlauftemp im Auslegepunkt")]
                public AnalogValue VorlauftempImAuslegepunkt => new AnalogValue(162, "°C", 20, 99);

                /// <summary>
                ///     Adaptierter Fixpunkt
                /// </summary>
                [Secondary]
                [Description(@"Adaptierter Fixpunkt")]
                public AnalogValue AdaptierterFixpunkt => new AnalogValue(164, "°C", 10, 40);

                /// <summary>
                ///     Adapt Vorl’temp im Auslegep
                /// </summary>
                [Secondary]
                [Description(@"Adapt Vorl’temp im Auslegep")]
                public AnalogValue AdaptVorlauftempImAuslegepunkt => new AnalogValue(165, "°C", 20, 99);

                /// <summary>
                ///     Heizkennlinienadaption:
                ///     Keine Funktion = 0
                ///     Manuell, auto mit Raum'füh = 1
                ///     Manuell, Korrektureingabe = 2
                /// </summary>
                [Secondary]
                [Description(@"Heizkennlinienadaption:
* Keine Funktion = 0
* Manuell, auto mit Raum'füh = 1
* Manuell, Korrektureingabe = 2")]
                public AnalogValue Heizkennlinienadaption => new AnalogValue(167, "", 0, 2);

            }

            [Description(@"Heizen Zone")]
            public class HeizenZone
            {
                /// <summary>
                ///     Tagesheizgrenze
                /// </summary>
                [Secondary]
                [Description(@"Tagesheizgrenze:
* AUS = 0
* EIN = 1

Die Tages-Heizgrenzenautomatik ist eine kurzfristig einsetzende Sparfunktion. Wenn bei
Mischerkreisen der Vorlauftemperatursollwert nur noch ca. 3K (Wert gerechnet aus Steilheit der
Heizkennlinie) grösser ist als der Raumtemperatursollwert oder wenn bei direktem Heizkreis der
Rücklauftemperatursollwert unter den Raumtemperatursollwert sinkt, schaltet der Heizbetrieb
aus. Die Grenze kann mit „Tagesheizgrenze Offset“ (3569) verschoben werden, um bei Bauten
mit sehr tiefem Energiebedarf den Ausschaltpunkt zu definieren.")]
                public AnalogValue Tagesheizgrenze => new AnalogValue(180, "", 0, 1);

                /// <summary>
                ///     Tagesheizgrenze Offset
                /// </summary>
                [Secondary]
                [Description(@"Tagesheizgrenze Offset")]
                public AnalogValue TagesheizgrenzeOffset => new AnalogValue(3569, "K", -5, 5);

                /// <summary>
                ///     Winterheizgrenze
                /// </summary>
                [Secondary]
                [Description(@"Winterheizgrenze:
Die Sommer/Winter-Heizgrenzenautomatik ist eine mittelfristig einsetzende Sparfunktion. Wenn
der Raumtemperatursollwert nur noch um den hier eingestellten Wert grösser ist als die
gedämpfte Aussentemperatur (Zeitkonstante 21 Std.), schaltet der Heizbetrieb aus. Die Funktion
wird nur in den automatischen Betriebsarten („Normal/Reduziert“ und „Normal/Frostschutz“)
ausgeführt.")]
                public AnalogValue Winterheizgrenze => new AnalogValue(181, "K", 0.0f, 10.0f);

                /// <summary>
                ///     Raumeinfluss
                /// </summary>
                [Secondary]
                [Description(@"Raumeinfluss")]
                public AnalogValue Raumeinfluss => new AnalogValue(183, "%", 0, 150);

                /// <summary>
                ///     Optimierung Heizschaltzeiten
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Optimierung Heizschaltzeiten:
* Keine Funktion = 0
* EIN = 1
")]
                public AnalogValue OptimierungHeizschaltzeiten
                    => new AnalogValue(172, "", 0, 1) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Max Vorhaltezeit Heizen
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Max Vorhaltezeit Heizen")]
                public AnalogValue MaxVorhaltezeitHeizen
                    => new AnalogValue(173, "min", 0, 180) {IsReadOnly = !Settings.Default.ExpertMode};


                /// <summary>
                ///     Max Vorhaltezeit Absenken
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Max Vorhaltezeit Absenken")]
                public AnalogValue MaxVorhaltezeitAbsenken
                    => new AnalogValue(174, "min", 0, 120) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Vorlauf Maximal
                /// </summary>
                [Secondary]
                [Description(@"Vorlauf Maximal")]
                public AnalogValue VorlaufMaximal => new AnalogValue(154, "°C", 0, 125);

                /// <summary>
                ///     Vorlauf Minimal
                /// </summary>
                [Secondary]
                [Description(@"Vorlauf Minimal")]
                public AnalogValue VorlaufMinimal => new AnalogValue(153, "°C", 0, 99);

                /// <summary>
                ///     Raumsoll-Korr Zo
                /// </summary>
                [Secondary]
                [Description(@"Raumsoll-Korr Zo")]
                public AnalogValue RaumsollKorrZone => new AnalogValue(3576, "K", -5.0f, 5.0f);
            }

            [Description(@"Kühlen Zone:
Kühlen wird aktiviert, sobald die
Gebäudebezogene Aussentemperatur über
die Sommerkühlgrenze steigt. Die
Sommerkühlgrenze setzt sich aus dem
aktuellen Raumsollwert und dem Wert in
„Sommerkühlgrenze“ (473) zusammen. Der
Raumsollwert wird anhand der
Aussentemperatur mit dem Faktor in
„Steilheit Raumsoll-Schiebung“ (475)
angehoben. Der Vorlaufsollwert für Kühlen
ist abhängig von der gemessenen
Raumist/soll Abweichung und begrenzt
durch die Gerade zwischen den Werten in
„Min Vorl’temp Kühlen (20°C)“ (477) und „Min Vorl’temp Kühlen (40°C)“ (478) und der „Abs min
Vorlauftemp Kühlen“ (479). Die Raumsollwerte können für die Kühlfunktion mit
„Kühlraumsoll’abst“ (normal: 3562, reduziert: 863, standby: 864) angehoben werden. Für
Gebäude mit stark unterschiedlichen Gegebenheiten, kann ein zusätzlicher Raumfühler
eingesetzt werden. Dabei wird ein Fühler für Heizen, der andere für Kühlen verwendet. Die
Zuordnung erfolgt über „Umschaltung Raumfühler“ (3085).")]
            public class CoolingZone
            {
                /// <summary>
                ///     Kühlen
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Kühlen:
* Keine Funktion = 0
* Kühlen = 1
* Nur Kühlen = 3")]
                public AnalogValue Cooling => new AnalogValue(470, "", 0, 3) {IsReadOnly = !Settings.Default.ExpertMode}
                    ;

                /// <summary>
                ///     Sommerkühlgrenze
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Sommerkühlgrenze")]
                public AnalogValue Sommerkühlgrenze
                    => new AnalogValue(473, "K", -10, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Kühlraumsoll’abst normal
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Kühlraumsoll’abst normal")]
                public AnalogValue KühlraumsollAbstNormal
                    => new AnalogValue(3562, "K", 0, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Kühlraumsoll’abst reduziert
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Kühlraumsoll’abst reduziert")]
                public AnalogValue KühlraumsollAbstReduziert
                    => new AnalogValue(863, "K", 0, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Kühlraumsoll’abst standby
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Kühlraumsoll’abst standby")]
                public AnalogValue KühlraumsollAbstStandby
                    => new AnalogValue(864, "K", 0, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Fixpunkt Raumsoll Küh (20°C)
                /// </summary>
                [Secondary]
                [Description(@"Fixpunkt Raumsoll Küh (20°C)")]
                public AnalogValue FixpunktRaumsoll => new AnalogValue(474, "°C", 10, 30);

                /// <summary>
                ///     Steilheit Raumsoll-Schiebung
                /// </summary>
                [Secondary]
                [Description(@"Steilheit Raumsoll-Schiebung")]
                public AnalogValue SteilheitRaumsollSchiebung => new AnalogValue(475, "K", 0, 5);

                /// <summary>
                ///     Min Vorl’temp Kühlen (20°C)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Min Vorl’temp Kühlen (20°C)")]
                public AnalogValue MinVorlTemp20
                    => new AnalogValue(477, "°C", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Min Vorl’temp Kühlen (40°C)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Min Vorl’temp Kühlen (40°C)")]
                public AnalogValue MinVorlTemp40
                    => new AnalogValue(478, "°C", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Abs min Vorlauftemp Kühlen
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Abs min Vorlauftemp Kühlen")]
                public AnalogValue AbsMinVorlauftemp
                    => new AnalogValue(479, "°C", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Raumeinfluss bei Kühlen
                /// </summary>
                [Secondary]
                [Description(@"Raumeinfluss bei Kühlen")]
                public AnalogValue RaumeinflussBeiKühlen => new AnalogValue(578, "%", 100, 999);

                /// <summary>
                ///     Umschaltung Raumfühler
                /// </summary>
                [Secondary]
                [Description(@"Umschaltung Raumfühler:
* Keine Funktion = 0
* Heiz'n mit Tr1, Kühl'n mit Tr2 = 1
* Heiz'n mit Tr2, Kühl'n mit Tr1 = 2
")]
                public AnalogValue UmschaltungRaumfühler => new AnalogValue(3085, "", 0, 2);
            }

            public class Warmwater
            {
                /// <summary>
                ///     Legionellenschutzfunktion
                /// </summary>
                [Secondary]
                [Description(@"Legionellenschutzfunktion:
* Keine Funktion = 0
* Legionellenschutz am Mo = 1
* Legionellenschutz am Di = 2
* Legionellenschutz am Mi = 3
* Legionellenschutz am Do = 4
* Legionellenschutz am Fr = 5
* Legionellenschutz am Sa = 6
* Legionellenschutz am So = 7
* Legionellenschutz täglich = 8

")]
                public AnalogValue Legionellenschutzfunktion => new AnalogValue(192, "", 0, 8);

                /// <summary>
                ///     WW-Zwangsladung
                /// </summary>
                [Secondary]
                [Description(@"WW-Zwangsladung
* Keine Funktion = 0
* Täglich bei erster WW-Ladung = 1")]
                public AnalogValue Zwangsladung
                    => new AnalogValue(199, "", 0, 1);

                /// <summary>
                ///     WW-Vorrang
                /// </summary>
                [Secondary]
                [Description(@"WW-Vorrang:
* Kein Vorrang = 0
* Teilvorrang = 1
* Voller Vorrang = 2
")]
                public AnalogValue Vorrang
                    => new AnalogValue(196, "", 0, 2);

                /// <summary>
                ///     Energieerz’anhebung (Fühler)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Energieerz’anhebung (Fühler)")]
                public AnalogValue EnergieerzAnhebungFühler
                    => new AnalogValue(193, "K", 2, 60) { IsReadOnly = !Settings.Default.ExpertMode };

                /// <summary>
                ///     Energieerz’soll (Thermostat)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Energieerz’soll (Thermostat)")]
                public AnalogValue EnergieerzSollThermostat
                    => new AnalogValue(194, "°C", 0, 99) { IsReadOnly = !Settings.Default.ExpertMode };

                /// <summary>
                ///     WW-Nachlaufzeit
                /// </summary>
                [Secondary]
                [Description(@"WW-Nachlaufzeit")]
                public AnalogValue Nachlaufzeit => new AnalogValue(197, "min", 0, 10);

                /// <summary>
                ///     WW-Freigabe
                /// </summary>
                [Secondary]
                [Description(@"WW-Freigabe:
* Nach Schaltuhr = 0
* 1h vor Zonenbeginn = 1
* WW dauernd = 2
")]
                public AnalogValue Freigabe => new AnalogValue(175, "", 0, 2);

                /// <summary>
                ///     WW-Sollwert Maximal
                /// </summary>
                [Secondary]
                [Description(@"WW-Sollwert Maximal")]
                public AnalogValue SollwertMaximal
                    => new AnalogValue(190, "°C", 5, 99);

                /// <summary>
                ///     WW-Sollwert Maximal WP
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"WW-Sollwert Maximal WP")]
                public AnalogValue SollwertMaximalWP
                    => new AnalogValue(447, "°C", 0, 58) { IsReadOnly = !Settings.Default.ExpertMode };

                /// <summary>
                ///     WW-Schaltdifferenz
                /// </summary>
                [Secondary]
                [Description(@"WW-Schaltdifferenz")]
                public AnalogValue Schaltdifferenz
                    => new AnalogValue(191, "K", 1, 10);

                /// <summary>
                ///     WW-Entladeschutz
                /// </summary>
                [Secondary]
                [Description(@"WW-Entladeschutz:
* Keine Funktion = 0
* EIN = 1
")]
                public AnalogValue Entladeschutz => new AnalogValue(157, "", 0, 1);
               
            }


            /// <summary>
            ///     WP, Schutz
            /// </summary>
            [Description(
                @"WP, Schutz -> Zweifelhaft, ob verwendet? Viele Schutzfunktionen sind auf 'Keine Funktion' eingestellt"
                )]
            public class Protection
            {
                /// <summary>
                ///     Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen.Parameter
                ///     „Kondensat’frostschutz“ (494) definiert, welcher Fühler für die Schutzfunktion zuständig ist.Wird
                ///     die Frostschutztemperatur(495) um die halbe Schaltdifferenz(496) unterschritten, wird der
                ///     Kompressor gestoppt und(je nach Einstellung) die Zusatzheizung gestartet(497). Die
                ///     Kondensatorpumpe läuft in jedem Fall weiter.Der Parameter „WP-Frostschutz Mode“ (498)
                ///     definiert, ob die WP wieder selbstständig starten kann oder ob eine manuelle Entriegelung nötig
                ///     ist(siehe Beschreibung „WP-Frostschutz“).
                /// </summary>
                public class KondensatorFrostschutz
                {
                    /// <summary>
                    ///     Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen. Parameter „Kondensat’frostschutz“
                    ///     definiert, welcher Fühler für die Schutzfunktion zuständig ist.
                    /// </summary>
                    [Secondary]
                    [ExpertMode]
                    [Description(
                        @"Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen. Parameter „Kondensat’frostschutz“ definiert, welcher Fühler für die Schutzfunktion zuständig ist."
                        )]
                    public EnumValue<KondensatFrostschutzEnum?> KondensatFrostschutz
                        => new EnumValue<KondensatFrostschutzEnum?>(494) {IsReadOnly = !Settings.Default.ExpertMode};

                    //TODO: 1984


                    /// <summary>
                    ///     Kond’frostschutztemp
                    /// </summary>
                    [Secondary]
                    [ExpertMode]
                    [Description(@"Kond’frostschutztemp")]
                    public AnalogValue KondFrostschutzTemp
                        => new AnalogValue(495, "°C", -20, 30) {IsReadOnly = !Settings.Default.ExpertMode};

                    /// <summary>
                    ///     Kond’frostschutztemp
                    /// </summary>
                    [Secondary]
                    [ExpertMode]
                    [Description(@"Kond’frostschutztemp")]
                    public AnalogValue KondFrostschutzSchatdiff
                        => new AnalogValue(496, "K", 2, 10) {IsReadOnly = !Settings.Default.ExpertMode};
                }
            }

            /// <summary>
            ///     Wärmepumpe Stufe 1
            /// </summary>
            [Description(
                @"Wärmepumpe Stufe 1: Die „Schaltdifferenz Stufe 1“ liegt symmetrisch zum aktiven Energieerzeugersollwert. Die minimale Energieerzeugerlaufzeit wirkt auf den Energieerzeuger, wenn eine Energieanforderung vorhanden ist.Nach Abschaltung des Kompressors sperrt die Wiedereinschaltverzögerung eine erneute Zuschaltung. Beim Überschreiten der „WP - Vorlauf Maximal“-Begrenzung(444) schaltet den Kompressor ab, die Kondensatorpumpe läuft weiter, die Wiedereinschaltverzögerung wird um die Hälfte reduziert"
                )]
            public class HeatPump
            {
                /// <summary>
                ///     Schaltdifferenz Stufe 1
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Schaltdifferenz Stufe 1")]
                public AnalogValue SwitchingDifferenceStage1
                    => new AnalogValue(140, "K", 2, 20) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Min WP-Laufzeit
                /// </summary>
                [Secondary]
                [Description(@"Min WP-Laufzeit")]
                public AnalogValue MinHeatpumpRunningTime => new AnalogValue(143, "min", 0, 30) {IsReadOnly = false};

                /// <summary>
                ///     Wiedereinschaltverzögerung 1
                /// </summary>
                [Secondary]
                [Description(@"Wiedereinschaltverzögerung")]
                public AnalogValue RestartDelay => new AnalogValue(864, "min", 20, 60) {IsReadOnly = false};

                /// <summary>
                ///     WP-Vorlauf Maximal
                /// </summary>
                [Secondary]
                [Description(@"WP-Vorlauf Maximal")]
                public AnalogValue FlowMaxTemperature => new AnalogValue(444, "°C", 0, 58) {IsReadOnly = false};
            }
        }
    }
}