﻿/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Core;
using Arteranos.Social;
using Ipfs.Cryptography.Proto;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arteranos.Avatar
{
    public interface IAvatarBrain : IMonoBehaviour
    {
        uint NetID { get; }
        string Nickname { get; }
        string UserIcon {  get; }
        int AppearanceStatus { get; set; }
        IAvatarBody Body { get; }
        UserID UserID { get; set; }
        PublicKey AgreePublicKey { get; set; }
        UserPrivacy UserPrivacy { get; }
        ulong UserState { get; set; }
        string Address { get; set; }
        string DeviceID { get; set; }

        event Action<int> OnAppearanceStatusChanged;

        void BlockUser(IAvatarBrain receiver, bool blocking = true);
        ulong GetSocialStateTo(IAvatarBrain receiver);
        bool IsAbleTo(UserCapabilities cap, IAvatarBrain target);
        void LogDebug(object message);
        void LogError(object message);
        void LogWarning(object message);
        void NotifyBubbleBreached(IAvatarBrain touchy, bool isFriend, bool entered);
        void OfferFriendship(IAvatarBrain receiver, bool offering = true);
        void PerformEmote(string emoteName);
        void SendTextMessage(IAvatarBrain receiver, string text);
        void SetAppearanceStatusBit(int ASBit, bool set);
        void UpdateSSEffects(IAvatarBrain receiver, ulong state);
        void SendSocialState(IAvatarBrain receiver, ulong state);
        void GotObjectClicked(GameObject clicked);
        void GotObjectGrabbed(GameObject grabbed);
        void GotObjectReleased(GameObject released);
        void GotObjectHeld(GameObject holding, Vector3 position, Quaternion rotation);
        void GotObjectClicked(List<Guid> clicked);
        void GotObjectGrabbed(List<Guid> grabbed);
        void GotObjectReleased(List<Guid> released);
        void GotObjectHeld(List<Guid> holding, Vector3 position, Quaternion rotation);
        void ManageAuthorityOf(GameObject GO, bool auth);
    }
}
