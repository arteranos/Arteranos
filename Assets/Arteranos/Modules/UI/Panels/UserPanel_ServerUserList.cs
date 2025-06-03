/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Arteranos.Core;
using Arteranos.XR;
using System;
using System.Collections.Generic;
using Arteranos.Avatar;
using Arteranos.Services;
using Arteranos.Common;

namespace Arteranos.UI
{
    public class UserPanel_ServerUserList : UIBehaviour
    {

        public RectTransform lvc_ServerUserList;

        protected override void Awake() => base.Awake();

        protected override void OnEnable()
        {
            base.OnEnable();

            // Online users first.
            foreach (IAvatarBrain user in G.NetworkStatus.GetOnlineUsers())
                PopulateOnlineSUBItem(user);

            // Prime myself for listening...
            SettingsManager.OnClientReceivedServerUserStateAnswer += PopulateServerSUBItem;

            // And send the query to the server.
            SettingsManager.EmitToServerCTSPacket(new STCUserInfo() { State = new() });
        }

        protected override void OnDisable()
        {
            List<GameObject> list = new();

            for (int i = 0, c = lvc_ServerUserList.transform.childCount; i < c; ++i)
                list.Add(lvc_ServerUserList.transform.GetChild(i).gameObject);

            foreach (GameObject item in list) Destroy(item);

            // Discard the remaining anser packets the server wish to deliver.
            if(SettingsManager.Instance != null)
                SettingsManager.OnClientReceivedServerUserStateAnswer -= PopulateServerSUBItem;

            base.OnDisable();
        }

        private void PopulateOnlineSUBItem(IAvatarBrain onlineUser)
        {
            // If it's there, you'll find it in the database.
            if (onlineUser.UserState != UserState.Normal) return;

            ServerUserState user = new()
            {
                userID = onlineUser.UserID,
                userState = onlineUser.UserState,
                remarks = string.Empty
            };
            PopulateSUBItem(user);
        }
        private void PopulateServerSUBItem(UserID userID, ServerUserState state)
            => PopulateSUBItem(state);

        private void PopulateSUBItem(ServerUserState state) 
            => ServerUserListItem.New(lvc_ServerUserList, state);
    }
}
