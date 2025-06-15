/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Common;

namespace Arteranos.Core
{
    public class UserData : UserDataJSON
    {
        public UserData() : base() { }
        public UserData(UserDataJSON udj)
        {
            SignKeyPair = udj.SignKeyPair;
            Nickname = udj.Nickname;
            Icon = udj.Icon;

            _dirty = false;
        }
        public override bool OfferFriend(UserID target, bool offering)
        {
            bool changed = base.OfferFriend(target, offering);
            if (changed)
            {
                if (G.Me != null) G.Me.RelayFriendState(target);
                Save();
            }

            return changed;
        }

        public override bool ImposeBlock(UserID target, bool imposing)
        {
            bool changed = base.ImposeBlock(target, imposing);
            if (changed)
            {
                if (G.Me != null) G.Me.RelayFriendState(target);
                Save();
            }

            return changed;
        }
    }
}
