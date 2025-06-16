/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

namespace Arteranos.Core
{
    // -------------------------------------------------------------------
    #region Capabilities handling
    public enum UserCapabilities
    {
        CanEnableFly = 0,
        CanFriendUser,
        CanMuteUser,
        CanGagUser,
        CanBlockUser,
        CanKickUser,
        CanBanUser,
        CanViewUsersID,
        CanSendText,
        CanAdminServerUsers,
        CanEditServer,
        CanInitiateWorldTransition,
        CanEditWorld,
    }
    #endregion

}