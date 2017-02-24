﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NicoPlayerHohoema.Models;
using Reactive.Bindings;
using System.Reactive.Linq;
using Reactive.Bindings.Extensions;

namespace NicoPlayerHohoema.ViewModels
{
    public class LoginPageViewModel : HohoemaViewModelBase
    {
        public string VersionText { get; private set; }

        public ReactiveProperty<string> Mail { get; private set; }
        public ReactiveProperty<string> Password { get; private set; }
        public ReactiveProperty<bool> IsRememberPassword { get; private set; }

        public ReactiveProperty<bool> IsValidAccount { get; private set; }
        public ReactiveProperty<bool> NowProcessLoggedIn { get; private set; }

        public ReactiveProperty<bool> IsAuthoricationFailed { get; private set; }
        public ReactiveProperty<bool> IsServiceUnavailable { get; private set; }

        public ReactiveCommand TryLoginCommand { get; private set; }

        public ReactiveProperty<string> LoginErrorText { get; private set; }


        public LoginPageViewModel(HohoemaApp hohoemaApp, PageManager pageManager) 
            : base(hohoemaApp, pageManager, canActivateBackgroundUpdate:false)
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            VersionText = $"{version.Major}.{version.Minor}.{version.Build}";

            var accountInfo = AccountManager.GetPrimaryAccount();
            Mail = new ReactiveProperty<string>(accountInfo?.Item1, mode: ReactivePropertyMode.DistinctUntilChanged);
            Password = new ReactiveProperty<string>(accountInfo?.Item2, mode: ReactivePropertyMode.DistinctUntilChanged);

            IsRememberPassword = new ReactiveProperty<bool>( !string.IsNullOrEmpty(Password.Value));

            IsValidAccount = new ReactiveProperty<bool>(hohoemaApp.IsLoggedIn);
            NowProcessLoggedIn = new ReactiveProperty<bool>(false);
            IsAuthoricationFailed = new ReactiveProperty<bool>(false);
            IsServiceUnavailable = new ReactiveProperty<bool>(false);

            LoginErrorText = HohoemaApp.ObserveProperty(x => x.LoginErrorText)
                .ToReactiveProperty();

            // メールかパスワードが変更されたらログイン検証されていないアカウントとしてマーク
            TryLoginCommand = Observable.CombineLatest(
                Mail.Select(x => !string.IsNullOrEmpty(x)),
                Password.Select(x => !string.IsNullOrEmpty(x)),
                NowProcessLoggedIn.Select(x => !x)
                )
                .Select(x => x.All(y => y))
                .ToReactiveCommand();
                
            TryLoginCommand.Subscribe(async _ =>
            {
                NowProcessLoggedIn.Value = true;

                try
                {
                    await TryLogin();
                }
                finally
                {
                    NowProcessLoggedIn.Value = false;
                }
            });

        }

        private async Task TryLogin()
        {
            // Note: NiconicoContextのインスタンスを作成してサインインを試行すると
            // HttpClientのキャッシュ削除がされていない状態で試行されてしまい
            // 正常な結果を得られません。
            // HohoemaApp上で管理しているNiconicoContextのみに限定することで
            // HttpClientのキャッシュが残る挙動に対処しています

            IsAuthoricationFailed.Value = false;
            IsServiceUnavailable.Value = false;

            var result = await HohoemaApp.SignIn(Mail.Value, Password.Value);
            IsValidAccount.Value = result == Mntone.Nico2.NiconicoSignInStatus.Success;
            IsAuthoricationFailed.Value = result == Mntone.Nico2.NiconicoSignInStatus.Failed;
            IsServiceUnavailable.Value = result == Mntone.Nico2.NiconicoSignInStatus.ServiceUnavailable;


            if (IsValidAccount.Value)
            {
                AccountManager.SetPrimaryAccountId(Mail.Value);

                if (IsRememberPassword.Value)
                {
                    AccountManager.AddOrUpdateAccount(Mail.Value, Password.Value);
                }
                else
                {
                    AccountManager.RemoveAccount(Mail.Value);
                }


                // TODO: 初期セットアップ補助ページを開く？

                PageManager.OpenPage(HohoemaPageType.Portal);
            }
        }

    }
}
