﻿using Mntone.Nico2;
using Mntone.Nico2.Videos.Comment;
using Mntone.Nico2.Videos.Flv;
using Mntone.Nico2.Videos.Thumbnail;
using Mntone.Nico2.Videos.WatchAPI;
using NicoPlayerHohoema.Models;
using NicoPlayerHohoema.Util;
using NicoPlayerHohoema.ViewModels.VideoInfoContent;
using NicoPlayerHohoema.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Windows.Mvvm;
using Prism.Windows.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace NicoPlayerHohoema.ViewModels
{
	public class VideoPlayerPageViewModel : HohoemaViewModelBase, IDisposable
	{

		public static SynchronizationContextScheduler PlayerWindowUIDispatcherScheduler;




		public VideoPlayerPageViewModel(HohoemaApp hohoemaApp, EventAggregator ea, PageManager pageManager)
			: base(pageManager)
		{
			if (PlayerWindowUIDispatcherScheduler == null)
			{
				PlayerWindowUIDispatcherScheduler = new SynchronizationContextScheduler(SynchronizationContext.Current);
			}

			_SidePaneContentCache = new Dictionary<MediaInfoDisplayType, MediaInfoViewModel>();



			ea.GetEvent<Events.PlayerClosedEvent>()
				.Subscribe(_ =>
				{
					
				});

			_HohoemaApp = hohoemaApp;


			VideoStream = new ReactiveProperty<IRandomAccessStream>(PlayerWindowUIDispatcherScheduler);
			CurrentVideoPosition = new ReactiveProperty<TimeSpan>(PlayerWindowUIDispatcherScheduler, TimeSpan.Zero);
			ReadVideoPosition = new ReactiveProperty<TimeSpan>(PlayerWindowUIDispatcherScheduler, TimeSpan.Zero);
			CommentData = new ReactiveProperty<CommentResponse>(PlayerWindowUIDispatcherScheduler);
			SliderVideoPosition = new ReactiveProperty<double>(PlayerWindowUIDispatcherScheduler, 0);
			VideoLength = new ReactiveProperty<double>(PlayerWindowUIDispatcherScheduler, 0);
			CurrentState = new ReactiveProperty<MediaElementState>(PlayerWindowUIDispatcherScheduler);
			Comments = new ObservableCollection<Comment>();
			NowCommentWriting = new ReactiveProperty<bool>(PlayerWindowUIDispatcherScheduler, false);
			NowSoundChanging = new ReactiveProperty<bool>(PlayerWindowUIDispatcherScheduler, false);
			IsVisibleComment = new ReactiveProperty<bool>(PlayerWindowUIDispatcherScheduler, true);
			IsEnableRepeat = new ReactiveProperty<bool>(PlayerWindowUIDispatcherScheduler, false);
			IsMuted = _HohoemaApp.UserSettings.PlayerSettings
				.ToReactivePropertyAsSynchronized(x => x.IsMute, PlayerWindowUIDispatcherScheduler);
			SoundVolume = _HohoemaApp.UserSettings.PlayerSettings
				.ToReactivePropertyAsSynchronized(x => x.SoundVolume, PlayerWindowUIDispatcherScheduler);

			Title = new ReactiveProperty<string>("");
			WritingComment = new ReactiveProperty<string>("");

			CommentSubmitCommand = WritingComment.Select(x => !string.IsNullOrWhiteSpace(x))
				.ToReactiveCommand();

			CommentSubmitCommand.Subscribe(x => 
			{
				if (SubmitComment(WritingComment.Value, Enumerable.Empty<CommandType>()))
				{
					WritingComment.Value = "";
				}
			});


			CurrentVideoQuality = new ReactiveProperty<NicoVideoQuality>(PlayerWindowUIDispatcherScheduler, NicoVideoQuality.Low, ReactivePropertyMode.DistinctUntilChanged);
			CanToggleCurrentQualityCacheState = CurrentVideoQuality
				.SubscribeOnUIDispatcher()
				.Select(x =>
				{
					if (this.Video == null) { return false; }

					switch (CurrentVideoQuality.Value)
					{
						case NicoVideoQuality.Original:
							return Video.OriginalQualityCacheState == NicoVideoCacheState.Incomplete ? Video.CanRequestDownloadOriginalQuality : false;
						case NicoVideoQuality.Low:
							return Video.LowQualityCacheState == NicoVideoCacheState.Incomplete ? Video.CanRequestDownloadLowQuality : false;
						default:
							return false;
					}
				})
				.ToReactiveProperty();

			IsSaveRequestedCurrentQualityCache = new ReactiveProperty<bool>(PlayerWindowUIDispatcherScheduler, false, ReactivePropertyMode.DistinctUntilChanged);

			CurrentVideoQuality
				.SubscribeOnUIDispatcher()
				.Subscribe(async x => 
			{
				if (Video == null) { IsSaveRequestedCurrentQualityCache.Value = false; return; }

				switch (x)
				{
					case NicoVideoQuality.Original:
						IsSaveRequestedCurrentQualityCache.Value = Video.OriginalQualityCacheState != NicoVideoCacheState.Incomplete;
						break;
					case NicoVideoQuality.Low:
						IsSaveRequestedCurrentQualityCache.Value = Video.LowQualityCacheState != NicoVideoCacheState.Incomplete;
						break;
					default:
						IsSaveRequestedCurrentQualityCache.Value = false;
						break;
				}

				VideoStream.Value = await Video.GetVideoStream(x);
			});

			IsSaveRequestedCurrentQualityCache
				.SubscribeOnUIDispatcher()
				.Subscribe(async saveRequested => 
			{
				if (saveRequested)
				{
					await Video.RequestCache(this.CurrentVideoQuality.Value);
				}
				else
				{
					Video.CancelCacheRequest(this.CurrentVideoQuality.Value);
				}

				CanToggleCurrentQualityCacheState.ForceNotify();
			});



			TogglePlayQualityCommand =
				Observable.Merge(
					this.ObserveProperty(x => x.Video).ToUnit(),
					CurrentVideoQuality.ToUnit()
					)
				.Select(_ =>
				{
					if (Video == null) { return false; }
					if (CurrentVideoQuality.Value == NicoVideoQuality.Original)
					{
						return Video.CanPlayLowQuality;
					}
					else
					{
						return Video.CanPlayOriginalQuality;
					}
				})
				.ToReactiveCommand();

			TogglePlayQualityCommand
				.SubscribeOnUIDispatcher()
				.Subscribe(_ => 
				{
					if (CurrentVideoQuality.Value == NicoVideoQuality.Low)
					{
						CurrentVideoQuality.Value = NicoVideoQuality.Original;
					}
					else
					{
						CurrentVideoQuality.Value = NicoVideoQuality.Low;
					}
				});


			ToggleQualityText = CurrentVideoQuality
				.Select(x => x == NicoVideoQuality.Low ? "低画質に切り替え" : "通常画質に切り替え")
				.ToReactiveProperty();



			Observable.Merge(
				IsMuted.ToUnit(),
				SoundVolume.ToUnit()
				)
				.Throttle(TimeSpan.FromSeconds(5))
				.Subscribe(_ => 
				{
					_HohoemaApp.UserSettings.PlayerSettings.Save().ConfigureAwait(false);
				});

			this.ObserveProperty(x => x.Video)
				.Subscribe(async x =>
				{
					if (x != null)
					{
						CommentData.Value = await GetComment();
						VideoLength.Value = x.CachedWatchApiResponse.Length.TotalSeconds;
						SliderVideoPosition.Value = 0;
					}
				});

		 
			CommentData.Subscribe(x => 
			{
				Comments.Clear();

				if (x == null)
				{
					return;
				}

				var list = x.Chat.Select(ChatToComment)
					.Where(y => y != null)
					.OrderBy(y => y.VideoPosition);

				foreach (var comment in list)
				{
					Comments.Add(comment);
				}

				UpdateCommentNGStatus();

				System.Diagnostics.Debug.WriteLine($"コメント数:{Comments.Count}");
			});


			SliderVideoPosition.Subscribe(x =>
			{
				_NowControlSlider = true;
				if (x > VideoLength.Value)
				{
					x = VideoLength.Value;
				}
				CurrentVideoPosition.Value = TimeSpan.FromSeconds(x);
				ReadVideoPosition.Value = CurrentVideoPosition.Value;
				_NowControlSlider = false;
			});

			ReadVideoPosition.Subscribe(x =>
			{
				if (_NowControlSlider) { return; }

				SliderVideoPosition.Value = x.TotalSeconds;
			});

			NowPlaying = CurrentState
				.Select(x =>
				{
					return
						x == MediaElementState.Opening ||
						x == MediaElementState.Buffering ||
						x == MediaElementState.Playing;
				})
				.ToReactiveProperty(PlayerWindowUIDispatcherScheduler);

			IsAutoHideEnable =
				Observable.CombineLatest(
					NowPlaying,
					NowSoundChanging.Select(x => !x),
					NowCommentWriting.Select(x => !x)
					)
					.Select(x => x.All(y => y))
					.ToReactiveProperty(PlayerWindowUIDispatcherScheduler);



			SelectedSidePaneType = new ReactiveProperty<MediaInfoDisplayType>(MediaInfoDisplayType.Summary, ReactivePropertyMode.DistinctUntilChanged);

			Types = new List<MediaInfoDisplayType>()
			{
				MediaInfoDisplayType.Summary,
				MediaInfoDisplayType.Comment,
				MediaInfoDisplayType.Settings,
			};

			SidePaneContent = SelectedSidePaneType
				.SelectMany(x => GetMediaInfoVM(x))
				.ToReactiveProperty();

			RequestFPS = _HohoemaApp.UserSettings.PlayerSettings.ObserveProperty(x => x.CommentRenderingFPS)
				.Select(x => (int)x)
				.ToReactiveProperty();
		}

		private Comment ChatToComment(Chat comment)
		{
			uint fontSize_mid = 26;
			uint fontSize_small = (uint)Math.Ceiling(fontSize_mid * 0.75f);
			uint fontSize_big = (uint)Math.Floor(fontSize_mid * 1.25f);

			uint default_fontSize = fontSize_mid;
			uint default_DisplayTime = 300; // 3秒

			Color defaultColor = ColorExtention.HexStringToColor("FFFFFF");


			if (comment.Text == null)
			{
				return null;
			}


			var decodedText = comment.GetDecodedText();

			var vpos = comment.GetVpos();

			var commentVM = new Comment(this)
			{
				CommentText = decodedText,
				CommentId = comment.GetCommentNo(),
				FontSize = default_fontSize,
				Color = defaultColor,
				VideoPosition = vpos,
				EndPosition = vpos + default_DisplayTime,
			};


			commentVM.IsOwnerComment = comment.User_id != null ? comment.User_id == Video.CachedThumbnailInfo.UserId.ToString() : false;

			var commandList = comment.GetCommandTypes();
			//					var commandList = Enumerable.Empty<CommandType>();
			bool isCommendCancel = false;
			foreach (var command in commandList)
			{
				switch (command)
				{
					case CommandType.small:
						commentVM.FontSize = fontSize_small;
						break;
					case CommandType.big:
						commentVM.FontSize = fontSize_big;
						break;
					case CommandType.medium:
						commentVM.FontSize = fontSize_mid;
						break;
					case CommandType.ue:
						commentVM.VAlign = VerticalAlignment.Top;
						break;
					case CommandType.shita:
						commentVM.VAlign = VerticalAlignment.Bottom;
						break;
					case CommandType.naka:
						commentVM.VAlign = VerticalAlignment.Center;
						break;
					case CommandType.white:
						commentVM.Color = ColorExtention.HexStringToColor("FFFFFF");
						break;
					case CommandType.red:
						commentVM.Color = ColorExtention.HexStringToColor("FF0000");
						break;
					case CommandType.pink:
						commentVM.Color = ColorExtention.HexStringToColor("FF8080");
						break;
					case CommandType.orange:
						commentVM.Color = ColorExtention.HexStringToColor("FFC000");
						break;
					case CommandType.yellow:
						commentVM.Color = ColorExtention.HexStringToColor("FFFF00");
						break;
					case CommandType.green:
						commentVM.Color = ColorExtention.HexStringToColor("00FF00");
						break;
					case CommandType.cyan:
						commentVM.Color = ColorExtention.HexStringToColor("00FFFF");
						break;
					case CommandType.blue:
						commentVM.Color = ColorExtention.HexStringToColor("0000FF");
						break;
					case CommandType.purple:
						commentVM.Color = ColorExtention.HexStringToColor("C000FF");
						break;
					case CommandType.black:
						commentVM.Color = ColorExtention.HexStringToColor("000000");
						break;
					case CommandType.white2:
						commentVM.Color = ColorExtention.HexStringToColor("CCCC99");
						break;
					case CommandType.niconicowhite:
						commentVM.Color = ColorExtention.HexStringToColor("CCCC99");
						break;
					case CommandType.red2:
						commentVM.Color = ColorExtention.HexStringToColor("CC0033");
						break;
					case CommandType.truered:
						commentVM.Color = ColorExtention.HexStringToColor("CC0033");
						break;
					case CommandType.pink2:
						commentVM.Color = ColorExtention.HexStringToColor("FF33CC");
						break;
					case CommandType.orange2:
						commentVM.Color = ColorExtention.HexStringToColor("FF6600");
						break;
					case CommandType.passionorange:
						commentVM.Color = ColorExtention.HexStringToColor("FF6600");
						break;
					case CommandType.yellow2:
						commentVM.Color = ColorExtention.HexStringToColor("999900");
						break;
					case CommandType.madyellow:
						commentVM.Color = ColorExtention.HexStringToColor("999900");
						break;
					case CommandType.green2:
						commentVM.Color = ColorExtention.HexStringToColor("00CC66");
						break;
					case CommandType.elementalgreen:
						commentVM.Color = ColorExtention.HexStringToColor("00CC66");
						break;
					case CommandType.cyan2:
						commentVM.Color = ColorExtention.HexStringToColor("00CCCC");
						break;
					case CommandType.blue2:
						commentVM.Color = ColorExtention.HexStringToColor("3399FF");
						break;
					case CommandType.marineblue:
						commentVM.Color = ColorExtention.HexStringToColor("3399FF");
						break;
					case CommandType.purple2:
						commentVM.Color = ColorExtention.HexStringToColor("6633CC");
						break;
					case CommandType.nobleviolet:
						commentVM.Color = ColorExtention.HexStringToColor("6633CC");
						break;
					case CommandType.black2:
						commentVM.Color = ColorExtention.HexStringToColor("666666");
						break;
					case CommandType.full:
						break;
					case CommandType._184:
						commentVM.IsAnonimity = true;
						break;
					case CommandType.invisible:
						commentVM.IsVisible = false;
						break;
					case CommandType.all:
						isCommendCancel = true;
						break;
					case CommandType.from_button:
						break;
					case CommandType.is_button:
						break;
					case CommandType._live:

						break;
					default:
						break;
				}

				// TODO: 投稿者のコメント表示時間を伸ばす？（3秒→５秒）
				// usまたはshitaが指定されている場合に限る？

				// 　→　投コメ解説をみやすくしたい

				if (commentVM.IsOwnerComment && commentVM.VAlign.HasValue)
				{
					var displayTime = commentVM.CommentText.Count() * 0.3f; // 4文字で1秒？ 年齢層的に読みが遅いかもしれないのでやや長めに
					var displayTimeVideoLength = (uint)(displayTime * 100);
					commentVM.EndPosition = commentVM.VideoPosition + displayTimeVideoLength;
				}


				if (isCommendCancel)
				{
					commentVM = new Comment(this)
					{
						CommentText = decodedText,
						FontSize = default_fontSize,
						Color = defaultColor,
						VideoPosition = vpos,
						EndPosition = vpos + default_DisplayTime,
					};
					break;
				}
			}

			return commentVM;
		}
		
		private void UpdateCommentNGStatus()
		{
			var ngSettings = _HohoemaApp.UserSettings.NGSettings;
			foreach (var comment in Comments)
			{
				if (comment.UserId != null)
				{
					var userNg = ngSettings.IsNGCommentUser(comment.UserId);
					if (userNg != null)
					{
						comment.NgResult = userNg;
						continue;
					}
				}

				var keywordNg = ngSettings.IsNGComment(comment.CommentText);
				if (keywordNg != null)
				{
					comment.NgResult = keywordNg;
					continue;
				}

				comment.NgResult = null;
			}
		}

		

		bool _NowControlSlider = false;
		


		public override async void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
			base.OnNavigatedTo(e, viewModelState);

			NicoVideoQuality quality = NicoVideoQuality.Low;
			if (e?.Parameter is string)
			{
				var payload = VideoPlayPayload.FromParameterString(e.Parameter as string);
				VideoId = payload.VideoId;
				quality =  payload.Quality;
			}
			else if(viewModelState.ContainsKey(nameof(VideoId)))
			{
				VideoId = (string)viewModelState[nameof(VideoId)];
			}



			var currentUIDispatcher = Window.Current.Dispatcher;

			try
			{
				var videoInfo = await _HohoemaApp.MediaManager.GetNicoVideo(VideoId);

				// 内部状態を更新
				await videoInfo.GetVideoInfo();
				await videoInfo.CheckCacheStatus();

				Video = videoInfo;
			}
			catch (Exception exception)
			{
				// 動画情報の取得に失敗
				System.Diagnostics.Debug.Write(exception.Message);
				
			}




			// ビデオクオリティをトリガーにしてビデオ関連の情報を更新させる
			// CurrentVideoQualityは代入時に常にNotifyが発行される設定になっている
			
			if (Video.NowLowQualityOnly && Video.OriginalQualityCacheState != NicoVideoCacheState.Cached)
			{
				CurrentVideoQuality.Value = NicoVideoQuality.Low;
				CurrentVideoQuality.ForceNotify();
			}
			else
			{

				if (quality == CurrentVideoQuality.Value)
				{
					CurrentVideoQuality.ForceNotify();
				}
				else
				{
					CurrentVideoQuality.Value = quality;
				}
			}



			if (viewModelState.ContainsKey(nameof(CurrentVideoPosition)))
			{
				CurrentVideoPosition.Value = TimeSpan.FromSeconds((double)viewModelState[nameof(CurrentVideoPosition)]);
			}

			Title.Value = Video.Title;
			_SidePaneContentCache.Clear();

			if (SelectedSidePaneType.Value == MediaInfoDisplayType.Summary)
			{
				SelectedSidePaneType.ForceNotify();
			}
			else
			{
				SelectedSidePaneType.Value = MediaInfoDisplayType.Summary;
			}

			// PlayerSettings
			var playerSettings = _HohoemaApp.UserSettings.PlayerSettings;
			IsVisibleComment.Value = playerSettings.DefaultCommentDisplay;
			



		}

		private async Task<CommentResponse> GetComment()
		{
			if (Video == null) { return null; }

			return await Video.GetComment(true);
//			return await this._HohoemaApp.NiconicoContext.Video
//					.GetCommentAsync(response);
		}

		public override void OnNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewModelState, bool suspending)
		{
			if (VideoStream.Value != null)
			{
				Video.StopPlay();

				var stream = VideoStream.Value;
				VideoStream.Value = null;

				// Note: VideoStopPlayによってストリームの管理が行われます
				// これは再生後もダウンロードしている場合に対応するためです
				// stream.Dispose();
			}

			Comments.Clear();

			if (suspending)
			{
				viewModelState.Add(nameof(VideoId), VideoId);
				viewModelState.Add(nameof(CurrentVideoPosition), CurrentVideoPosition.Value.TotalSeconds);
			}

			_SidePaneContentCache.Clear();

			base.OnNavigatingFrom(e, viewModelState, suspending);
		}

		public void Dispose()
		{
			Video?.StopPlay();
		}

		public override string GetPageTitle()
		{
			return Title.Value;
		}


		private bool SubmitComment(string comment, IEnumerable<CommandType> commands)
		{
			Debug.WriteLine($"comment submit:{comment}");


			return true;
		}



		private async Task<MediaInfoViewModel> GetMediaInfoVM(MediaInfoDisplayType type)
		{
			MediaInfoViewModel vm = null;
			if (_SidePaneContentCache.ContainsKey(type))
			{
				vm = _SidePaneContentCache[type];
			}
			else 
			{
				switch (type)
				{
					case MediaInfoDisplayType.Summary:
						var uri = await VideoDescriptionHelper.PartHtmlOutputToCompletlyHtml(VideoId, Video.CachedWatchApiResponse.videoDetail.description);

						vm = new SummaryVideoInfoContentViewModel(Video.CachedThumbnailInfo, uri, PageManager);
						break;

					case MediaInfoDisplayType.Comment:
						vm = new CommentVideoInfoContentViewModel(_HohoemaApp.UserSettings, Comments);
						break;

					case MediaInfoDisplayType.Settings:
						vm = new SettingsVideoInfoContentViewModel(_HohoemaApp.UserSettings.PlayerSettings);
						break;
					default:
						throw new NotSupportedException();
				}

				_SidePaneContentCache.Add(type, vm);
			}

			return vm;
		}

		#region Command	

		private DelegateCommand<object> _CurrentStateChangedCommand;
		public DelegateCommand<object> CurrentStateChangedCommand
		{
			get
			{
				return _CurrentStateChangedCommand
					?? (_CurrentStateChangedCommand = new DelegateCommand<object>((arg) =>
					{
						var e = (RoutedEventArgs)arg;
						
					}
					));
			}
		}


		private DelegateCommand _ToggleMuteCommand;
		public DelegateCommand ToggleMuteCommand
		{
			get
			{
				return _ToggleMuteCommand
					?? (_ToggleMuteCommand = new DelegateCommand(() => 
					{
						IsMuted.Value = !IsMuted.Value;
					}));
			}
		}


		private DelegateCommand _ToggleRepeatCommand;
		public DelegateCommand ToggleRepeatCommand
		{
			get
			{
				return _ToggleRepeatCommand
					?? (_ToggleRepeatCommand = new DelegateCommand(() =>
					{
						IsEnableRepeat.Value = !IsEnableRepeat.Value;
					}));
			}
		}

		public ReactiveCommand CommentSubmitCommand { get; private set; }
		public ReactiveCommand TogglePlayQualityCommand { get; private set; }
		

		#endregion


		public ReactiveProperty<CommentResponse> CommentData { get; private set; }

		private NicoVideo _Video;
		public NicoVideo Video
		{
			get { return _Video; }
			set { SetProperty(ref _Video, value); }
		}

		private string _VideoId;
		public string VideoId
		{
			get { return _VideoId; }
			set { SetProperty(ref _VideoId, value); }
		}
		
		public ReactiveProperty<IRandomAccessStream> VideoStream { get; private set; }

		public ReactiveProperty<NicoVideoQuality> CurrentVideoQuality { get; private set; }
		public ReactiveProperty<bool> CanToggleCurrentQualityCacheState { get; private set; }
		public ReactiveProperty<bool> IsSaveRequestedCurrentQualityCache { get; private set; }

		public ReactiveProperty<string> ToggleQualityText { get; private set; }

		public ReactiveProperty<TimeSpan> CurrentVideoPosition { get; private set; }
		public ReactiveProperty<TimeSpan> ReadVideoPosition { get; private set; }

		public ObservableCollection<Comment> Comments { get; private set; }

		public ReactiveProperty<double> SliderVideoPosition { get; private set; }

		public ReactiveProperty<double> VideoLength { get; private set; }

		public ReactiveProperty<MediaElementState> CurrentState { get; private set; }

		public ReactiveProperty<bool> NowPlaying { get; private set; }

		public ReactiveProperty<bool> NowCommentWriting { get; private set; }

		public ReactiveProperty<bool> NowSoundChanging { get; private set; }

		public ReactiveProperty<bool> IsAutoHideEnable { get; private set; }


		public ReactiveProperty<bool> IsVisibleComment { get; private set; }

		public ReactiveProperty<bool> IsEnableRepeat { get; private set; }
		
		public ReactiveProperty<string> WritingComment { get; private set; }

		public ReactiveProperty<bool> IsMuted { get; private set; }

		public ReactiveProperty<float> SoundVolume { get; private set; }

		public ReactiveProperty<string> Title { get; private set; }

		public ReactiveProperty<int> RequestFPS { get; private set; }

		public ReactiveProperty<MediaInfoViewModel> SidePaneContent { get; private set; }
		private Dictionary<MediaInfoDisplayType, MediaInfoViewModel> _SidePaneContentCache;
		public ReactiveProperty<MediaInfoDisplayType> SelectedSidePaneType { get; private set; }
		public List<MediaInfoDisplayType> Types { get; private set; }

		// Note: 新しいReactivePropertyを追加したときの注意点
		// RPではPlayerWindowUIDispatcherSchedulerを使うこと



		private HohoemaApp _HohoemaApp;




		internal async Task AddNgUser(Comment commentViewModel)
		{
			if (commentViewModel.UserId == null) { return; }

			string userName = "";
			try
			{
//				var commentUser = await _HohoemaApp.ContentFinder.GetUserInfo(commentViewModel.UserId);
//				userName = commentUser.Nickname;
			}
			catch { }

		    _HohoemaApp.UserSettings.NGSettings.NGCommentUserIds.Add(new UserIdInfo()
			{
				UserId = commentViewModel.UserId,
				Description = userName
			});

			UpdateCommentNGStatus();
			
		}
	}


	


	

	public class TagViewModel
	{
		public TagViewModel(Tag tag)
		{
			_Tag = tag;

			TagText = _Tag.Value;
			IsCategoryTag = _Tag.Category;
			IsLock = _Tag.Lock;
		}

		public string TagText { get; private set; }
		public bool IsCategoryTag { get; private set; }
		public bool IsLock { get; private set; }


		private DelegateCommand _OpenSearchPageWithTagCommand;
		public DelegateCommand OpenSearchPageWithTagCommand
		{
			get
			{
				return _OpenSearchPageWithTagCommand
					?? (_OpenSearchPageWithTagCommand = new DelegateCommand(() => 
					{
						// TODO: 
					}));
			}
		}


		private DelegateCommand _OpenTagDictionaryInBrowserCommand;
		public DelegateCommand OpenTagDictionaryInBrowserCommand
		{
			get
			{
				return _OpenTagDictionaryInBrowserCommand
					?? (_OpenTagDictionaryInBrowserCommand = new DelegateCommand(() =>
					{
						// TODO: 
					}));
			}
		}

		Tag _Tag;
	}


	public enum MediaInfoDisplayType
	{
		Summary,
		Comment,
		Settings,
	}










}
