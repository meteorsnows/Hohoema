﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Bindings;
using System.Runtime.Serialization;
using NicoPlayerHohoema.Services;

namespace NicoPlayerHohoema.Models
{
    [DataContract]
    public class AppearanceSettings : SettingsBase
    {
        private HohoemaPageType _StartupPageType = HohoemaPageType.RankingCategoryList;

        [DataMember]
        public HohoemaPageType StartupPageType
        {
            get { return _StartupPageType; }
            set { SetProperty(ref _StartupPageType, value); }
        }


        private bool _IsForceTVModeEnable = false;

        [DataMember]
        public bool IsForceTVModeEnable
        {
            get { return _IsForceTVModeEnable; }
            set { SetProperty(ref _IsForceTVModeEnable, value); }
        }

        private bool _IsForceMobileModeEnable = false;

        [DataMember]
        public bool IsForceMobileModeEnable
        {
            get { return _IsForceMobileModeEnable; }
            set { SetProperty(ref _IsForceMobileModeEnable, value); }
        }
    }
}
