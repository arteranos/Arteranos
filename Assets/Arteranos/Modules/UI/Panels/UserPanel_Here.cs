/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections;
using System.Collections.Generic;

using Arteranos.Core;
using Arteranos.Avatar;
using Arteranos.Social;
using Arteranos.XR;
using Arteranos.Services;
using Arteranos.Common;

namespace Arteranos.UI
{
    public class UserPanel_Here : UserPanelBase
    {
        public override IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialListTab()
        {
            Dictionary<UserID, UserSocialEntryJSON> list = new();

            // Get the currently logged-in users with the default state....
            foreach(IAvatarBrain user in G.NetworkStatus.GetOnlineUsers())
            {
                if(user.UserID == G.Me.UserID) continue;

                list[user.UserID] = new()
                {
                    State = SocialState.None,
                    Icon = null // No matter, it's delayed load in the UserListItem.
                };
            }

            // Fill in the subset of the data in the social database.
            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in cs.GetSocialList())
            {
                if(!list.ContainsKey(entry.Key)) continue;

                list[entry.Key] = entry.Value;
            }

            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in list) 
                yield return entry;
        }

    }
}
