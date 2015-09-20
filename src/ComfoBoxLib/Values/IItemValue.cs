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
using System.Threading.Tasks;

namespace ComfoBoxLib.Values
{
    public interface IItemValue : INotifyPropertyChanged
    {
        event Action<object, float> InitializedValueChanged;
        object Value { get; }
        string Unit { get; }
        bool IsReadOnly { get; }
        Task ReadValueAsync(ComfoBoxClient client);
        Task WriteValueAsync(ComfoBoxClient client);
    }
}