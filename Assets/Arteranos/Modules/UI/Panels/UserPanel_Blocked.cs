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
using Arteranos.Common;
using System.Linq;

namespace Arteranos.UI
{
    public class UserPanel_Blocked : UserPanelBase
    {
        public override IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialListTab()
        {
            IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> list = Enumerable.Empty<KeyValuePair<UserID, UserSocialEntryJSON>>(); // TODO Stub
            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in list) yield return entry;
        }

        private bool IsFriends(KeyValuePair<UserID, UserSocialEntryJSON> arg)
            => SocialState.IsBlocked(arg.Value.State);
    }
}
