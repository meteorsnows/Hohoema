﻿using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity;
using System.Diagnostics;
using NicoPlayerHohoema.Models;
using NicoPlayerHohoema.Services;

namespace NicoPlayerHohoema.Commands.Mylist
{
    public sealed class CreateMylistCommand : DelegateCommandBase
    {
        public CreateMylistCommand(
            UserMylistManager userMylistManager,
            DialogService dialogService
            )
        {
            UserMylistManager = userMylistManager;
            DialogService = dialogService;
        }

        public UserMylistManager UserMylistManager { get; }
        public DialogService DialogService { get; }

        protected override bool CanExecute(object parameter)
        {
            if (parameter == null) { return false; }

            return parameter is Interfaces.IVideoContent 
                || Mntone.Nico2.NiconicoRegex.IsVideoId(parameter as string);
        }

        protected override async void Execute(object parameter)
        {
            var data = new Dialogs.MylistGroupEditData() { };
            var result = await DialogService.ShowCreateMylistGroupDialogAsync(data);
            if (result)
            {
                var mylistCreateResult = await UserMylistManager.AddMylist(data.Name, data.Description, data.IsPublic, data.MylistDefaultSort, data.IconType);

                Debug.WriteLine("マイリスト作成：" + mylistCreateResult);
            }

            var mylist = UserMylistManager.Mylists.FirstOrDefault(x => x.Label == data.Name);
            

            if (parameter is Interfaces.IVideoContent content)
            {
                await mylist.AddMylistItem(content.Id);
            }
            else if (parameter is string videoId)
            {
                await mylist.AddMylistItem(videoId);
            }
        }
    }
}
