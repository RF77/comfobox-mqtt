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

namespace ComfoBoxLib.Values
{
    public interface IEnumValue
    {
        void SetValueFromString(string value);
        Type GetEnumType();
    }

	public interface ICommandValue
	{
	}

	public interface IChangeableValue
    {
        
    }
}