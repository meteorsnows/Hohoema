﻿using NicoPlayerHohoema.Services;
using NicoPlayerHohoema.Services.Page;
using Prism.Commands;

namespace NicoPlayerHohoema.Views.Subscriptions
{
    public sealed class OpenSubscriptionSourceCommand : DelegateCommandBase
    {
        public OpenSubscriptionSourceCommand(Services.PageManager pageManager)
        {
            PageManager = pageManager;
        }

        public Services.PageManager PageManager { get; }

        protected override bool CanExecute(object parameter)
        {
            return parameter is Models.Subscription.SubscriptionSource;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is Models.Subscription.SubscriptionSource source)
            {
                switch (source.SourceType)
                {
                    case Models.Subscription.SubscriptionSourceType.User:
                        PageManager.OpenPage(HohoemaPageType.UserVideo, source.Parameter);
                        break;
                    case Models.Subscription.SubscriptionSourceType.Channel:
                        PageManager.OpenPage(HohoemaPageType.ChannelVideo, source.Parameter);
                        break;
                    case Models.Subscription.SubscriptionSourceType.Mylist:
                        var mylistPagePayload = new MylistPagePayload(source.Parameter)
                        {
                            Origin = Services.PlaylistOrigin.OtherUser
                        };
                        PageManager.OpenPage(HohoemaPageType.Mylist, mylistPagePayload.ToParameterString());
                        break;
                    case Models.Subscription.SubscriptionSourceType.TagSearch:
                        PageManager.SearchTag(source.Parameter, Mntone.Nico2.Order.Descending, Mntone.Nico2.Sort.FirstRetrieve);
                        break;
                    case Models.Subscription.SubscriptionSourceType.KeywordSearch:
                        PageManager.SearchKeyword(source.Parameter, Mntone.Nico2.Order.Descending, Mntone.Nico2.Sort.FirstRetrieve);
                        break;
                    default:
                        break;
                }


            }
        }
    }
}
