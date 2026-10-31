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

using System.Windows;
using DemoClient.ViewModels;

namespace DemoClient
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = ViewModel = new MainViewModel();
        }

        public MainViewModel ViewModel { get; set; }

        private void ConnectClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.Connect();
        }

        private void ReadAllClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ReadAllValues();
        }

        private void OnReadClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ReadValue();
        }
    }
}