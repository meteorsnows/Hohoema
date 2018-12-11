﻿using NicoPlayerHohoema.ViewModels;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Prism.Unity.Windows;
using Microsoft.Practices.Unity;
using NicoPlayerHohoema.Models;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Prism.Events;
using NicoPlayerHohoema.Events;
using Prism.Windows.Navigation;
using Prism.Windows.AppModel;
using Prism.Windows.Mvvm;
//using BackgroundAudioShared;
using Windows.Media;
using Windows.Storage;
using System.Text;
using NicoPlayerHohoema.Helpers;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.DataTransfer;
using Mntone.Nico2;
using Prism.Commands;
using System.Text.RegularExpressions;
using System.Threading;
using Windows.UI;

namespace NicoPlayerHohoema
{
    /// <summary>
    /// 既定の Application クラスを補完するアプリケーション固有の動作を提供します。
    /// </summary>
    sealed partial class App : Prism.Unity.Windows.PrismUnityApplication
    {
        const bool _DEBUG_XBOX_RESOURCE = false;


        public SplashScreen SplashScreen { get; private set; }

        private bool _IsPreLaunch;

		public const string ACTIVATION_WITH_ERROR = "error";

        
        internal const string IS_COMPLETE_INTRODUCTION = "is_first_launch";

		/// <summary>
		/// 単一アプリケーション オブジェクトを初期化します。これは、実行される作成したコードの
		///最初の行であるため、main() または WinMain() と論理的に等価です。
		/// </summary>
		public App()
        {
			UnhandledException += PrismUnityApplication_UnhandledException;

            // XboxOne向けの設定
            // 基本カーソル移動で必要なときだけポインターを出現させる
            this.RequiresPointerMode = Windows.UI.Xaml.ApplicationRequiresPointerMode.WhenRequested;

            // テーマ設定
            // ThemeResourceの切り替えはアプリの再起動が必要
            RequestedTheme = GetTheme();

            
            Microsoft.Toolkit.Uwp.UI.ImageCache.Instance.CacheDuration = TimeSpan.FromDays(7);
            Microsoft.Toolkit.Uwp.UI.ImageCache.Instance.MaxMemoryCacheCount = 200;
            Microsoft.Toolkit.Uwp.UI.ImageCache.Instance.RetryCount = 3;


            this.InitializeComponent();

        }



        /*
		private async void App_Suspending(object sender, SuspendingEventArgs e)
		{
			
			var deferral = e.SuspendingOperation.GetDeferral();
			var hohoemaApp = Container.Resolve<HohoemaApp>();
			await hohoemaApp.OnSuspending();

			deferral.Complete();
		}
		*/


        private async Task RegisterTypes()
        {            
            // Service
            var dialogService = new Services.DialogService();
            Container.RegisterInstance(dialogService);

            // Models
            var secondaryViewMan = new HohoemaViewManager();
            var hohoemaApp = await HohoemaApp.Create(EventAggregator, secondaryViewMan, dialogService);
            Container.RegisterInstance(secondaryViewMan);
            Container.RegisterInstance(hohoemaApp);
            Container.RegisterInstance(new PageManager(hohoemaApp, NavigationService, hohoemaApp.UserSettings.AppearanceSettings, hohoemaApp.Playlist, secondaryViewMan, dialogService));
            Container.RegisterInstance(hohoemaApp.ContentProvider);
            Container.RegisterInstance(hohoemaApp.Playlist);
            Container.RegisterInstance(hohoemaApp.OtherOwneredMylistManager);
            Container.RegisterInstance(hohoemaApp.FeedManager);
            Container.RegisterInstance(hohoemaApp.CacheManager);
            Container.RegisterInstance(hohoemaApp.UserSettings);
            Container.RegisterInstance(new Models.Niconico.Live.NicoLiveSubscriber(hohoemaApp));

            Container.RegisterInstance(new Microsoft.Toolkit.Uwp.Helpers.LocalObjectStorageHelper());



            // サブスクリプション（動画の新着自動チェック機能）の初期化
            Models.Subscription.SubscriptionManager.Initialize(hohoemaApp.ContentProvider, Container.Resolve<Services.NotificationService>());
            Models.Subscription.WatchItLater.Instance.ContentProvider = hohoemaApp.ContentProvider;

            // ViewModels
            Container.RegisterType<ViewModels.RankingCategoryListPageViewModel>(new ContainerControlledLifetimeManager());

            Resources.Add("IsXbox", Helpers.DeviceTypeHelper.IsXbox);
            Resources.Add("IsMobile", Helpers.DeviceTypeHelper.IsMobile);

            Resources.Add("IsCacheEnabled", hohoemaApp.UserSettings.CacheSettings.IsEnableCache);
            Resources.Add("IsTVModeEnabled", Helpers.DeviceTypeHelper.IsXbox || hohoemaApp.UserSettings.AppearanceSettings.IsForceTVModeEnable);


#if DEBUG
            //			BackgroundUpdater.MaxTaskSlotCount = 1;
#endif
            // TODO: プレイヤーウィンドウ上で管理する
            //			var backgroundTask = MediaBackgroundTask.Create();
            //			Container.RegisterInstance(backgroundTask);


        }

