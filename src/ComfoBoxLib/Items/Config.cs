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
            ///     1: Sommerkick t�glich um 16:00
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Sommerknick: Die Sommerintervallschaltung (185) verhindert das Festsitzen der Heizkreispumpen, der Energieerzeugerpumpe und der Mischer im Sommerbetrieb.
* 0: deaktiviert
* 1: Sommerkick t�glich um 16:00")]
            public AnalogValue Sommerknick => new AnalogValue(185, "", 0, 1) {IsReadOnly = !Settings.Default.ExpertMode}
                ;

            /// <summary>
            ///     Entl�ftung Prim�rkreis:
            ///     Die Funktion definiert eine Zeit zur Entl�ftung des Prim�rkreises(Energiequellen-Kreis). W�hrend
            ///     der eingestellten Zeit l�uft die Prim�rpumpe(resp.die Prim�rpumpen bei mehrstufigen
            ///     W�rmepumpen).
            /// </summary>
            [Secondary]
            [Description(@"Entl�ftung Prim�rkreis:
Die Funktion definiert eine Zeit zur Entl�ftung des Prim�rkreises (Energiequellen-Kreis). W�hrend
der eingestellten Zeit l�uft die Prim�rpumpe (resp. die Prim�rpumpen bei mehrstufigen
W�rmepumpen).")]
            public AnalogValue EntlueftungPrimaerkreis => new AnalogValue(3398, "h");

            /// <summary>
            ///     Entl�ftung Sek�kreis
            ///     Die Funktion definiert eine Zeit zur Entl�ftung des Sekund�rkreises(Energieabnahme). W�hrend
            ///     der eingestellten Zeit laufen alle Zonenpumpen, sowie die Pufferladepumpe(resp.
            ///     Kondensatorpumpe).
            /// </summary>
            [Secondary]
            [Description(@"Entl�ftung Sek�kreis:
Die Funktion definiert eine Zeit zur Entl�ftung des Sekund�rkreises (Energieabnahme). W�hrend
der eingestellten Zeit laufen alle Zonenpumpen, sowie die Pufferladepumpe (resp.
Kondensatorpumpe).")]
            public AnalogValue EntlueftungSekundaerkreis => new AnalogValue(3400, "h");
        }

        [Description(@"Einstellung f�r Solekreispumpe")]
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
            ///     Minimale K�hl-Pumpenleistung in %
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Minimale K�hl-Pumpenleistung in %")]
            public AnalogValue MinCooling
                => new AnalogValue(3613, "%", 0, 100) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Maximale K�hl-Pumpenleistung in %
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Maximale K�hl-Pumpenleistung in %")]
            public AnalogValue MaxCooling
                => new AnalogValue(3614, "%", 0, 100) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Y1 Max Mode:
            ///     Die Maximalbegrenzung(412, 3614) kann in speziellen Zust�nden �bersteuert werden(z.B.bei
            ///     Frostschutz). Wenn dies nicht erw�nscht ist, kann mit diesem Parameter die Maximalbegrenzung
            ///     als �Immer aktiv� definiert werden.
            ///     0: Nicht aktiv bei �bersteuer
            ///     1: Immer aktiv
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Y1 Max Mode:
Die Maximalbegrenzung (412, 3614) kann in speziellen Zust�nden �bersteuert werden (z.B. bei
Frostschutz). Wenn dies nicht erw�nscht ist, kann mit diesem Parameter die Maximalbegrenzung
als �Immer aktiv� definiert werden.
* 0: Nicht aktiv bei �bersteuer
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
            ///     DeltaT f�r Umsch Pas�k�hlen
            /// </summary>
            [Secondary]
            [Description(
                @"DeltaT f�r Umsch Pas�k�hlen"
                )]
            public AnalogValue DeltaTempForSwitching => new AnalogValue(574, "K", 0, 20);

            /// <summary>
            ///     Schaltdiff Umsch Pas�k�hlen
            /// </summary>
            [Secondary]
            [Description(
                @"Schaltdiff Umsch Pas�k�hlen"
                )]
            public AnalogValue DiffForSwitching => new AnalogValue(575, "K", 2, 10);

            /// <summary>
            ///     Temp min bei K�hlen
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Temp min bei K�hlen"
                )]
            public AnalogValue MinTemperature
                => new AnalogValue(576, "�C", 6, 99) {IsReadOnly = !Settings.Default.ExpertMode};
        }


        [Description(@"Comfofond-L Einstellungen")]
        public class Comfofond
        {
            /// <summary>
            ///     Sollwert K�hlung
            ///     Ab dieser Temperatur wird die Luft �ber das Comfofond-L gek�hlt,
            ///     falls der Raumsollwert der L�ftung tiefer liegt
            /// </summary>
            [Secondary]
            [Description(
                @"Sollwert K�hlung: Ab dieser Temperatur wird die Luft �ber das Comfofond-L gek�hlt, falls der Raumsollwert der L�ftung tiefer liegt"
                )]
            public AnalogValue CoolingSetPoint => new AnalogValue(3441, "�C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der K�hlung �ber das Comfofond-L
            ///     z.B. Sollwert ist bei 22�C
            ///     -> falls K�hlung aus, wird sie erst >24�C eingeschaltet
            ///     -> falls K�hlung ein, wird sie erst kleiner 20�C ausgeschaltet
            /// </summary>
            [Secondary]
            [Description(
                @"Hysterese beim Ein- und Ausschalten der K�hlung �ber das Comfofond-L. z.B. Sollwert ist bei 22�C
* -> falls K�hlung aus, wird sie erst >24�C eingeschaltet
* -> falls K�hlung ein, wird sie erst kleiner 20�C ausgeschaltet")]
            public AnalogValue CoolingHysteresis => new AnalogValue(3431, "K", 2, 20);

            /// <summary>
            ///     Sollwert Heizung
            ///     Ab dieser Temperatur wird die Luft �ber das Comfofond-L im Winter gew�rmt,
            /// </summary>
            [Secondary]
            [Description(@"Sollwert Heizung: Ab dieser Temperatur wird die Luft �ber das Comfofond-L im Winter gew�rmt")
            ]
            public AnalogValue HeatingSetPoint => new AnalogValue(3442, "�C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Heizung �ber das Comfofond-L
            ///     z.B. Sollwert ist bei 2�C
            ///     -> falls Heizung aus, wird sie erst unter 0�C eingeschaltet
            ///     -> falls Heizung ein, wird sie erst oberhalb 4�C ausgeschaltet
            /// </summary>
            [Secondary]
            [Description(
                @"Hysterese beim Ein- und Ausschalten der Heizung �ber das Comfofond-L z.B. Sollwert ist bei 2�C;
* -> falls Heizung aus, wird sie erst unter 0�C eingeschaltet
* -> falls Heizung ein, wird sie erst oberhalb 4�C ausgeschaltet")]
            public AnalogValue HeatingHysteresis => new AnalogValue(3452, "K", 2, 20);
        }

        /// <summary>
        ///     Heizen System
        /// </summary>
        [Description(@"Heizen System")]
        public class HeatingSystem
        {
            /// <summary>
            ///     Energieerz�temp im Auslegep
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Energieerz�temp im Auslegep")]
            public AnalogValue EnergieerzeugerTempImAuslegepunkt
                => new AnalogValue(163, "�C", 20, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Adapt. Energieerz�temp im Auslegep
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(@"Adapt. Energieerz�temp im Auslegep")]
            public AnalogValue AdaptEnergieerzeugerTempImAuslegepunkt
                => new AnalogValue(166, "�C", 20, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Anlagenfrostschutz
            ///     Wenn die geb�udebezogene Aussentemperatur unter den �Anlagefrostschutz� (187) f�llt werden
            ///     die Heizkreispumpen aktiviert.
            /// </summary>
            [Secondary]
            [Description(
                @"Anlagenfrostschutz: Wenn die geb�udebezogene Aussentemperatur unter den �Anlagefrostschutz� (187) f�llt, werden die Heizkreispumpen aktiviert."
                )]
            public AnalogValue Anlagenfrostschutz => new AnalogValue(187, "�C", -15, 20);
        }


        /// <summary>
        ///     Energieerzeuger parametrieren
        /// </summary>
        [Description(@"Energieerzeuger parametrieren")]
        public class EnergyProducer
        {
            /// <summary>
            ///     Vorlaufzeit Prim�r
            ///     Bei einer Energieanforderung wird die Prim�rpumpe bzw. der Ventilator aktiviert und der
            ///     Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Vorlaufzeit Prim�r: Bei einer Energieanforderung wird die Prim�rpumpe bzw. der Ventilator aktiviert und der Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben"
                )]
            public AnalogValue PrimaryPumpPreRunningTime
                => new AnalogValue(880, "min", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Nachlaufzeit Prim�r
            ///     Nach dem Ausschalten des Energieerzeugers wird die Prim�rpumpe bzw. der Ventilator erst
            ///     nach der Nachlaufzeit ausgeschaltet.
            /// </summary>
            [Secondary]
            [ExpertMode]
            [Description(
                @"Nachlaufzeit Prim�r: Nach dem Ausschalten des Energieerzeugers wird die Prim�rpumpe bzw. der Ventilator erst nach der Nachlaufzeit ausgeschaltet."
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
                ///     Geb�udetr�gheit:
                ///     Ohne Tr�gheit (Testzwecke) = 0
                ///     Leichte Bauweise = 1
                ///     Normale Bauweise = 2
                ///     Schwere Bauweise = 3
                /// </summary>
                [Secondary]
                [Description(
                    @"Geb�udetr�gheit:
* Ohne Tr�gheit (Testzwecke) = 0
* Leichte Bauweise = 1
* Normale Bauweise = 2
* Schwere Bauweise = 3
"
                    )]
                public AnalogValue Geb�udetr�gheit => new AnalogValue(170, "", 0, 3);

                /// <summary>
                ///     Mischerlaufzeit
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Mischerlaufzeit")]
                public AnalogValue Mischerlaufzeit
                    => new AnalogValue(113, "min", 1, 30) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     �berh�h Vorlauf/Energieerz
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"�berh�h Vorlauf/Energieerz")]
                public AnalogValue �berh�hVorlaufEnergieerz
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
                ///     Raumsoll��berh Sol
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Raumsoll��berh Sol")]
                public AnalogValue Raumsoll�berhSol
                    => new AnalogValue(462, "K", 0, 6) {IsReadOnly = !Settings.Default.ExpertMode};

            }

            [Description(@"Heizkurve Zone parametrieren:
Die normierte Steilheit verl�uft durch �Fixpunkt� (160) und
Auslegepunkt. Der Auslegepunkt wird durch die �Aussentemp
im Auslegepunkt� (161) und �Vorlauftemp im Auslegepunkt�
(162) festgelegt. Die Heizkennlinienadaption erlaubt die
Korrektur der definierten Heizkennlinie. Wenn ein Raumf�hler
vorhanden ist, kann die Adaption automatisch erfolgen. In
beiden Situationen (manuell und automatisch) erfolgt die
Korrektur bei Aussentemperaturen unter 5�C beim
Auslegepunkt, bei Aussentemperaturen �ber 5�C wird die
Kr�mmung der Heizkennlinie ver�ndert. Der korrigierte
Auslegepunkt ist in �Adaptierter Fixpunkt� (164) und �Adapt Vorl�temp im Auslegep� (165)
ersichtlich.")]
            public class Heizkurve
            {
                /// <summary>
                ///     Fixpunkt
                /// </summary>
                [Secondary]
                [Description(@"Fixpunkt")]
                public AnalogValue Fixpunkt => new AnalogValue(160, "�C", 10, 40);

                /// <summary>
                ///     Aussentemp im Auslegepunkt
                /// </summary>
                [Secondary]
                [Description(@"Aussentemp im Auslegepunkt")]
                public AnalogValue AussentempImAuslegepunkt => new AnalogValue(161, "�C", -30, 0);

                /// <summary>
                ///     Vorlauftemp im Auslegepunkt
                /// </summary>
                [Secondary]
                [Description(@"Vorlauftemp im Auslegepunkt")]
                public AnalogValue VorlauftempImAuslegepunkt => new AnalogValue(162, "�C", 20, 99);

                /// <summary>
                ///     Adaptierter Fixpunkt
                /// </summary>
                [Secondary]
                [Description(@"Adaptierter Fixpunkt")]
                public AnalogValue AdaptierterFixpunkt => new AnalogValue(164, "�C", 10, 40);

                /// <summary>
                ///     Adapt Vorl�temp im Auslegep
                /// </summary>
                [Secondary]
                [Description(@"Adapt Vorl�temp im Auslegep")]
                public AnalogValue AdaptVorlauftempImAuslegepunkt => new AnalogValue(165, "�C", 20, 99);

                /// <summary>
                ///     Heizkennlinienadaption:
                ///     Keine Funktion = 0
                ///     Manuell, auto mit Raum'f�h = 1
                ///     Manuell, Korrektureingabe = 2
                /// </summary>
                [Secondary]
                [Description(@"Heizkennlinienadaption:
* Keine Funktion = 0
* Manuell, auto mit Raum'f�h = 1
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
Heizkennlinie) gr�sser ist als der Raumtemperatursollwert oder wenn bei direktem Heizkreis der
R�cklauftemperatursollwert unter den Raumtemperatursollwert sinkt, schaltet der Heizbetrieb
aus. Die Grenze kann mit �Tagesheizgrenze Offset� (3569) verschoben werden, um bei Bauten
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
der Raumtemperatursollwert nur noch um den hier eingestellten Wert gr�sser ist als die
ged�mpfte Aussentemperatur (Zeitkonstante 21 Std.), schaltet der Heizbetrieb aus. Die Funktion
wird nur in den automatischen Betriebsarten (�Normal/Reduziert� und �Normal/Frostschutz�)
ausgef�hrt.")]
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
                public AnalogValue VorlaufMaximal => new AnalogValue(154, "�C", 0, 125);

                /// <summary>
                ///     Vorlauf Minimal
                /// </summary>
                [Secondary]
                [Description(@"Vorlauf Minimal")]
                public AnalogValue VorlaufMinimal => new AnalogValue(153, "�C", 0, 99);

                /// <summary>
                ///     Raumsoll-Korr Zo
                /// </summary>
                [Secondary]
                [Description(@"Raumsoll-Korr Zo")]
                public AnalogValue RaumsollKorrZone => new AnalogValue(3576, "K", -5.0f, 5.0f);
            }

            [Description(@"K�hlen Zone:
K�hlen wird aktiviert, sobald die
Geb�udebezogene Aussentemperatur �ber
die Sommerk�hlgrenze steigt. Die
Sommerk�hlgrenze setzt sich aus dem
aktuellen Raumsollwert und dem Wert in
�Sommerk�hlgrenze� (473) zusammen. Der
Raumsollwert wird anhand der
Aussentemperatur mit dem Faktor in
�Steilheit Raumsoll-Schiebung� (475)
angehoben. Der Vorlaufsollwert f�r K�hlen
ist abh�ngig von der gemessenen
Raumist/soll Abweichung und begrenzt
durch die Gerade zwischen den Werten in
�Min Vorl�temp K�hlen (20�C)� (477) und �Min Vorl�temp K�hlen (40�C)� (478) und der �Abs min
Vorlauftemp K�hlen� (479). Die Raumsollwerte k�nnen f�r die K�hlfunktion mit
�K�hlraumsoll�abst� (normal: 3562, reduziert: 863, standby: 864) angehoben werden. F�r
Geb�ude mit stark unterschiedlichen Gegebenheiten, kann ein zus�tzlicher Raumf�hler
eingesetzt werden. Dabei wird ein F�hler f�r Heizen, der andere f�r K�hlen verwendet. Die
Zuordnung erfolgt �ber �Umschaltung Raumf�hler� (3085).")]
            public class CoolingZone
            {
                /// <summary>
                ///     K�hlen
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"K�hlen:
* Keine Funktion = 0
* K�hlen = 1
* Nur K�hlen = 3")]
                public AnalogValue Cooling => new AnalogValue(470, "", 0, 3) {IsReadOnly = !Settings.Default.ExpertMode}
                    ;

                /// <summary>
                ///     Sommerk�hlgrenze
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Sommerk�hlgrenze")]
                public AnalogValue Sommerk�hlgrenze
                    => new AnalogValue(473, "K", -10, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     K�hlraumsoll�abst normal
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"K�hlraumsoll�abst normal")]
                public AnalogValue K�hlraumsollAbstNormal
                    => new AnalogValue(3562, "K", 0, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     K�hlraumsoll�abst reduziert
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"K�hlraumsoll�abst reduziert")]
                public AnalogValue K�hlraumsollAbstReduziert
                    => new AnalogValue(863, "K", 0, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     K�hlraumsoll�abst standby
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"K�hlraumsoll�abst standby")]
                public AnalogValue K�hlraumsollAbstStandby
                    => new AnalogValue(864, "K", 0, 10) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Fixpunkt Raumsoll K�h (20�C)
                /// </summary>
                [Secondary]
                [Description(@"Fixpunkt Raumsoll K�h (20�C)")]
                public AnalogValue FixpunktRaumsoll => new AnalogValue(474, "�C", 10, 30);

                /// <summary>
                ///     Steilheit Raumsoll-Schiebung
                /// </summary>
                [Secondary]
                [Description(@"Steilheit Raumsoll-Schiebung")]
                public AnalogValue SteilheitRaumsollSchiebung => new AnalogValue(475, "K", 0, 5);

                /// <summary>
                ///     Min Vorl�temp K�hlen (20�C)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Min Vorl�temp K�hlen (20�C)")]
                public AnalogValue MinVorlTemp20
                    => new AnalogValue(477, "�C", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Min Vorl�temp K�hlen (40�C)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Min Vorl�temp K�hlen (40�C)")]
                public AnalogValue MinVorlTemp40
                    => new AnalogValue(478, "�C", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Abs min Vorlauftemp K�hlen
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Abs min Vorlauftemp K�hlen")]
                public AnalogValue AbsMinVorlauftemp
                    => new AnalogValue(479, "�C", 0, 99) {IsReadOnly = !Settings.Default.ExpertMode};

                /// <summary>
                ///     Raumeinfluss bei K�hlen
                /// </summary>
                [Secondary]
                [Description(@"Raumeinfluss bei K�hlen")]
                public AnalogValue RaumeinflussBeiK�hlen => new AnalogValue(578, "%", 100, 999);

                /// <summary>
                ///     Umschaltung Raumf�hler
                /// </summary>
                [Secondary]
                [Description(@"Umschaltung Raumf�hler:
* Keine Funktion = 0
* Heiz'n mit Tr1, K�hl'n mit Tr2 = 1
* Heiz'n mit Tr2, K�hl'n mit Tr1 = 2
")]
                public AnalogValue UmschaltungRaumf�hler => new AnalogValue(3085, "", 0, 2);
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
* Legionellenschutz t�glich = 8

")]
                public AnalogValue Legionellenschutzfunktion => new AnalogValue(192, "", 0, 8);

                /// <summary>
                ///     WW-Zwangsladung
                /// </summary>
                [Secondary]
                [Description(@"WW-Zwangsladung
* Keine Funktion = 0
* T�glich bei erster WW-Ladung = 1")]
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
                ///     Energieerz�anhebung (F�hler)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Energieerz�anhebung (F�hler)")]
                public AnalogValue EnergieerzAnhebungF�hler
                    => new AnalogValue(193, "K", 2, 60) { IsReadOnly = !Settings.Default.ExpertMode };

                /// <summary>
                ///     Energieerz�soll (Thermostat)
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"Energieerz�soll (Thermostat)")]
                public AnalogValue EnergieerzSollThermostat
                    => new AnalogValue(194, "�C", 0, 99) { IsReadOnly = !Settings.Default.ExpertMode };

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
                    => new AnalogValue(190, "�C", 5, 99);

                /// <summary>
                ///     WW-Sollwert Maximal WP
                /// </summary>
                [Secondary]
                [ExpertMode]
                [Description(@"WW-Sollwert Maximal WP")]
                public AnalogValue SollwertMaximalWP
                    => new AnalogValue(447, "�C", 0, 58) { IsReadOnly = !Settings.Default.ExpertMode };

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
                ///     Der Kondensatorfrostschutz sch�tzt die W�rmepumpe w�hrend Abtauen und K�hlen.Parameter
                ///     �Kondensat�frostschutz� (494) definiert, welcher F�hler f�r die Schutzfunktion zust�ndig ist.Wird
                ///     die Frostschutztemperatur(495) um die halbe Schaltdifferenz(496) unterschritten, wird der
                ///     Kompressor gestoppt und(je nach Einstellung) die Zusatzheizung gestartet(497). Die
                ///     Kondensatorpumpe l�uft in jedem Fall weiter.Der Parameter �WP-Frostschutz Mode� (498)
                ///     definiert, ob die WP wieder selbstst�ndig starten kann oder ob eine manuelle Entriegelung n�tig
                ///     ist(siehe Beschreibung �WP-Frostschutz�).
                /// </summary>
                public class KondensatorFrostschutz
                {
                    /// <summary>
                    ///     Der Kondensatorfrostschutz sch�tzt die W�rmepumpe w�hrend Abtauen und K�hlen. Parameter �Kondensat�frostschutz�
                    ///     definiert, welcher F�hler f�r die Schutzfunktion zust�ndig ist.
                    /// </summary>
                    [Secondary]
                    [ExpertMode]
                    [Description(
                        @"Der Kondensatorfrostschutz sch�tzt die W�rmepumpe w�hrend Abtauen und K�hlen. Parameter �Kondensat�frostschutz� definiert, welcher F�hler f�r die Schutzfunktion zust�ndig ist."
                        )]
                    public EnumValue<KondensatFrostschutzEnum?> KondensatFrostschutz
                        => new EnumValue<KondensatFrostschutzEnum?>(494) {IsReadOnly = !Settings.Default.ExpertMode};

                    //TODO: 1984


                    /// <summary>
                    ///     Kond�frostschutztemp
                    /// </summary>
                    [Secondary]
                    [ExpertMode]
                    [Description(@"Kond�frostschutztemp")]
                    public AnalogValue KondFrostschutzTemp
                        => new AnalogValue(495, "�C", -20, 30) {IsReadOnly = !Settings.Default.ExpertMode};

                    /// <summary>
                    ///     Kond�frostschutztemp
                    /// </summary>
                    [Secondary]
                    [ExpertMode]
                    [Description(@"Kond�frostschutztemp")]
                    public AnalogValue KondFrostschutzSchatdiff
                        => new AnalogValue(496, "K", 2, 10) {IsReadOnly = !Settings.Default.ExpertMode};
                }
            }

            /// <summary>
            ///     W�rmepumpe Stufe 1
            /// </summary>
            [Description(
                @"W�rmepumpe Stufe 1: Die �Schaltdifferenz Stufe 1� liegt symmetrisch zum aktiven Energieerzeugersollwert. Die minimale Energieerzeugerlaufzeit wirkt auf den Energieerzeuger, wenn eine Energieanforderung vorhanden ist.Nach Abschaltung des Kompressors sperrt die Wiedereinschaltverz�gerung eine erneute Zuschaltung. Beim �berschreiten der �WP - Vorlauf Maximal�-Begrenzung(444) schaltet den Kompressor ab, die Kondensatorpumpe l�uft weiter, die Wiedereinschaltverz�gerung wird um die H�lfte reduziert"
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
                ///     Wiedereinschaltverz�gerung 1
                /// </summary>
                [Secondary]
                [Description(@"Wiedereinschaltverz�gerung")]
                public AnalogValue RestartDelay => new AnalogValue(864, "min", 20, 60) {IsReadOnly = false};

                /// <summary>
                ///     WP-Vorlauf Maximal
                /// </summary>
                [Secondary]
                [Description(@"WP-Vorlauf Maximal")]
                public AnalogValue FlowMaxTemperature => new AnalogValue(444, "�C", 0, 58) {IsReadOnly = false};
            }
        }
    }
}