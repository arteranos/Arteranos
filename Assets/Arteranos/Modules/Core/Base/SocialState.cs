/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Avatar;
using Arteranos.Core;

namespace Arteranos.Social
{
    // -------------------------------------------------------------------
    #region Social & User constants
    public static class SocialState
    {
        public const ulong None = 0;

        // You offered your friendship to the targeted user.
        private const ulong Own_Friend_requested = (ulong)1 << 0;

        // You blocked the targeted user.
        private const ulong Own_Blocked = (ulong)1 << 4;

        private const int THEM_SHIFT = 16;

        private const ulong OWN_MASK = ((ulong)1 << THEM_SHIFT) - 1;

        // The targeted user offered his frienship to you.
        private const ulong Them_Friend_requested = Own_Friend_requested << THEM_SHIFT;

        // The targeted user blocked you.
        private const ulong Them_Blocked = Own_Blocked << THEM_SHIFT;


        public static void Set(ref ulong field, ulong bits, bool desired) => Bit64field.Set(ref field, bits, desired);

        private static bool IsAll(ulong you, ulong stateBit) => Bit64field.IsAll(you, stateBit);

        private static bool IsAny(ulong you, ulong stateBit) => Bit64field.IsAny(you, stateBit);

        private static bool IsAll(IAvatarBrain target, ulong stateBit) => false; // TODO Stub

        //private static bool IsAny(IAvatarBrain target, ulong stateBit)
        //    => IsAny(G.Me?.GetOwnState(target) ?? None, stateBit);

        public static ulong ReflectSocialState(ulong other, ulong me)
            => (me & OWN_MASK) | (other & OWN_MASK) << THEM_SHIFT;

        public static bool IsFriends(IAvatarBrain target)
            => IsAll(target, Own_Friend_requested | Them_Friend_requested);

        public static bool IsFriends(ulong state)
            => IsAll(state, Own_Friend_requested | Them_Friend_requested);

        public static bool IsFriendOffered(IAvatarBrain target)
            => IsAll(target, Them_Friend_requested);

        public static bool IsFriendOffered(ulong state)
            => IsAll(state, Them_Friend_requested);

        public static bool IsFriendRequested(IAvatarBrain target)
            => IsAll(target, Own_Friend_requested);

        public static bool IsFriendRequested(ulong state)
            => IsAll(state, Own_Friend_requested);

        public static bool IsBlocked(IAvatarBrain target)
            => IsAll(target, Own_Blocked);

        public static bool IsBlocked(ulong state)
            => IsAll(state, Own_Blocked);

        public static bool IsBeingBlocked(IAvatarBrain target)
            => IsAll(target, Them_Blocked);

        public static bool IsBeingBlocked(ulong state)
            => IsAll(state, Them_Blocked);

        public static bool IsSomehowBlocked(ulong state)
            => IsAny(state, Own_Blocked | Them_Blocked);

        public static void SetFriendState(ref ulong state, bool value)
            => Set(ref state, Own_Friend_requested, value);

        public static void SetBlockState(ref ulong state, bool value)
            => Set(ref state, Own_Blocked, value);

        /// <summary>
        /// checks if the action (like seeing someone's user name or sending texts) is permitted
        /// </summary>
        /// <param name="target">The target user</param>
        /// <param name="visibility">The target's visibility setting, or your setting if this action is being remotely invoked</param>
        /// <returns>Self explanatory.</returns>
        public static bool IsPermitted(IAvatarBrain target, UserVisibility visibility)
        {
            return visibility switch
            {
                UserVisibility.none => false,
                UserVisibility.friends => IsFriends(target),
                UserVisibility.everyone => true,
                _ => throw new System.NotImplementedException() // Not gonna happen. Supposedly...
            };
        }
    }

    #endregion
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