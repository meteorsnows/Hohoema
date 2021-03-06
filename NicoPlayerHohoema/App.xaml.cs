﻿using Prism.Mvvm;
using System;
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
using Windows.UI.Xaml.Input;
using Prism.Unity.Windows;
using Unity;
using NicoPlayerHohoema.Models;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Prism.Windows.AppModel;
using Windows.Storage;
using Windows.UI;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.ApplicationModel.Background;
using System.Reactive.Concurrency;
using System.Threading;
using NicoPlayerHohoema.Services;
using NicoPlayerHohoema.Services.Page;
using Prism.Windows.Navigation;
using Reactive.Bindings.Extensions;
using System.Reactive.Linq;
using Unity.Lifetime;
using Unity.Injection;

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
        public const string ACTIVATION_WITH_ERROR_OPEN_LOG = "error_open_log";
        public const string ACTIVATION_WITH_ERROR_COPY_LOG = "error_copy_log";

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

        private async Task RegisterTypes()
        {
            Container.RegisterType<IScheduler>(new InjectionFactory(c => SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : null));

            // Service
            Container.RegisterType<Services.NiconicoLoginService>(new ContainerControlledLifetimeManager());
            Container.RegisterType<Services.DialogService>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Services.PageManager>(new PerThreadLifetimeManager());
            Container.RegisterType<PlayerViewManager>(new PerThreadLifetimeManager());

            // Models
            Container.RegisterType<Models.NiconicoSession>(lifetimeManager: new ContainerControlledLifetimeManager());
            
            var hohoemaSettings = await Models.HohoemaUserSettings.LoadSettings(ApplicationData.Current.LocalFolder);
            Container.RegisterInstance(hohoemaSettings.PlayerSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.ActivityFeedSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.AppearanceSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.CacheSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.NGSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.PinSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.PlaylistSettings, lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterInstance(hohoemaSettings.RankingSettings, lifetimeManager: new ContainerControlledLifetimeManager());

            
            Container.RegisterType<Models.LocalMylist.LocalMylistManager>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Models.OtherOwneredMylistManager>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Models.UserMylistManager>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Models.FollowManager>(lifetimeManager: new ContainerControlledLifetimeManager());

            Container.RegisterType<Services.HohoemaPlaylist>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Models.Cache.VideoCacheManager>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Models.Subscription.SubscriptionManager>(lifetimeManager: new ContainerControlledLifetimeManager());
            
            // Commands 
            Container.RegisterType<Commands.Mylist.CreateMylistCommand>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Commands.Mylist.CreateLocalMylistCommand>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Commands.Subscriptions.CreateSubscriptionGroupCommand>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Commands.AddToHiddenUserCommand>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Commands.Cache.AddCacheRequestCommand>(lifetimeManager: new ContainerControlledLifetimeManager());
            Container.RegisterType<Commands.Cache.DeleteCacheRequestCommand>(lifetimeManager: new ContainerControlledLifetimeManager());

            // Note: インスタンス化が別途必要
            Container.RegisterInstance(Container.Resolve<Services.WatchItLater>());
            Container.RegisterInstance(Container.Resolve<Services.Notification.NotificationCacheVideoDeletedService>());
            Container.RegisterInstance(Container.Resolve<Services.Notification.NotificationMylistUpdatedService>());
            Container.RegisterInstance(Container.Resolve<Services.Page.PreventBackNavigationOnShowPlayerService>());
            Container.RegisterInstance(Container.Resolve<Services.Notification.CheckingClipboardAndNotificationService>());
            Container.RegisterInstance(Container.Resolve<Services.Notification.NotificationFollowUpdatedService>());

            // ViewModels
            Container.RegisterType<ViewModels.RankingCategoryListPageViewModel>(new ContainerControlledLifetimeManager());

           

#if DEBUG
            //			BackgroundUpdater.MaxTaskSlotCount = 1;
#endif
            // TODO: プレイヤーウィンドウ上で管理する
            //			var backgroundTask = MediaBackgroundTask.Create();
            //			Container.RegisterInstance(backgroundTask);


        }

        public bool IsTitleBarCustomized { get; } = Services.Helpers.DeviceTypeHelper.IsDesktop && Services.Helpers.InputCapabilityHelper.IsMouseCapable;

        /// <summary>
        /// アプリ動作に必要な機能を初期化する
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override async Task OnInitializeAsync(IActivatedEventArgs args)
        {
            await HohoemaInitializeAsync(args);

            await base.OnInitializeAsync(args);
        }


        Models.Helpers.AsyncLock InitializeLock = new Models.Helpers.AsyncLock();
        bool isInitialized = false;
        private async Task HohoemaInitializeAsync(IActivatedEventArgs args)
        {
            using (await InitializeLock.LockAsync())
            {
                if (isInitialized) { return; }
                isInitialized = true;

                await RegisterTypes();

                // ログイン
                try
                {
                    var niconicoSession = Container.Resolve<NiconicoSession>();
                    if (Models.Helpers.AccountManager.HasPrimaryAccount())
                    {
                        // サインイン処理の待ちを初期化内でしないことで初期画面表示を早める
                        _ = niconicoSession.SignInWithPrimaryAccount();
                    }
                }
                catch
                {
                    Debug.WriteLine("ログイン処理に失敗");
                }


                Resources["IsXbox"] = Services.Helpers.DeviceTypeHelper.IsXbox;
                Resources["IsMobile"] = Services.Helpers.DeviceTypeHelper.IsMobile;
                var cacheSettings = Container.Resolve<CacheSettings>();
                Resources["IsCacheEnabled"] = cacheSettings.IsEnableCache;
                var appearanceSettings = Container.Resolve<AppearanceSettings>();
                Resources["IsTVModeEnabled"] = Services.Helpers.DeviceTypeHelper.IsXbox || appearanceSettings.IsForceTVModeEnable;


                try
                {
#if DEBUG
                    if (_DEBUG_XBOX_RESOURCE)
#else
                if (Services.Helpers.DeviceTypeHelper.IsXbox)
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
                }
                catch
                {

                }



#if DEBUG
                Resources["IsDebug"] = true;
#else
            Resources["IsDebug"] = false;
#endif
                Resources["TitleBarCustomized"] = IsTitleBarCustomized;
                Resources["TitleBarDummyHeight"] = IsTitleBarCustomized ? 32.0 : 0.0;


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
                if (Services.Helpers.DeviceTypeHelper.IsDesktop)
                {
                    var localObjectStorageHelper = Container.Resolve<Microsoft.Toolkit.Uwp.Helpers.LocalObjectStorageHelper>();
                    if (localObjectStorageHelper.KeyExists(PlayerViewManager.primary_view_size))
                    {
                        var view = ApplicationView.GetForCurrentView();
                        MainViewId = view.Id;
                        _PrevWindowSize = localObjectStorageHelper.Read<Size>(PlayerViewManager.primary_view_size);
                        view.TryResizeView(_PrevWindowSize);
                        ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
                    }
                }

                // XboxOneで外枠表示を行わないように設定
                if (Services.Helpers.DeviceTypeHelper.IsXbox)
                {
                    Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode
                        (Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
                }

                // モバイルでナビゲーションバーをアプリに被せないように設定
                if (Services.Helpers.DeviceTypeHelper.IsMobile)
                {
                    // モバイルで利用している場合に、ナビゲーションバーなどがページに被さらないように指定
                    ApplicationView.GetForCurrentView().SuppressSystemOverlays = true;
                    ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
                }








                // 更新通知を表示
                try
                {
                    var dialogService = Container.Resolve<Services.DialogService>();
                    if (Models.Helpers.AppUpdateNotice.HasNotCheckedUptedeNoticeVersion)
                    {
                        _ = dialogService.ShowLatestUpdateNotice();
                        Models.Helpers.AppUpdateNotice.UpdateLastCheckedVersionInCurrentVersion();
                    }
                }
                catch { }

                // 購読機能を初期化
                try
                {
                    var watchItLater = Container.Resolve<Services.WatchItLater>();
                    watchItLater.Initialize();
                }
                catch (Exception e)
                {
                    Debug.WriteLine("購読機能の初期化に失敗");
                    Debug.WriteLine(e.ToString());
                }




                // バックグラウンドでのトースト通知ハンドリングを初期化
                await RegisterDebugToastNotificationBackgroundHandling();


                try
                {
                    var cacheManager = Container.Resolve<Models.Cache.VideoCacheManager>();
                    _ = cacheManager.Initialize();
                }
                catch { }


                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated
                    || args.PreviousExecutionState == ApplicationExecutionState.ClosedByUser)
                {
                    if (!Services.Helpers.ApiContractHelper.Is2018FallUpdateAvailable)
                    {
                        var pageManager = Container.Resolve<Services.PageManager>();
                        pageManager.OpenStartupPage();
                    }
                }
                else
                {
                    var pageManager = Container.Resolve<Services.PageManager>();


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
                }

            }
        }


        protected override async Task OnLaunchApplicationAsync(LaunchActivatedEventArgs args)
        {
            SplashScreen = args.SplashScreen;
#if DEBUG
            DebugSettings.IsBindingTracingEnabled = true;
#endif
            _IsPreLaunch = args.PrelaunchActivated;

            if (args.Kind == ActivationKind.Launch)
            {
                await HohoemaInitializeAsync(args);
            }
            


            await Task.CompletedTask;
        }

        protected override async Task OnActivateApplicationAsync(IActivatedEventArgs args)
		{
            await HohoemaInitializeAsync(args);

            var niconicoSession = Container.Resolve<NiconicoSession>();

            // 外部から起動した場合にサインイン動作と排他的動作にさせたい
            // こうしないと再生処理を正常に開始できない
            using (await niconicoSession.SigninLock.LockAsync())
            {
                await Task.Delay(50);
            }



            if (args.Kind == ActivationKind.ToastNotification)
            {
                bool isHandled = false;

                //Get the pre-defined arguments and user inputs from the eventargs;
                var toastArgs = args as IActivatedEventArgs as ToastNotificationActivatedEventArgs;
                var arguments = toastArgs.Argument;
                try
                {
                    var serialize = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginRedirectPayload>(arguments);
                    if (serialize != null)
                    {
                        if (serialize.RedirectPageType == HohoemaPageType.VideoPlayer)
                        {
                            PlayVideoFromExternal(serialize.RedirectParamter);
                        }
                        else
                        {
                            var pageManager = Container.Resolve<Services.PageManager>();
                            pageManager.OpenPage(serialize.RedirectPageType, serialize.RedirectParamter);
                        }
                        isHandled = true;
                    }
                }
                catch { }
                
                if (!isHandled)
                {
                    if (arguments == ACTIVATION_WITH_ERROR)
                    {
                        await ShowErrorLog().ConfigureAwait(false);
                    }
                    else if (arguments == ACTIVATION_WITH_ERROR_COPY_LOG)
                    {
                        var error = await GetMostRecentErrorText();

                        Services.Helpers.ClipboardHelper.CopyToClipboard(error);
                    }
                    else if (arguments == ACTIVATION_WITH_ERROR_OPEN_LOG)
                    {
                        await ShowErrorLogFolder();
                    }
                    else if (arguments.StartsWith("cache_cancel"))
                    {
                        var query = arguments.Split('?')[1];
                        var decode = new WwwFormUrlDecoder(query);

                        var videoId = decode.GetFirstValueByName("id");
                        var quality = (NicoVideoQuality)Enum.Parse(typeof(NicoVideoQuality), decode.GetFirstValueByName("quality"));

                        var cacheManager = Container.Resolve<Models.Cache.VideoCacheManager>();
                        await cacheManager.CancelCacheRequest(videoId, quality);
                    }
                    else
                    {
                        var nicoContentId = Models.Helpers.NicoVideoIdHelper.UrlToVideoId(arguments);

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

            await base.OnActivateApplicationAsync(args);
		}



       

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            var deferral = args.TaskInstance.GetDeferral();

            switch (args.TaskInstance.Task.Name)
            {
                case "ToastBackgroundTask":
                    var details = args.TaskInstance.TriggerDetails as Windows.UI.Notifications.ToastNotificationActionTriggerDetail;
                    if (details != null)
                    {
                        string arguments = details.Argument;
                        var userInput = details.UserInput;

                        await ProcessToastNotificationActivation(arguments, userInput);
                    }
                    break;
            }

            deferral.Complete();

                
            base.OnBackgroundActivated(args);
        }



        private void PlayVideoFromExternal(string videoId, string videoTitle = null, NicoVideoQuality? quality = null)
        {
            var playlist = Container.Resolve<HohoemaPlaylist>();

            // TODO: ログインが必要な動画かをチェックしてログインダイアログを出す

            playlist.PlayVideo(videoId, videoTitle, quality);
        }
        private void PlayLiveVideoFromExternal(string videoId)
        {
            // TODO: ログインが必要な生放送かをチェックしてログインダイアログを出す

            var playlist = Container.Resolve<HohoemaPlaylist>();

            playlist.PlayLiveVideo(videoId);
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
            var appearanceSettings = Container.Resolve<AppearanceSettings>();
            var isForceTVModeEnable = appearanceSettings.IsForceTVModeEnable;
            var isForceMobileModeEnable = appearanceSettings.IsForceMobileModeEnable;

            Type viewType = null;
            if (isForceTVModeEnable || Services.Helpers.DeviceTypeHelper.IsXbox)
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
            else if (isForceMobileModeEnable || Services.Helpers.DeviceTypeHelper.IsMobile)
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

            rootFrame.NavigationFailed += (_, e) => 
            {
                Debug.WriteLine("Page navigation failed!!");
                Debug.WriteLine(e.SourcePageType.AssemblyQualifiedName);
                Debug.WriteLine(e.Exception.ToString());

                _ = OutputErrorFile(e.Exception, e.SourcePageType?.AssemblyQualifiedName);
            };


            // Grid
            //   |- HohoemaInAppNotification
            //   |- PlayerWithPageContainerViewModel
            //   |    |- MenuNavigatePageBaseViewModel
            //   |         |- rootFrame 

            var menuPageBase = new Views.MenuNavigatePageBase()
            {
                Content = rootFrame,
                DataContext = Container.Resolve<ViewModels.MenuNavigatePageBaseViewModel>()
            };


            Views.PlayerWithPageContainer playerWithPageContainer = new Views.PlayerWithPageContainer()
            {
                Content = menuPageBase,
                DataContext = Container.Resolve<ViewModels.PlayerWithPageContainerViewModel>()
            };

            playerWithPageContainer.ObserveDependencyProperty(Views.PlayerWithPageContainer.FrameProperty)
                .Where(x => x != null)
                .Take(1)
                .Subscribe(async frame =>
                {
                    var frameFacade = new FrameFacadeAdapter(playerWithPageContainer.Frame, EventAggregator);
                    
                    var sessionStateService = new SessionStateService();
                    var ns = new FrameNavigationService(frameFacade
                        , (pageToken) =>
                        {
                            if (pageToken == nameof(Views.VideoPlayerPage))
                            {
                                return typeof(Views.VideoPlayerPage);
                            }
                            else if (pageToken == nameof(Views.LivePlayerPage))
                            {
                                return typeof(Views.LivePlayerPage);
                            }
                            else
                            {
                                return typeof(Views.BlankPage);
                            }
                        }, sessionStateService);

                    var name = nameof(PlayerViewManager.PrimaryViewPlayerNavigationService);
                    Container.RegisterInstance(name, ns as INavigationService);
                });

            


            var hohoemaInAppNotification = new Views.HohoemaInAppNotification()
            {
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var grid = new Grid()
            {
                Children =
                {
                    playerWithPageContainer,
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
                    _PrevWindowSize = localObjectStorageHelper.Read<Size>(PlayerViewManager.primary_view_size);
                    localObjectStorageHelper.Save(PlayerViewManager.primary_view_size, new Size(sender.VisibleBounds.Width, sender.VisibleBounds.Height));

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
                        localObjectStorageHelper.Save(PlayerViewManager.primary_view_size, _PrevWindowSize);
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

        public async Task ShowErrorLogFolder()
        {
            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("error");
            if (folder != null)
            {
                await Windows.System.Launcher.LaunchFolderAsync(folder);
            }
        }

        private async void PrismUnityApplication_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Debug.Write(e.Message);

            if (IsDebugModeEnabled)
            {
                await OutputErrorFile(e.Exception);

                ShowErrorToast(e.Message);
            }
        }


        struct ErrorReport
        {
            public string DeviceType { get; set; }
            public string OperatingSystem { get; set; }
            public string OperatingSystemVersion { get; set; }
            public string OperatingSystemArchitecture { get; set; }

            public string ApplicationVersion { get; set; }
            public DateTime Time { get; set; }
            public string RecentOpenedPageName { get; set; }
            public string ErrorMessage { get; set; }

            public bool IsInternetAvailable { get; set; }
            public bool IsLoggedIn { get; set; }
            public bool IsPremiumAccount { get; set; }

        }

        public async Task OutputErrorFile(Exception e, string pageName = null)
        {
            var pageManager = Container.Resolve<Services.PageManager>();
            if (pageName == null)
            {
                pageName = pageManager.CurrentPageType.ToString();
            }

            var niconicoSession = Container.Resolve<NiconicoSession>();


            try
            {
                var v = Package.Current.Id.Version;
                var versionText = $"{v.Major}.{v.Minor}.{v.Revision}.{v.Build}";
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("error", CreationCollisionOption.OpenIfExists);
                var errorFile = await folder.CreateFileAsync($"Hohoema-{versionText.Replace('.', '_')}-{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.txt", CreationCollisionOption.OpenIfExists);

                var errorReport = new ErrorReport()
                {
                    ApplicationVersion = versionText,
                    Time = DateTime.Now,
                    RecentOpenedPageName = pageName,
                    IsInternetAvailable = Models.Helpers.InternetConnection.IsInternet(),
                    IsLoggedIn = niconicoSession.IsLoggedIn,
                    IsPremiumAccount = niconicoSession.IsPremiumAccount,
                    ErrorMessage = e.ToString(),
                    DeviceType = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.DeviceFamily,
                    OperatingSystem = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.OperatingSystem,
                    OperatingSystemArchitecture = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.OperatingSystemArchitecture.ToString(),
                    OperatingSystemVersion = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.OperatingSystemVersion.ToString(),
                };

                var errorReportJsonText = Newtonsoft.Json.JsonConvert.SerializeObject(
                    errorReport, 
                    Newtonsoft.Json.Formatting.Indented, 
                    new Newtonsoft.Json.JsonSerializerSettings()
                    {
                        TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
                    });

                await FileIO.WriteTextAsync(errorFile, errorReportJsonText);
            }
            catch { }
        }

        public void ShowErrorToast(string message)
        {
            var toast = Container.Resolve<Services.NotificationService>();
            toast.ShowToast("Hohoemaに問題が発生しました"
                , message
                , Microsoft.Toolkit.Uwp.Notifications.ToastDuration.Long
                , luanchContent: ACTIVATION_WITH_ERROR
                ,  toastButtons: new[] 
                {
                    new ToastButton("エラーログをコピー", ACTIVATION_WITH_ERROR_COPY_LOG) { ActivationType = ToastActivationType.Background },
                    new ToastButton("ログフォルダを開く", ACTIVATION_WITH_ERROR_OPEN_LOG) { ActivationType = ToastActivationType.Background },
                }
                );
        }



        private async Task RegisterDebugToastNotificationBackgroundHandling()
        {
            try
            {
                const string taskName = "ToastBackgroundTask";

                // If background task is already registered, do nothing
                if (BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name.Equals(taskName)))
                    return;

                // Otherwise request access
                BackgroundAccessStatus status = await BackgroundExecutionManager.RequestAccessAsync();

                // Create the background task
                BackgroundTaskBuilder builder = new BackgroundTaskBuilder()
                {
                    Name = taskName
                };

                // Assign the toast action trigger
                builder.SetTrigger(new ToastNotificationActionTrigger());

                // And register the task
                BackgroundTaskRegistration registration = builder.Register();
            }
            catch { }
        }

        private async Task ProcessToastNotificationActivation(string arguments, ValueSet userInput)
        {
            
            // Perform tasks
            if (arguments == ACTIVATION_WITH_ERROR)
            {
                await ShowErrorLog().ConfigureAwait(false);
            }
            else if (arguments == ACTIVATION_WITH_ERROR_COPY_LOG)
            {
                var error = await GetMostRecentErrorText();
                Services.Helpers.ClipboardHelper.CopyToClipboard(error);
            }
            else if (arguments == ACTIVATION_WITH_ERROR_OPEN_LOG)
            {
                await ShowErrorLogFolder();
            }
            else if (arguments.StartsWith("cache_cancel"))
            {
                var cacheManager = Container.Resolve<Models.Cache.VideoCacheManager>();

                var query = arguments.Split('?')[1];
                var decode = new WwwFormUrlDecoder(query);

                var videoId = decode.GetFirstValueByName("id");
                var quality = (NicoVideoQuality)Enum.Parse(typeof(NicoVideoQuality), decode.GetFirstValueByName("quality"));

                await cacheManager.CancelCacheRequest(videoId, quality);
            }
        }

        /*
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
        */



#endregion

    }





}
