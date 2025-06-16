/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;
using System.Linq;
using Arteranos.Common;

namespace Arteranos.UI
{
    public class UserPanel_Friends : UserPanelBase
    {
        public override bool LocationVisible => true;

        public override IEnumerable<UserID> GetSocialListTab()
        {
             return from entry in G.NetworkStatus.GetOnlineUsers()
                         where entry != G.Me && G.UserData.IsFriends(entry.UserID)
                         select entry.UserID;
        }
    }
}
