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

namespace ComfoBoxLib.Items
{
    public enum KondensatFrostschutzEnum
    {
        KeineFunktion = 0,
        MitWpVorlauftemperatur = 31,
        MitSauggastemperatur = 37,
        MitKondensatortemperatur = 39,
        MitKondFrostchPressost = 63,
        KondWpFrostmitTKond = 80
    }
}