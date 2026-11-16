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
using ComfoBoxLib.Properties;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    /// <summary>
    ///     Aktionen auf der Anlage - rein schreibende V2-Command-Topics.
    /// </summary>
    /// <remarks>
    ///     Alle Items dieser Klasse sind schreibbar und nur mit Experten-Modus
    ///     freigegeben (ADR-014). Die Topic-Adresse ergibt sich aus
    ///     Command/&lt;Property&gt;.
    /// </remarks>
    [Description(@"Command")]
    public class Command
    {
        /// <summary>
        ///     Heizkennlinien-Adaption loeschen: gesammelte Adaption zuruecksetzen
        /// </summary>
        [V2]
        [ExpertMode]
        [Description(@"Heizkennlinien-Adaption löschen: gesammelte Adaption zurücksetzen (Schreibwert 1)")]
        public AnalogValue ResetHeatingCurveAdaption
            => new AnalogValue(678, null, 0, 1) {IsReadOnly = !Settings.Default.ExpertMode};
    }
}
