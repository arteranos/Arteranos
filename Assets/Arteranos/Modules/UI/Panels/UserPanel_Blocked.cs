/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;

using Arteranos.Common;
using System.Linq;

namespace Arteranos.UI
{
    public class UserPanel_Blocked : UserPanelBase
    {
        public override IEnumerable<UserID> GetSocialListTab()
        {
             return from entry in G.NetworkStatus.GetOnlineUsers()
                         where entry != G.Me && G.UserData.IsImposingBlock(entry.UserID)
                         select entry.UserID;
        }
    }
}
