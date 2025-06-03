/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;
using Arteranos.Common;
using Arteranos.Core;
using Arteranos.Social;

namespace Arteranos.UI
{
    public class UserPanel_Incoming : UserPanelBase
    {
        public override IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialListTab()
        {
            IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> list = cs.GetSocialList(null, IsFriendReceived);
            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in list) yield return entry;
        }

        private bool IsFriendReceived(KeyValuePair<UserID, UserSocialEntryJSON> arg)
        {
            return SocialState.IsFriendOffered(arg.Value.State)
                && !SocialState.IsFriendRequested(arg.Value.State);
        }
    }
}
