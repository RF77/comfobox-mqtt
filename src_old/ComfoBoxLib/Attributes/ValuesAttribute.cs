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

using System;

namespace ComfoBoxLib.Attributes
{
    /// <summary>
    ///     Erlaubte Werte eines Auswahl-Parameters.
    ///     Min/Max sind Bereiche, keine Wertelisten: das Gerät kennt bei
    ///     "Kühlen" z.B. nur 0, 1 und 3 - nicht 2.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ValuesAttribute : Attribute
    {
        public ValuesAttribute(params int[] values)
        {
            Values = values;
        }

        public int[] Values { get; }
    }
}
