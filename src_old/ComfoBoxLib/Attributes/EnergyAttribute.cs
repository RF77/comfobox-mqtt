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
    ///     Markiert Energie- und COP-Items.
    ///     Ohne W�rmemengenmessung (DP 3340 = None) duerfen diese Items nicht
    ///     als 0 publiziert werden - der Gateway sperrt sie stattdessen.
    ///     Nur Deklaration - src_old/ wird nicht kompiliert.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EnergyAttribute : Attribute
    {
    }
}
