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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ComfoBoxLib;
using ComfoBoxLib.Annotations;

namespace DemoClient.ViewModels
{
    public abstract class TreeItemViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;

        protected TreeItemViewModel(string name)
        {
            Name = name;
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public string Name { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public abstract Task ReadAllValuesAsync(ComfoBoxClient client);

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}