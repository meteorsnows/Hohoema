﻿using NicoPlayerHohoema.Models;
using NicoPlayerHohoema.Models.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using Mntone.Nico2.Searches.Mylist;
using Prism.Commands;
using Mntone.Nico2;
using Prism.Windows.Navigation;
using System.Collections.Async;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using NicoPlayerHohoema.Services.Page;
using NicoPlayerHohoema.Models.Provider;
using NicoPlayerHohoema.Services;

namespace NicoPlayerHohoema.ViewModels
{
    public class SearchResultMylistPageViewModel : HohoemaListingPageViewModelBase<Interfaces.IMylist>
	{
        public SearchResultMylistPageViewModel(
            SearchProvider searchProvider,
            Services.PageManager pageManager
            )
            : base(pageManager, useDefaultPageTitle: false)
        {
            SelectedSearchSort = new ReactivePropertySlim<SearchSortOptionListItem>();
            SelectedSearchTarget = new ReactiveProperty<SearchTarget>();
            SearchProvider = searchProvider;
        }


        public MylistSearchPagePayloadContent SearchOption { get; private set; }

        public static IReadOnlyList<SearchSortOptionListItem> MylistSearchOptionListItems { get; private set; }

        static SearchResultMylistPageViewModel()
        {
            MylistSearchOptionListItems = new List<SearchSortOptionListItem>()
            {
                new SearchSortOptionListItem()
                {
                    Label = "人気が高い順",
                    Sort = Sort.MylistPopurarity,
                    Order = Order.Descending,
                }
				//, new SearchSortOptionListItem()
				//{
				//	Label = "人気が低い順",
				//	Sort = Sort.MylistPopurarity,
				//	Order = Order.Ascending,
				//}
				, new SearchSortOptionListItem()
                {
                    Label = "更新が新しい順",
                    Sort = Sort.UpdateTime,
                    Order = Order.Descending,
                }
				//, new SearchSortOptionListItem()
				//{
				//	Label = "更新が古い順",
				//	Sort = Sort.UpdateTime,
				//	Order = Order.Ascending,
				//}
				, new SearchSortOptionListItem()
                {
                    Label = "動画数が多い順",
                    Sort = Sort.VideoCount,
                    Order = Order.Descending,
                }
                //, new SearchSortOptionListItem()
                //{
                //	Label = "動画数が少ない順",
                //	Sort = Sort.VideoCount,
                //	Order = Order.Ascending,
                //}
                , new SearchSortOptionListItem()
                {
                    Label = "適合率が高い順",
                    Sort = Sort.Relation,
                    Order = Order.Descending,
                }
				//, new SearchSortOptionListItem()
				//{
				//	Label = "適合率が低い順",
				//	Sort = Sort.Relation,
				//	Order = Order.Ascending,
				//}

			};
        }

        public ReactivePropertySlim<SearchSortOptionListItem> SelectedSearchSort { get; private set; }



        private string _SearchOptionText;
        public string SearchOptionText
        {
            get { return _SearchOptionText; }
            set { SetProperty(ref _SearchOptionText, value); }
        }


        public static List<SearchTarget> SearchTargets { get; } = Enum.GetValues(typeof(SearchTarget)).Cast<SearchTarget>().ToList();

        public ReactiveProperty<SearchTarget> SelectedSearchTarget { get; }

        private DelegateCommand<SearchTarget?> _ChangeSearchTargetCommand;
        public DelegateCommand<SearchTarget?> ChangeSearchTargetCommand
        {
            get
            {
                return _ChangeSearchTargetCommand
                    ?? (_ChangeSearchTargetCommand = new DelegateCommand<SearchTarget?>(target =>
                    {
                        if (target.HasValue && target.Value != SearchOption.SearchTarget)
                        {
                            var payload = SearchPagePayloadContentHelper.CreateDefault(target.Value, SearchOption.Keyword);
                            PageManager.Search(payload);
                        }
                    }));
            }
        }


		#region Commands


		private DelegateCommand _ShowSearchHistoryCommand;
		public DelegateCommand ShowSearchHistoryCommand
		{
			get
			{
				return _ShowSearchHistoryCommand
					?? (_ShowSearchHistoryCommand = new DelegateCommand(() =>
					{
						PageManager.OpenPage(HohoemaPageType.Search);
					}));
			}
		}

        public SearchProvider SearchProvider { get; }

        #endregion

        protected override string ResolvePageName()
        {
            return $"\"{SearchOption.Keyword}\"";
        }

