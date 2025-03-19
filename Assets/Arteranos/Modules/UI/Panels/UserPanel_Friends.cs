/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;

using Arteranos.Core;
using Arteranos.Social;

namespace Arteranos.UI
{
    public class UserPanel_Friends : UserPanelBase
    {
        public override bool LocationVisible => true;

        public override IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialListTab()
        {
            IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> list = cs.GetSocialList(null, IsFriends);
            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in list) yield return entry;
        }

        private bool IsFriends(KeyValuePair<UserID, UserSocialEntryJSON> arg) 
            => SocialState.IsFriends(arg.Value.State);
    }
}
