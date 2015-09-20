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

using ComfoBoxLib.Attributes;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    public class Config
    {
        public class Comfofond
        {
            /// <summary>
            ///     Sollwert Kühlung
            ///     Ab dieser Temperatur wird die Luft über das Comfofond-L gekühlt,
            ///     falls der Raumsollwert der Lüftung tiefer liegt
            /// </summary>
            [Secondary]
            public AnalogValue CoolingSetPoint => new AnalogValue(3441, "°C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Kühlung über das Comfofond-L
            ///     z.B. Sollwert ist bei 22°C
            ///     -> falls Kühlung aus, wird sie erst >24°C eingeschaltet
            ///     -> falls Kühlung ein, wird sie erst kleiner 20°C ausgeschaltet
            /// </summary>
            [Secondary]
            public AnalogValue CoolingHysteresis => new AnalogValue(3431, "K", 2, 20);

            /// <summary>
            ///     Sollwert Heizung
            ///     Ab dieser Temperatur wird die Luft über das Comfofond-L im Winter gewärmt,
            /// </summary>
            [Secondary]
            public AnalogValue HeatingSetPoint => new AnalogValue(3442, "°C", -50, 999);

            /// <summary>
            ///     Hysterese beim Ein- und Ausschalten der Heizung über das Comfofond-L
            ///     z.B. Sollwert ist bei 2°C
            ///     -> falls Heizung aus, wird sie erst unter 0°C eingeschaltet
            ///     -> falls Heizung ein, wird sie erst oberhalb 4°C ausgeschaltet
            /// </summary>
            [Secondary]
            public AnalogValue HeatingHysteresis => new AnalogValue(3452, "K", 2, 20);
        }

        /// <summary>
        ///     Heizen System
        /// </summary>
        public class HeatingSystem
        {
            /// <summary>
            ///     Energieerz’temp im Auslegep
            /// </summary>
            [Secondary]
            public AnalogValue EnergieerzeugerTempImAuslegepunkt => new AnalogValue(163, "°C", 20, 99) {IsReadOnly = true};

            /// <summary>
            ///     Adapt. Energieerz’temp im Auslegep
            /// </summary>
            [Secondary]
            public AnalogValue AdaptEnergieerzeugerTempImAuslegepunkt => new AnalogValue(166, "°C", 20, 99) { IsReadOnly = true };

            /// <summary>
            ///     Anlagenfrostschutz
            ///     Wenn die gebäudebezogene Aussentemperatur unter den „Anlagefrostschutz“ (187) fällt werden
            ///     die Heizkreispumpen aktiviert.
            /// </summary>
            [Secondary]
            public AnalogValue Anlagenfrostschutz => new AnalogValue(187, "°C", -15, 20);
        }

        /// <summary>
        ///     Energieerzeuger parametrieren
        /// </summary>
        public class EnergyProducer
        {
            /// <summary>
            ///     Vorlaufzeit Primär
            ///     Bei einer Energieanforderung wird die Primärpumpe bzw. der Ventilator aktiviert und der
            ///     Energieerzeuger(Verdichter) erst nach der Vorlaufzeit freigegeben
            /// </summary>
            [Secondary]
            public AnalogValue PrimaryPumpPreRunningTime => new AnalogValue(880, "min", 0, 99);

            /// <summary>
            ///     Nachlaufzeit Primär
            ///     Nach dem Ausschalten des Energieerzeugers wird die Primärpumpe bzw. der Ventilator erst
            ///     nach der Nachlaufzeit ausgeschaltet.
            /// </summary>
            [Secondary]
            public AnalogValue PrimaryPumpPostRunningTime => new AnalogValue(881, "min", 0, 99);
        }

        //TODO: WP, Schutz
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