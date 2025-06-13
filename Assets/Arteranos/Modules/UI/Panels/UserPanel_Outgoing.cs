/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;
using System.Linq;
using Arteranos.Common;
using Arteranos.Core;
using Arteranos.Social;

namespace Arteranos.UI
{
    public class UserPanel_Outgoing : UserPanelBase
    {
        public override IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialListTab()
        {
            IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> list = Enumerable.Empty<KeyValuePair<UserID, UserSocialEntryJSON>>(); // TODO Stub
            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in list) yield return entry;
        }

        private bool IsFriendOffered(KeyValuePair<UserID, UserSocialEntryJSON> arg) 
            => !SocialState.IsFriendOffered(arg.Value.State)
                && SocialState.IsFriendRequested(arg.Value.State);
    }
}
