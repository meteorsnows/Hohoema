﻿using NicoPlayerHohoema.Models;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Prism.Windows.Navigation;
using System.Threading;
using NicoPlayerHohoema.Helpers;
using Windows.UI.Xaml.Navigation;
using Prism.Commands;
using NicoPlayerHohoema.Services;
using Windows.System;
using System.Collections.Async;

namespace NicoPlayerHohoema.ViewModels
{
    public class CacheManagementPageViewModel : HohoemaVideoListingPageViewModelBase<CacheVideoViewModel>
	{

        public ReadOnlyReactiveProperty<bool> IsCacheUserAccepted { get; private set; }
        public ReactiveProperty<bool> IsRequireUpdateCacheSaveFolder { get; private set; }

        public ReactiveProperty<string> CacheSaveFolderPath { get; private set; }
        public DelegateCommand OpenCurrentCacheFolderCommand { get; private set; }
        public ReactiveProperty<string> CacheFolderStateDescription { get; private set; }


        public DelegateCommand ChangeCacheFolderCommand { get; private set; }
        public DelegateCommand CheckExistCacheFolderCommand { get; private set; }
        public DelegateCommand RequireEnablingCacheCommand { get; private set; }
        public DelegateCommand ReadCacheAcceptTextCommand { get; private set; }

        private DelegateCommand _ResumeCacheCommand;
        public DelegateCommand ResumeCacheCommand
        {
            get
            {
                return _ResumeCacheCommand
                    ?? (_ResumeCacheCommand = new DelegateCommand(() =>
                    {
                        // TODO: バックグラウンドダウンロードの強制更新？
                        //await _MediaManager.StartBackgroundDownload();
                    }));
            }
        }

        VideoCacheManager _MediaManager;

        DialogService _HohoemaDialogService;


        public NotificationService NotificationService { get; }

        public CacheManagementPageViewModel(
            HohoemaApp app, 
            PageManager pageManager,
            DialogService dialogService,
            NotificationService notificationService
            )
			: base(app, pageManager)
		{
			_MediaManager = app.CacheManager;
            _HohoemaDialogService = dialogService;
            NotificationService = notificationService;

            IsRequireUpdateCacheSaveFolder = new ReactiveProperty<bool>(false);

            IsCacheUserAccepted = HohoemaApp.UserSettings.CacheSettings.ObserveProperty(x => x.IsUserAcceptedCache)
                .ToReadOnlyReactiveProperty();

            RequireEnablingCacheCommand = new DelegateCommand(async () => 
            {
                var result = await _HohoemaDialogService.ShowAcceptCacheUsaseDialogAsync();
                if (result)
                {
                    HohoemaApp.UserSettings.CacheSettings.IsEnableCache = true;
                    HohoemaApp.UserSettings.CacheSettings.IsUserAcceptedCache = true;
                    (App.Current).Resources["IsCacheEnabled"] = true;

                    await RefreshCacheSaveFolderStatus();

                    NotificationService.ShowInAppNotification(
                        InAppNotificationPayload.CreateReadOnlyNotification("キャッシュの保存先フォルダを選択してください。\n保存先が選択されると利用準備が完了します。",
                        showDuration: TimeSpan.FromSeconds(30)
                        ));

                    if (await HohoemaApp.ChangeUserDataFolder())
                    {
                        await RefreshCacheSaveFolderStatus();
                        await ResetList();

                        NotificationService.ShowInAppNotification(
                            InAppNotificationPayload.CreateReadOnlyNotification("キャッシュの利用準備が出来ました")
                            );
                    }
                }
            });

            ReadCacheAcceptTextCommand = new DelegateCommand(async () =>
            {
                var result = await _HohoemaDialogService.ShowAcceptCacheUsaseDialogAsync(showWithoutConfirmButton:true);
            });



            CacheFolderStateDescription = new ReactiveProperty<string>("");
            CacheSaveFolderPath = new ReactiveProperty<string>("");

            OpenCurrentCacheFolderCommand = new DelegateCommand(async () =>
            {
                await RefreshCacheSaveFolderStatus();

                var folder = await HohoemaApp.GetVideoCacheFolder();
                if (folder != null)
                {
                    await Launcher.LaunchFolderAsync(folder);
                }
            });
            

            ChangeCacheFolderCommand = new DelegateCommand(async () =>
            {
                var prevPath = CacheSaveFolderPath.Value;

                if (await HohoemaApp.ChangeUserDataFolder())
                {
                    NotificationService.ShowInAppNotification(
                        InAppNotificationPayload.CreateReadOnlyNotification($"キャッシュの保存先を {CacheSaveFolderPath.Value} に変更しました")
                        );

                    await RefreshCacheSaveFolderStatus();
                    await ResetList();
                }
            });
        }


        #region Implement HohoemaVideListViewModelBase

        protected override async Task NavigatedToAsync(CancellationToken cancelToken, NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
        {
            await RefreshCacheSaveFolderStatus();

            if (IsRequireUpdateCacheSaveFolder.Value)
            {
                NotificationService.ShowInAppNotification(
                    InAppNotificationPayload.CreateReadOnlyNotification("キャッシュの保存先フォルダを選択してください。\n保存先が選択されると利用準備が完了します。",
                    showDuration: TimeSpan.FromSeconds(30)
                    ));

                if (await HohoemaApp.ChangeUserDataFolder())
                {
                    await RefreshCacheSaveFolderStatus();
                    await ResetList();

                    NotificationService.ShowInAppNotification(
                        InAppNotificationPayload.CreateReadOnlyNotification("キャッシュの利用準備が出来ました")
                        );
                }


            }

            await base.NavigatedToAsync(cancelToken, e, viewModelState);
        }




        public override void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
        {
            Windows.UI.Xaml.Window.Current.Activated += Current_Activated;
            base.OnNavigatedTo(e, viewModelState);
        }

        public override void OnNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewModelState, bool suspending)
        {
            if (!suspending)
            {
                Windows.UI.Xaml.Window.Current.Activated -= Current_Activated;
            }

            base.OnNavigatingFrom(e, viewModelState, suspending);
        }

