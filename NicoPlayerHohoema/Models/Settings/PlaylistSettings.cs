﻿using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;

namespace NicoPlayerHohoema.Models
{
    [DataContract]
    public class PlaylistSettings : SettingsBase
    {
        private MediaPlaybackAutoRepeatMode _RepeatMode = MediaPlaybackAutoRepeatMode.List;

        [DataMember]
        public MediaPlaybackAutoRepeatMode RepeatMode
        {
            get { return _RepeatMode; }
			set { SetProperty(ref _RepeatMode, value); }
        }


        private bool _IsShuffleEnable = false;

        [DataMember]
        public bool IsShuffleEnable
        {
            get { return _IsShuffleEnable; }
            set { SetProperty(ref _IsShuffleEnable, value); }
        }


        private bool _IsReverseModeEnable = false;

        [DataMember]
        public bool IsReverseModeEnable
        {
            get { return _IsReverseModeEnable; }
            set { SetProperty(ref _IsReverseModeEnable, value); }
        }



        private PlaylistEndAction _PlaylistEndAction;

        [DataMember]
        public PlaylistEndAction PlaylistEndAction
        {
            get { return _PlaylistEndAction; }
            set { SetProperty(ref _PlaylistEndAction, value); }
        }


        private bool _AutoMoveNextVideoOnPlaylistEmpty = true;

        [DataMember]
        public bool AutoMoveNextVideoOnPlaylistEmpty
        {
            get { return _AutoMoveNextVideoOnPlaylistEmpty; }
            set { SetProperty(ref _AutoMoveNextVideoOnPlaylistEmpty, value); }
        }

        

        private DelegateCommand _ToggleRepeatModeCommand;
        public DelegateCommand ToggleRepeatModeCommand
        {
            get
            {
                return _ToggleRepeatModeCommand
                    ?? (_ToggleRepeatModeCommand = new DelegateCommand(() =>
                    {
                        switch (RepeatMode)
                        {
                            case MediaPlaybackAutoRepeatMode.None:
                                RepeatMode = MediaPlaybackAutoRepeatMode.Track;
                                break;
                            case MediaPlaybackAutoRepeatMode.Track:
                                RepeatMode = MediaPlaybackAutoRepeatMode.List;
                                break;
                            case MediaPlaybackAutoRepeatMode.List:
                                RepeatMode = MediaPlaybackAutoRepeatMode.None;
                                break;
                            default:
                                break;
                        }
                    }
                    ));
            }
        }

        private DelegateCommand _ToggleShuffleCommand;
        public DelegateCommand ToggleShuffleCommand
        {
            get
            {
                return _ToggleShuffleCommand
                    ?? (_ToggleShuffleCommand = new DelegateCommand(() =>
                    {
                        IsShuffleEnable = !IsShuffleEnable;
                    }
                    ));
            }
        }

    }


    public enum PlaylistEndAction
    {
        NothingDo,
        ChangeIntoSplit,
        CloseIfPlayWithCurrentWindow,
    }
}