        public override void OnNavigatedTo(NavigatedToEventArgs e, Dictionary<string, object> viewModelState)
		{
            if (e.Parameter is string && e.NavigationMode == NavigationMode.New)
            {
                SearchOption = PagePayloadBase.FromParameterString<MylistSearchPagePayloadContent>(e.Parameter as string);
            }

            SelectedSearchTarget.Value = SearchOption?.SearchTarget ?? SearchTarget.Mylist;

            if (SearchOption == null)
            {
                var oldOption = viewModelState[nameof(SearchOption)] as string;
                SearchOption = PagePayloadBase.FromParameterString<MylistSearchPagePayloadContent>(oldOption);

                if (SearchOption == null)
                {
                    throw new Exception();
                }
            }

            SelectedSearchSort.Value = MylistSearchOptionListItems.FirstOrDefault(x => x.Order == SearchOption.Order && x.Sort == SearchOption.Sort);

            SelectedSearchSort.Subscribe(async opt =>
            {
                SearchOption.Order = opt.Order;
                SearchOption.Sort = opt.Sort;
                SearchOptionText = Services.Helpers.SortHelper.ToCulturizedText(SearchOption.Sort, SearchOption.Order);

                await ResetList();
            })
            .AddTo(_NavigatingCompositeDisposable);

            Database.SearchHistoryDb.Searched(SearchOption.Keyword, SearchOption.SearchTarget);

            

            base.OnNavigatedTo(e, viewModelState);
		}

        public override void OnNavigatingFrom(NavigatingFromEventArgs e, Dictionary<string, object> viewModelState, bool suspending)
        {
            viewModelState[nameof(SearchOption)] = SearchOption.ToParameterString();

            base.OnNavigatingFrom(e, viewModelState, suspending);
        }

        #region Implement HohoemaVideListViewModelBase


        protected override IIncrementalSource<Interfaces.IMylist> GenerateIncrementalSource()
		{
			return new MylistSearchSource(new MylistSearchPagePayloadContent()
            {
                Keyword = SearchOption.Keyword,
                Sort = SearchOption.Sort,
                Order = SearchOption.Order
            } 
            , SearchProvider
            );
		}

		protected override bool CheckNeedUpdateOnNavigateTo(NavigationMode mode)
		{
			var source = IncrementalLoadingItems?.Source as MylistSearchSource;
			if (source == null) { return true; }

			if (SearchOption != null)
			{
				return !SearchOption.Equals(source.SearchOption);
			}
			else
			{
				return base.CheckNeedUpdateOnNavigateTo(mode);
			}
		}

		#endregion
	}

	public class MylistSearchSource : IIncrementalSource<Interfaces.IMylist>
	{
        public MylistSearchSource(
            MylistSearchPagePayloadContent searchOption,
            SearchProvider searchProvider
            )
        {
            SearchOption = searchOption;
            SearchProvider = searchProvider;
        }


        public int MaxPageCount { get; private set; }

		public MylistSearchPagePayloadContent SearchOption { get; private set; }
        public SearchProvider SearchProvider { get; }

        private MylistSearchResponse _MylistGroupResponse;






		public uint OneTimeLoadCount
		{
			get
			{
				return 10;
			}
		}


		public async Task<int> ResetSource()
		{
			// Note: 件数が1だとJsonのParseがエラーになる
			_MylistGroupResponse = await SearchProvider.MylistSearchAsync(
				SearchOption.Keyword,
				0,
				2,
				SearchOption.Sort, 
				SearchOption.Order
				);

			return (int)_MylistGroupResponse.GetTotalCount();
		}


		

		public async Task<IAsyncEnumerable<Interfaces.IMylist>> GetPagedItems(int head, int count)
		{
			var response = await SearchProvider.MylistSearchAsync(
				SearchOption.Keyword
				, (uint)head
				, (uint)count
				, SearchOption.Sort
				, SearchOption.Order
			);

            return response.MylistGroupItems?
                .Select(item => new OtherOwneredMylist(item.VideoInfoItems.Select(x => x.Video.Id).ToList() ?? Enumerable.Empty<string>().ToList())
                {
                    Label = item.Name,
                    Description = item.Description,
                    ItemCount = (int)item.ItemCount,

                    
                } as Interfaces.IMylist)
                .ToAsyncEnumerable()
            ?? AsyncEnumerable.Empty<Interfaces.IMylist>();
        }
	}
}
