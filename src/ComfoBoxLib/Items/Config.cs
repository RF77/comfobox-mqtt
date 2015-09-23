// /*******************************************************************************
//  * Copyright (c) 2015 by RF77 (https://github.com/RF77)
//  * All rights reserved. This program and the accompanying materials
//  * are made available under the terms of the Eclipse Public License v1.0
//  * which accompanies this distribution, and is available at
//  * http://www.eclipse.org/legal/epl-v10.html
//  *
//  * Contributors:
//  *    RF77 - initial API and implementation and/or initial documentation
//  *******************************************************************************/ 

using System;
using System.ComponentModel;
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Properties;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    [Description(@"Konfiguration")]
    public class Config
    {
        [Description(@"Comfofond-L Einstellungen")]
        public class Comfofond
        {
            /// <summary>
            ///     Sollwert Kühlung
            ///     Ab dieser Temperatur wird die Luft über das Comfofond-L gekühlt,
            ///     falls der Raumsollwert der Lüftung tiefer liegt
            /// </summary>
            [Secondary]
            [Description(@"Sollwert Kühlung: Ab dieser Temperatur wird die Luft über das Comfofond-L gekühlt, falls der Raumsollwert der Lüftung tiefer liegt")]
            public AnalogValue CoolingSetPoint => new AnalogValue(3441, "°C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Kühlung über das Comfofond-L
            ///     z.B. Sollwert ist bei 22°C
            ///     -> falls Kühlung aus, wird sie erst >24°C eingeschaltet
            ///     -> falls Kühlung ein, wird sie erst kleiner 20°C ausgeschaltet
            /// </summary>
            [Secondary]
            [Description(@"Hysterese beim Ein- und Ausschalten der Kühlung über das Comfofond-L. z.B. Sollwert ist bei 22°C
-> falls Kühlung aus, wird sie erst >24°C eingeschaltet
-> falls Kühlung ein, wird sie erst kleiner 20°C ausgeschaltet")]
            public AnalogValue CoolingHysteresis => new AnalogValue(3431, "K", 2, 20);

            /// <summary>
            ///     Sollwert Heizung
            ///     Ab dieser Temperatur wird die Luft über das Comfofond-L im Winter gewärmt,
            /// </summary>
            [Secondary]
            [Description(@"Sollwert Heizung: Ab dieser Temperatur wird die Luft über das Comfofond-L im Winter gewärmt")]
            public AnalogValue HeatingSetPoint => new AnalogValue(3442, "°C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Heizung über das Comfofond-L
            ///     z.B. Sollwert ist bei 2°C
            ///     -> falls Heizung aus, wird sie erst unter 0°C eingeschaltet
            ///     -> falls Heizung ein, wird sie erst oberhalb 4°C ausgeschaltet
            /// </summary>
            [Secondary]
            [Description(@"Hysterese beim Ein- und Ausschalten der Heizung über das Comfofond-L z.B. Sollwert ist bei 2°C;
-> falls Heizung aus, wird sie erst unter 0°C eingeschaltet
-> falls Heizung ein, wird sie erst oberhalb 4°C ausgeschaltet")]
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
            [Description(@"Energieerz’temp im Auslegep")]
            public AnalogValue EnergieerzeugerTempImAuslegepunkt => new AnalogValue(163, "°C", 20, 99) {IsReadOnly = !Settings.Default.ExpertMode};

            /// <summary>
            ///     Adapt. Energieerz’temp im Auslegep
            /// </summary>
            [Secondary]
            [Description(@"Adapt. Energieerz’temp im Auslegep")]
            public AnalogValue AdaptEnergieerzeugerTempImAuslegepunkt => new AnalogValue(166, "°C", 20, 99) { IsReadOnly = !Settings.Default.ExpertMode };

            /// <summary>
            ///     Anlagenfrostschutz
            ///     Wenn die gebäudebezogene Aussentemperatur unter den „Anlagefrostschutz“ (187) fällt werden
            ///     die Heizkreispumpen aktiviert.
            /// </summary>
            [Secondary]
            [Description(@"Anlagenfrostschutz: Wenn die gebäudebezogene Aussentemperatur unter den „Anlagefrostschutz“ (187) fällt, werden die Heizkreispumpen aktiviert.")]
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
            [Description(@"Vorlaufzeit Primär: Bei einer Energieanforderung wird die Primärpumpe bzw. der Ventilator aktiviert und der Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben")]
            public AnalogValue PrimaryPumpPreRunningTime => new AnalogValue(880, "min", 0, 99);

            /// <summary>
            ///     Nachlaufzeit Primär
            ///     Nach dem Ausschalten des Energieerzeugers wird die Primärpumpe bzw. der Ventilator erst
            ///     nach der Nachlaufzeit ausgeschaltet.
            /// </summary>
            [Secondary]
            [Description(@"Nachlaufzeit Primär: Nach dem Ausschalten des Energieerzeugers wird die Primärpumpe bzw. der Ventilator erst nach der Nachlaufzeit ausgeschaltet.")]
            public AnalogValue PrimaryPumpPostRunningTime => new AnalogValue(881, "min", 0, 99);
        }
        /// <summary>
        ///     WP, Schutz
        /// </summary>
        [Description(@"WP, Schutz -> Zweifelhaft, ob verwendet? Viele Schutzfunktionen sind auf 'Keine Funktion' eingestellt")]
        public class Protection
        {
            /// <summary>
            /// Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen.Parameter
            /// „Kondensat’frostschutz“ (494) definiert, welcher Fühler für die Schutzfunktion zuständig ist.Wird
            /// die Frostschutztemperatur(495) um die halbe Schaltdifferenz(496) unterschritten, wird der
            /// Kompressor gestoppt und(je nach Einstellung) die Zusatzheizung gestartet(497). Die
            /// Kondensatorpumpe läuft in jedem Fall weiter.Der Parameter „WP-Frostschutz Mode“ (498)
            /// definiert, ob die WP wieder selbstständig starten kann oder ob eine manuelle Entriegelung nötig
            /// ist(siehe Beschreibung „WP-Frostschutz“).
            /// </summary>

            public class KondensatorFrostschutz
            {
                /// <summary>
                ///     Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen. Parameter „Kondensat’frostschutz“ definiert, welcher Fühler für die Schutzfunktion zuständig ist.
                /// </summary>
                [Secondary]
                [Description(@"Der Kondensatorfrostschutz schützt die Wärmepumpe während Abtauen und Kühlen. Parameter „Kondensat’frostschutz“ definiert, welcher Fühler für die Schutzfunktion zuständig ist.")]
                public EnumValue<KondensatFrostschutzEnum> KondensatFrostschutz => new EnumValue<KondensatFrostschutzEnum>(494) { IsReadOnly = !Settings.Default.ExpertMode };

                //TODO: 1984


                /// <summary>
                ///     Kond’frostschutztemp
                /// </summary>
                [Secondary]
                [Description(@"Kond’frostschutztemp")]
                public AnalogValue KondFrostschutzTemp => new AnalogValue(495, "°C", -20, 30) { IsReadOnly = !Settings.Default.ExpertMode };
                
                /// <summary>
                ///     Kond’frostschutztemp
                /// </summary>
                [Secondary]
                [Description(@"Kond’frostschutztemp")]
                public AnalogValue KondFrostschutzSchatdiff => new AnalogValue(496, "K", 2, 10) { IsReadOnly = !Settings.Default.ExpertMode };
            }
            //TODO: WP, Schutz -> 6.3.8

        }

        //TODO: Begrenzungen

        //TODO: Stufe 1
        //TODO: Kühlen
        //TODO: Energieabn parametrieren
        //TODO: Heizkurve Zone
        //TODO: Heizen Zone
        //TODO: Kühlen Zone
        //TODO: Warmwasser
    }
}