        // ウィンドウがアクティブになったタイミングで
        // キャッシュフォルダ―が格納されたストレージをホットスタンバイ状態にしたい
        // （コールドスタンバイ状態だと再生開始までのラグが大きい）
        private async void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.CodeActivated)
            {
                var folder = await HohoemaApp.GetVideoCacheFolder();
            }
        }




        protected override IIncrementalSource<CacheVideoViewModel> GenerateIncrementalSource()
		{
			return new CacheVideoInfoLoadingSource(HohoemaApp.CacheManager);
		}

		protected override bool CheckNeedUpdateOnNavigateTo(NavigationMode mode)
		{
			return mode == NavigationMode.New;
		}

		protected override void PostResetList()
		{
			
		}




        #endregion

        private async Task RefreshCacheSaveFolderStatus()
        {
            var cacheFolderAccessState = await HohoemaApp.GetVideoCacheFolderState();

            CacheSaveFolderPath.Value = "";
            switch (cacheFolderAccessState)
            {
                case CacheFolderAccessState.NotAccepted:
                    CacheFolderStateDescription.Value = "キャッシュ利用の同意が必要です。 「キャッシュを有効にする」ボタンを押すと同意文書が表示されます。";
                    break;
                case CacheFolderAccessState.NotEnabled:
                    CacheFolderStateDescription.Value = "キャッシュの有効化が必要です";
                    break;
                case CacheFolderAccessState.NotSelected:
                    CacheFolderStateDescription.Value = "フォルダを選択するとキャッシュ機能が使えるようになります";
                    break;
                case CacheFolderAccessState.SelectedButNotExist:
                    CacheFolderStateDescription.Value = "選択されたフォルダが確認できません。外付けストレージを再接続するか、キャッシュ先フォルダを再選択してください。";
                    CacheSaveFolderPath.Value = "?????";
                    break;
                case CacheFolderAccessState.Exist:
                    CacheFolderStateDescription.Value = "キャッシュ利用の準備ができました";
                    break;
                default:
                    break;
            }

            var folder = await HohoemaApp.GetVideoCacheFolder();
            if (folder != null)
            {
                CacheSaveFolderPath.Value = $"{folder.Path}";
            }


            IsRequireUpdateCacheSaveFolder.Value = 
                cacheFolderAccessState == CacheFolderAccessState.SelectedButNotExist
                || cacheFolderAccessState == CacheFolderAccessState.NotSelected
                ;
        }

    }


    public class CacheVideoViewModel : VideoInfoControlViewModel
	{
        public DateTime CacheRequestTime { get; private set; }

        IList<NicoVideoCacheRequest> Requests;

        public CacheVideoViewModel(string videoId, IList<NicoVideoCacheRequest> requests, VideoCacheManager cacheManager)
			: base(videoId, isNgEnabled:false)
		{
            CacheRequestTime = requests.First().RequestAt;
            Requests = requests;
        }

        protected override VideoPlayPayload MakeVideoPlayPayload()
		{
			var payload = base.MakeVideoPlayPayload();

//			payload.Quality = Quality;

			return payload;
		}
    }


	public class CacheVideoInfoLoadingSource : HohoemaIncrementalSourceBase<CacheVideoViewModel>
	{
        public override uint OneTimeLoadCount => (uint)10;

        VideoCacheManager _VideoCacheManager;

        List<string> _CacheRequestedItems;

        Dictionary<string, List<NicoVideoCacheRequest>> _CacheRequestMap = new Dictionary<string, List<NicoVideoCacheRequest>>();

        public CacheVideoInfoLoadingSource(VideoCacheManager cacheManager)
            : base()
		{
            _VideoCacheManager = cacheManager;

        }




        protected override Task<IAsyncEnumerable<CacheVideoViewModel>> GetPagedItemsImpl(int head, int count)
        {
            return Task.FromResult(_CacheRequestedItems.Skip(head).Take(count)
                .Select(x => new { VideoId = x, CacheRequests = _CacheRequestMap[x] })
                .Select(x =>
                {
                    var vm = new CacheVideoViewModel(x.VideoId, x.CacheRequests, _VideoCacheManager);

                    return vm;
                }).ToAsyncEnumerable());

        }

        protected override async Task<int> ResetSourceImpl()
        {
            await _VideoCacheManager.Initialize();


            _CacheRequestMap.Clear();

            var list = new List<NicoVideoCacheRequest>();

            // キャッシュ待ちアイテム
            // キャッシュ中アイテム
            // キャッシュ済みアイテム
            foreach (var item in await _VideoCacheManager.EnumerateCacheRequestedVideosAsync())
            {
                if (!_CacheRequestMap.ContainsKey(item.RawVideoId))
                {
                    list.Add(item);

                    _CacheRequestMap.Add(item.RawVideoId, new List<NicoVideoCacheRequest>());
                }

                _CacheRequestMap[item.RawVideoId].Add(item);
            }

            

            _CacheRequestedItems = list.OrderBy(x => x.RequestAt.Ticks).Reverse().Select(x => x.RawVideoId).ToList();

            return _CacheRequestedItems.Count;
        }
    }

}
