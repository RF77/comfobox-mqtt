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
    ///     Markiert ein Item als Teil des V2-Topic-Vertrags.
    ///     V2-Items sind rein additiv: sie ersetzen kein V1-Topic und stehen in
    ///     einer eigenen Fixture (docs/topics-v2.json).
    ///     Nur Deklaration - src_old/ wird nicht kompiliert.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class V2Attribute : Attribute
    {
    }
}
