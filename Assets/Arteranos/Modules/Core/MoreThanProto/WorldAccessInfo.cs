/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;
using System.IO;

namespace Arteranos.Core
{
    // Ref. #89 - World access control
    public partial class WorldAccessInfo
    {
        public bool IsBanned(UserID userID) => 
            BannedUsers.Contains(userID) && 
            (AccessAuthor != userID && WorldAuthor != userID);
        public bool CanView(UserID userID) => CheckUAL(userID, WorldAccessInfoLevel.View);
        public bool CanPin(UserID userID) => CheckUAL(userID, WorldAccessInfoLevel.Pin);
        public bool CanEdit(UserID userID) => CheckUAL(userID, WorldAccessInfoLevel.Edit);
        public bool CanAdmin(UserID userID) => CheckUAL(userID, WorldAccessInfoLevel.Admin) || userID == AccessAuthor;

        public bool CheckUAL(UserID user, WorldAccessInfoLevel neededAL)
        {
            if(IsBanned(user)) return false;

            if(WorldAuthor == user) return true;

            return UserALs.ContainsKey(user)
                ? neededAL >= UserALs[user]
                : neededAL >= DefaultLevel;
        }

        public void ModifyAL(UserID userID, WorldAccessInfoLevel neededAL) 
            => UserALs[userID] = neededAL;

        public void RemoveUser(UserID userID) 
            => UserALs.Remove(userID);
    }
}