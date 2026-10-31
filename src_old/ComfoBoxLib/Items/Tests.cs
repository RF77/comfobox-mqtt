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

using System.ComponentModel;
using ComfoBoxLib.Attributes;
using ComfoBoxLib.Values;

namespace ComfoBoxLib.Items
{
    [Description(@"Tests")]
    public class Tests
    {
        /// <summary>
        ///     Ausgänge inaktiv
        /// </summary>
        [Primary]
        [Description(@"Ausgänge inaktiv")]
        public AnalogValue OutputsInactive => new AnalogValue(2049, null);

        /// <summary>
        ///     Ausgang R4
        /// </summary>
        [Primary]
        [Description(@"Ausgang R4")]
        public AnalogValue R4 => new AnalogValue(2782, null);

        /// <summary>
        ///     Ausgang Y1
        /// </summary>
        [Primary]
        [Description(@"Ausgang Y1")]
        public AnalogValue Y1 => new AnalogValue(2789, null);

        /// <summary>
        ///     Ausgang Y2
        /// </summary>
        [Primary]
        [Description(@"Ausgang Y2")]
        public AnalogValue Y2 => new AnalogValue(2790, null);
    }
}