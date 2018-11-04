//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace aucovei.uwp
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "aucovei Companion";

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title="Connect", ClassType=typeof(DeviceConnection)},
            new Scenario() { Title="Waypoint Navigation", ClassType=typeof(AddWaypoints)},
            new Scenario() { Title="Review & Send", ClassType=typeof(SendWaypoints)},
            new Scenario() { Title="Manual Navigation", ClassType=typeof(ManualMode)}

        };
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