        public bool IsTitleBarCustomized { get; } = Helpers.DeviceTypeHelper.IsDesktop && Helpers.InputCapabilityHelper.IsMouseCapable;
        protected override async Task OnInitializeAsync(IActivatedEventArgs args)
        {
            await RegisterTypes();


#if DEBUG
            Views.UINavigationManager.Pressed += UINavigationManager_Pressed;
#endif


#if DEBUG
            if (_DEBUG_XBOX_RESOURCE)
#else
                if (Helpers.DeviceTypeHelper.IsXbox)
#endif
            {
                this.Resources.MergedDictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("ms-appx:///Styles/TVSafeColor.xaml")
                });
                this.Resources.MergedDictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("ms-appx:///Styles/TVStyle.xaml")
                });
            }



            Resources.Add("TitleBarCustomized", IsTitleBarCustomized);
            Resources.Add("TitleBarDummyHeight", IsTitleBarCustomized ? 32.0 : 0.0);

            if (IsTitleBarCustomized)
            {

                var coreApp = CoreApplication.GetCurrentView();
                coreApp.TitleBar.ExtendViewIntoTitleBar = true;

                var appView = ApplicationView.GetForCurrentView();
                appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appView.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                appView.TitleBar.ButtonInactiveForegroundColor = Colors.Transparent;

                if (RequestedTheme == ApplicationTheme.Light)
                {
                    appView.TitleBar.ButtonForegroundColor = Colors.Black;
                    appView.TitleBar.ButtonHoverBackgroundColor = Colors.DarkGray;
                    appView.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                }
            }


            // ウィンドウサイズの保存と復元
            if (Helpers.DeviceTypeHelper.IsDesktop)
            {
                var localObjectStorageHelper = Container.Resolve<Microsoft.Toolkit.Uwp.Helpers.LocalObjectStorageHelper>();
                if (localObjectStorageHelper.KeyExists(HohoemaViewManager.primary_view_size))
                {
                    var view = ApplicationView.GetForCurrentView();
                    MainViewId = view.Id;
                    _PrevWindowSize = localObjectStorageHelper.Read<Size>(HohoemaViewManager.primary_view_size);
                    view.TryResizeView(_PrevWindowSize);
                    ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
                }
            }

            // XboxOneで外枠表示を行わないように設定
            if (Helpers.DeviceTypeHelper.IsXbox)
            {
                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode
                    (Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
            }

            // モバイルでナビゲーションバーをアプリに被せないように設定
            if (Helpers.DeviceTypeHelper.IsMobile)
            {
                // モバイルで利用している場合に、ナビゲーションバーなどがページに被さらないように指定
                ApplicationView.GetForCurrentView().SuppressSystemOverlays = true;
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
            }



            // ウィンドウを有効化したタイミングでクリップボードをチェックする
            Window.Current.CoreWindow.Activated += async (__, activatedArgs) =>
            {
                var clipboardService = Container.Resolve<Services.HohoemaClipboardService>();

                if (activatedArgs.WindowActivationState == CoreWindowActivationState.PointerActivated)
                {
                    var clipboard = await clipboardService.CheckClipboard();
                    if (clipboard != null)
                    {
                        var hohoemaNotificationService = Container.Resolve<Services.HohoemaNotificationService>();
                        hohoemaNotificationService.ShowInAppNotification(clipboard.Type, clipboard.Id);
                    }
                }
            };

            var pageManager = Container.Resolve<PageManager>();
            var hohoemaApp = Container.Resolve<HohoemaApp>();

            try
            {
                await hohoemaApp.InitializeAsync().ConfigureAwait(false);
            }
            catch
            {
                Debug.WriteLine("HohoemaAppの初期化に失敗");
            }


#if false
            try
            {
                if (localStorge.Read(IS_COMPLETE_INTRODUCTION, false) == false)
                {
                    // アプリのイントロダクションを開始
                    pageManager.OpenIntroductionPage();
                }
                else
                {
                    pageManager.OpenStartupPage();
                }
            }
            catch
            {
                Debug.WriteLine("イントロダクションまたはスタートアップのページ表示に失敗");
                pageManager.OpenPage(HohoemaPageType.RankingCategoryList);
            }
#else
            try
            {
                pageManager.OpenStartupPage();
            }
            catch
            {
                Debug.WriteLine("スタートアップのページ表示に失敗");
                pageManager.OpenPage(HohoemaPageType.RankingCategoryList);
            }
#endif



            try
            {
                // ログインを試行
                if (!hohoemaApp.IsLoggedIn && AccountManager.HasPrimaryAccount())
                {
                    // サインイン処理の待ちを初期化内でしないことで初期画面表示を早める
                    _ = hohoemaApp.SignInWithPrimaryAccount();
                }
            }
            catch
            {
                Debug.WriteLine("ログイン処理に失敗");
            }

            // 購読機能を初期化
            Models.Subscription.WatchItLater.Instance.Initialize();

            await base.OnInitializeAsync(args);
        }



        protected override Task OnResumeApplicationAsync(IActivatedEventArgs args)
        {
            var hohoemaApp = Container.Resolve<HohoemaApp>();

            try
            {
                hohoemaApp.Resumed();
            }
            catch
            {
                Debug.WriteLine("アプリモデルの復帰処理でエラーを検出しました。");
                throw;
            }

            return base.OnResumeApplicationAsync(args);
        }

        protected override Task OnLaunchApplicationAsync(LaunchActivatedEventArgs args)
        {
            SplashScreen = args.SplashScreen;
#if DEBUG
            DebugSettings.IsBindingTracingEnabled = true;
#endif
            _IsPreLaunch = args.PrelaunchActivated;

            return Task.CompletedTask;
        }

        
        protected override async Task OnActivateApplicationAsync(IActivatedEventArgs args)
		{
            var pageManager = Container.Resolve<PageManager>();
            var hohoemaApp = Container.Resolve<HohoemaApp>();

            try
            {
                if (!hohoemaApp.IsLoggedIn && AccountManager.HasPrimaryAccount())
                {
                    await hohoemaApp.SignInWithPrimaryAccount();
                }
            }
            catch { }

            // ログインしていない場合、
            bool isNeedNavigationDefault = !hohoemaApp.IsLoggedIn;

            try
            {
                if (args.Kind == ActivationKind.ToastNotification)
                {
                    //Get the pre-defined arguments and user inputs from the eventargs;
                    var toastArgs = args as IActivatedEventArgs as ToastNotificationActivatedEventArgs;
                    var arguments = toastArgs.Argument;

                    var serialize = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginRedirectPayload>(arguments);
                    if (serialize != null)
                    {
                        if (serialize.RedirectPageType == HohoemaPageType.VideoPlayer)
                        {
                            PlayVideoFromExternal(serialize.RedirectParamter);
                        }
                        else
                        {
                            pageManager.OpenPage(serialize.RedirectPageType, serialize.RedirectParamter);
                        }
                    }
                    else if (arguments == ACTIVATION_WITH_ERROR)
                    {
                        await ShowErrorLog().ConfigureAwait(false);
                    }
                    else if (arguments.StartsWith("cache_cancel"))
                    {
                        var query = arguments.Split('?')[1];
                        var decode = new WwwFormUrlDecoder(query);

                        var videoId = decode.GetFirstValueByName("id");
                        var quality = (NicoVideoQuality)Enum.Parse(typeof(NicoVideoQuality), decode.GetFirstValueByName("quality"));

                        await hohoemaApp.CacheManager.CancelCacheRequest(videoId, quality);
                    }
                    else
                    {
                        var nicoContentId = Helpers.NicoVideoExtention.UrlToVideoId(arguments);

                        if (Mntone.Nico2.NiconicoRegex.IsVideoId(nicoContentId))
                        {
                            PlayVideoFromExternal(nicoContentId);
                        }
                        else if (Mntone.Nico2.NiconicoRegex.IsLiveId(nicoContentId))
                        {
                            PlayLiveVideoFromExternal(nicoContentId);
                        }
                    }
                }
                else if (args.Kind == ActivationKind.Protocol)
                {
                    var param = (args as IActivatedEventArgs) as ProtocolActivatedEventArgs;
                    var uri = param.Uri;
                    var maybeNicoContentId = new string(uri.OriginalString.Skip("niconico://".Length).TakeWhile(x => x != '?' && x != '/').ToArray());


                    if (Mntone.Nico2.NiconicoRegex.IsVideoId(maybeNicoContentId)
                        || maybeNicoContentId.All(x => x >= '0' && x <= '9'))
                    {
                        PlayVideoFromExternal(maybeNicoContentId);
                    }
                    else if (Mntone.Nico2.NiconicoRegex.IsLiveId(maybeNicoContentId))
                    {
                        PlayLiveVideoFromExternal(maybeNicoContentId);
                    }
                }
                else
                {
                    if (hohoemaApp.IsLoggedIn)
                    {
                        pageManager.OpenStartupPage();
                    }
                    else
                    {
                        pageManager.OpenPage(HohoemaPageType.RankingCategoryList);
                    }
                }
            }
            catch
            {
                
            }
			
			await base.OnActivateApplicationAsync(args);
		}

        private void PlayVideoFromExternal(string videoId, string videoTitle = null, NicoVideoQuality? quality = null)
        {
            var hohoemaApp = Container.Resolve<HohoemaApp>();
            var pageManager = Container.Resolve<PageManager>();

            // TODO: ログインが必要な動画かをチェックしてログインダイアログを出す

            hohoemaApp.Playlist.PlayVideo(videoId, videoTitle, quality);
        }
        private void PlayLiveVideoFromExternal(string videoId)
        {
            // TODO: ログインが必要な生放送かをチェックしてログインダイアログを出す


            var pageManager = Container.Resolve<PageManager>();
            var hohoemaApp = Container.Resolve<HohoemaApp>();

            hohoemaApp.Playlist.PlayLiveVideo(videoId);
        }


		/// <summary>
		/// 動画キャッシュ保存先フォルダをチェックします
		/// 選択済みだがフォルダが見つからない場合に、トースト通知を行います。
		/// </summary>
		/// <returns></returns>
		public async Task CheckVideoCacheFolderState()
		{
			var hohoemaApp = Container.Resolve<HohoemaApp>();
			var cacheFolderState = await hohoemaApp.GetVideoCacheFolderState();

			if (cacheFolderState == CacheFolderAccessState.SelectedButNotExist)
			{
				var toastService = Container.Resolve<Services.NotificationService>();
				toastService.ShowToast(
					"キャッシュが利用できません"
					, "キャッシュ保存先フォルダが見つかりません。（ここをタップで設定画面を表示）"
					, duration: Microsoft.Toolkit.Uwp.Notifications.ToastDuration.Long
					, toastActivatedAction: async () =>
					{
						await HohoemaApp.UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => 
						{
							var pm = Container.Resolve<PageManager>();
							pm.OpenPage(HohoemaPageType.CacheManagement);
						});
					});
			}
		}


        #region Page and Application Appiarance

        protected override IDeviceGestureService OnCreateDeviceGestureService()
        {
            var dgs = base.OnCreateDeviceGestureService();

            // TitleBarCustomized に合わせて
            dgs.UseTitleBarBackButton = !IsTitleBarCustomized;
            return dgs;
        }

        protected override void ConfigureViewModelLocator()
        {

            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(viewType => 
            {
                var pageToken = viewType.Name;

                if (pageToken.EndsWith("_TV"))
                {
                    pageToken = pageToken.Remove(pageToken.IndexOf("_TV"));
                }
                else if (pageToken.EndsWith("_Mobile"))
                {
                    pageToken = pageToken.Remove(pageToken.IndexOf("_Mobile"));
                }

                var assemblyQualifiedAppType = viewType.AssemblyQualifiedName;

                var pageNameWithParameter = assemblyQualifiedAppType.Replace(viewType.FullName, "NicoPlayerHohoema.ViewModels.{0}ViewModel");

                var viewModelFullName = string.Format(CultureInfo.InvariantCulture, pageNameWithParameter, pageToken);
                var viewModelType = Type.GetType(viewModelFullName);

                if (viewModelType == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, pageToken, this.GetType().Namespace + ".ViewModels"),
                        "pageToken");
                }

                return viewModelType;

            });

            base.ConfigureViewModelLocator();
        }

        protected override Type GetPageType(string pageToken)
        {
            var hohoemaApp = Container.Resolve<HohoemaApp>();
            var isForceTVModeEnable = hohoemaApp?.UserSettings?.AppearanceSettings.IsForceTVModeEnable ?? false;
            var isForceMobileModeEnable = hohoemaApp?.UserSettings?.AppearanceSettings.IsForceMobileModeEnable ?? false;

            Type viewType = null;
            if (isForceTVModeEnable || Helpers.DeviceTypeHelper.IsXbox)
            {
                // pageTokenに対応するXbox表示用のページの型を取得
                try
                {
                    var assemblyQualifiedAppType = this.GetType().AssemblyQualifiedName;

                    var pageNameWithParameter = assemblyQualifiedAppType.Replace(this.GetType().FullName, this.GetType().Namespace + ".Views.{0}Page_TV");

                    var viewFullName = string.Format(CultureInfo.InvariantCulture, pageNameWithParameter, pageToken);
                    viewType = Type.GetType(viewFullName);
                }
                catch { }
            }
            else if (isForceMobileModeEnable || Helpers.DeviceTypeHelper.IsMobile)
            {
                try
                {
                    var assemblyQualifiedAppType = this.GetType().AssemblyQualifiedName;

                    var pageNameWithParameter = assemblyQualifiedAppType.Replace(this.GetType().FullName, this.GetType().Namespace + ".Views.{0}Page_Mobile");

                    var viewFullName = string.Format(CultureInfo.InvariantCulture, pageNameWithParameter, pageToken);
                    viewType = Type.GetType(viewFullName);
                }
                catch { }
            }

            return viewType ?? base.GetPageType(pageToken);
        }




        protected override UIElement CreateShell(Frame rootFrame)
        {
            rootFrame.CacheSize = 5;

            rootFrame.Navigating += (__, e) => 
            {
                try
                {
                    var playlist = Container.Resolve<HohoemaPlaylist>();
                    // プレイヤーをメインウィンドウでウィンドウいっぱいに表示しているときだけ
                    // バックキーの動作をUIの表示/非表示切り替えに割り当てる
                    if (playlist.IsDisplayMainViewPlayer && playlist.PlayerDisplayType == PlayerDisplayType.PrimaryView)
                    {
                        playlist.IsDisplayPlayerControlUI = !playlist.IsDisplayPlayerControlUI;
                        e.Cancel = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    WriteErrorFile(ex).ConfigureAwait(false);
                }
            };
            rootFrame.NavigationFailed += (_, e) => 
            {
                Debug.WriteLine("Page navigation failed!!");
                Debug.WriteLine(e.SourcePageType.AssemblyQualifiedName);
                Debug.WriteLine(e.Exception.ToString());

                _ = WriteErrorFile(e.Exception, e.SourcePageType?.AssemblyQualifiedName);
            };

            var menuPageBase = new Views.MenuNavigatePageBase()
            {
                Content = rootFrame
            };

            var hohoemaInAppNotification = new Views.HohoemaInAppNotification()
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var grid = new Grid()
            {
                Children =
                {
                    menuPageBase,
                    hohoemaInAppNotification,
                }
            };


#if DEBUG
            menuPageBase.FocusEngaged += (__, args) => Debug.WriteLine("focus engagad: " + args.OriginalSource.ToString());
            
#endif

            return grid;
        }

        #endregion


        #region Multi Window Size Restoring


        private int MainViewId = -1;
        private Size _PrevWindowSize;

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
		{
			base.OnWindowCreated(args);

            var view = ApplicationView.GetForCurrentView();
            view.VisibleBoundsChanged += (sender, e) => 
            {
                if (MainViewId == sender.Id)
                {
                    var localObjectStorageHelper = Container.Resolve<Microsoft.Toolkit.Uwp.Helpers.LocalObjectStorageHelper>();
                    _PrevWindowSize = localObjectStorageHelper.Read<Size>(HohoemaViewManager.primary_view_size);
                    localObjectStorageHelper.Save(HohoemaViewManager.primary_view_size, new Size(sender.VisibleBounds.Width, sender.VisibleBounds.Height));

                    Debug.WriteLine("MainView VisibleBoundsChanged : " + sender.VisibleBounds.ToString());
                }
            };
            view.Consolidated += (sender, e) => 
            {
                if (sender.Id == MainViewId)
                {
                    var localObjectStorageHelper = Container.Resolve<Microsoft.Toolkit.Uwp.Helpers.LocalObjectStorageHelper>();
                    if (_PrevWindowSize != default(Size))
                    {
                        localObjectStorageHelper.Save(HohoemaViewManager.primary_view_size, _PrevWindowSize);
                    }
                    MainViewId = -1;
                }
            };
        }


        #endregion


        #region Theme 


        const string ThemeTypeKey = "Theme";

        public static void SetTheme(ApplicationTheme theme)
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey(ThemeTypeKey))
            {
                ApplicationData.Current.LocalSettings.Values[ThemeTypeKey] = theme.ToString();
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values.Add(ThemeTypeKey, theme.ToString());
            }
        }

        public static ApplicationTheme GetTheme()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey(ThemeTypeKey))
                {
                    return (ApplicationTheme)Enum.Parse(typeof(ApplicationTheme), (string)ApplicationData.Current.LocalSettings.Values[ThemeTypeKey]);
                }
            }
            catch { }

            return ApplicationTheme.Dark;
        }

        #endregion


        #region Debug

        const string DEBUG_MODE_ENABLED_KEY = "Hohoema_DebugModeEnabled";
        public bool IsDebugModeEnabled
        {
            get
            {
                var enabled = ApplicationData.Current.LocalSettings.Values[DEBUG_MODE_ENABLED_KEY];
                if (enabled != null)
                {
                    return (bool)enabled;
                }
                else
                {
                    return false;
                }
            }

            set
            {
                ApplicationData.Current.LocalSettings.Values[DEBUG_MODE_ENABLED_KEY] = value;
            }
        }

        public async Task<string> GetMostRecentErrorText()
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("error", CreationCollisionOption.OpenIfExists);
            var errorFiles = await folder.GetItemsAsync();

            var errorFile = errorFiles
                .Where(x => x.Name.StartsWith("hohoema_error"))
                .OrderBy(x => x.DateCreated)
                .LastOrDefault()
                as StorageFile;

            if (errorFile != null)
            {
                return await FileIO.ReadTextAsync(errorFile);
            }
            else
            {
                return null;
            }
        }

        private async Task ShowErrorLog()
        {
            var text = await GetMostRecentErrorText();

            if (text != null)
            {
                var contentDialog = new ContentDialog();
                contentDialog.Title = "Hohoemaで発生したエラー詳細";
                contentDialog.PrimaryButtonText = "OK";
                contentDialog.Content = new TextBox()
                {
                    Text = text,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                };

                await contentDialog.ShowAsync().AsTask();
            }
        }


        private async void PrismUnityApplication_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Debug.Write(e.Message);

            if (IsDebugModeEnabled)
            {
                await WriteErrorFile(e.Exception);
            }

            //ShowErrorToast();
        }

        public async Task WriteErrorFile(Exception e, string pageName = null)
        {
            var pageManager = Container.Resolve<PageManager>();
            if (pageName == null)
            {
                pageName = pageManager.CurrentPageType.ToString();
            }
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("error", CreationCollisionOption.OpenIfExists);
                var errorFile = await folder.CreateFileAsync($"hohoema_{pageName}_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt", CreationCollisionOption.OpenIfExists);

                var version = Package.Current.Id.Version;
                var versionText = $"{version.Major}.{version.Minor}.{version.Build}";
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"Hohoema {versionText}");
                stringBuilder.AppendLine("開いていたページ:" + pageName);
                stringBuilder.AppendLine("");
                stringBuilder.AppendLine("= = = = = = = = = = = = = = = =");
                stringBuilder.AppendLine("");
                stringBuilder.AppendLine(e.ToString());

                await FileIO.WriteTextAsync(errorFile, stringBuilder.ToString());
            }
            catch { }
        }

        public void ShowErrorToast()
        {
            var toast = Container.Resolve<Services.NotificationService>();
            toast.ShowToast("Hohoema実行中に不明なエラーが発生しました"
                , "ここをタップすると再起動できます。"
                , Microsoft.Toolkit.Uwp.Notifications.ToastDuration.Long
                , luanchContent: ACTIVATION_WITH_ERROR
                );
        }


        private async void UINavigationManager_Pressed(Views.UINavigationManager sender, Views.UINavigationButtons buttons)
        {
            await HohoemaApp.UIDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (buttons == Views.UINavigationButtons.Up ||
                buttons == Views.UINavigationButtons.Down ||
                buttons == Views.UINavigationButtons.Right ||
                buttons == Views.UINavigationButtons.Left
                )
                {
                    var focused = FocusManager.GetFocusedElement();
                    Debug.WriteLine("現在のフォーカス:" + focused?.ToString());
                }
            });
        }




        #endregion

    }





}
