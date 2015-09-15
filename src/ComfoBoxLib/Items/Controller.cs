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
    public class Controller
    {
        /// <summary>
        ///     Read only values
        /// </summary>
        public class States
        {
            /// <summary>
            ///     Genauer Reglertyp
            /// </summary>
            [Once]
            public AnalogValue ControllerType => new AnalogValue(203, null) {IsReadOnly = true};

            /// <summary>
            ///     Angeschlossener Erweiterungstyp
            /// </summary>
            [Once]
            public AnalogValue ExtensionType => new AnalogValue(2044, null) {IsReadOnly = true};

            /// <summary>
            ///     Sofwareversion
            /// </summary>
            [Once]
            public AnalogValue SoftwareVersion => new AnalogValue(2043, null) {IsReadOnly = true};

            /// <summary>
            ///     Das Inbetriebnahme-Datum der Reglers wird
            ///     beim Laden der Applikation gesetzt
            /// </summary>
            [Once]
            public DateValue InitialOperationDate => new DateValue(3353, null) {IsReadOnly = true};

            /// <summary>
            ///     Betriebsstunden des Reglers
            /// </summary>
            [Secondary]
            public AnalogValue OperatingHours => new AnalogValue(211, "h") {IsReadOnly = true};

            /// <summary>
            ///     Anzahl Einschaltungen Regler-Speisung
            /// </summary>
            [Secondary]
            public AnalogValue NumberOfStartUps => new AnalogValue(212, null) {IsReadOnly = true};

            /// <summary>
            ///     Anzahl der geänderten Parameter seit
            ///     „Applikation laden…“
            /// </summary>
            [Secondary]
            public AnalogValue NumberOfParameterChanges => new AnalogValue(617, null) {IsReadOnly = true};

            /// <summary>
            ///     Datum der letzten Parameteränderung
            /// </summary>
            [Secondary]
            public DateValue LastParameterChangeDate => new DateValue(618, null) {IsReadOnly = true};

            // Other values?: 
        }
    }
}