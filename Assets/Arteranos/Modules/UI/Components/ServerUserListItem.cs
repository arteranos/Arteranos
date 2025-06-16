/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Arteranos.Core;
using System.Collections.Generic;

namespace Arteranos.UI
{
    public class ServerUserListItem : ListItemBase
    {
        private HoverButton btn_Unban = null;
        private HoverButton btn_Promote = null;
        private HoverButton btn_Demote = null;

        public ServerUserState user;
        public Image img_Screenshot = null;
        public TMP_Text lbl_Caption = null;

        public static ServerUserListItem New(Transform parent, ServerUserState user)
        {
            GameObject go = Instantiate(BP.I.UIComponents.SserverUserListItem);
            go.transform.SetParent(parent, false);
            ServerUserListItem userListItem = go.GetComponent<ServerUserListItem>();
            userListItem.user = user;
            return userListItem;
        }



        protected override void Awake()
        {
            base.Awake();

            btn_Unban = btns_ItemButton[0];
            btn_Promote = btns_ItemButton[1];
            btn_Demote = btns_ItemButton[2];


            btn_Unban.onClick.AddListener(OnUnbanClicked);
            btn_Promote.onClick.AddListener(OnPromoteClicked);
            btn_Demote.onClick.AddListener(OnDemoteClicked);
        }

        protected override void Start()
        {
            base.Start();

            UpdateUserData();
        }

        private void UpdateUserData()
        {
            (string idline, string statelist) = PopulateUserData(user);

            lbl_Caption.text = $"{idline}\n{statelist}";

            bool IsBanned = UserState.IsBanned(user.userState);
            bool isSrvAdminAsstnt = Bit64field.IsAny(user.userState, UserState.Srv_admin_asstnt);
            bool canEditUsers = Utils.IsAbleTo(Core.UserCapabilities.CanAdminServerUsers, null);

            btn_Unban.gameObject.SetActive(IsBanned && canEditUsers);

            // User in question could not be a server admin of any kind
            btn_Promote.gameObject.SetActive(!UserState.IsSAdmin(user.userState) && !IsBanned && canEditUsers);

            // if it's a deputy server admin it's okay to self-demote.
            btn_Demote.gameObject.SetActive(isSrvAdminAsstnt && canEditUsers);

        }

        private static (string, string) PopulateUserData(ServerUserState user)
        {
            static string ElaborateBanReason(ServerUserState user)
            {
                if (!string.IsNullOrEmpty(user.remarks))
                    return $"Banned ({user.remarks})";

                string reason = BanHandling.FindBanReason(user.userState);

                return $"Banned ({reason ?? "unknown"})";
            }

            List<string> states = new();
            List<string> ids = new();

            if (UserState.IsBanned(user.userState)) states.Add(ElaborateBanReason(user));
            if (Bit64field.IsAny(user.userState, UserState.Srv_admin)) states.Add("Server Admin");
            if (Bit64field.IsAny(user.userState, UserState.Srv_admin_asstnt)) states.Add("Deputy Server Admin");

            if ((string)user.userID != null) ids.Add($"ID: {(string)user.userID}");
            if (user.address != null) ids.Add($"Address: {user.address}");
            if (user.deviceUID != null) ids.Add($"Device ID: {user.deviceUID[0..9]}");

            string statelist = string.Join(", ", states);
            string idlist = string.Join("; ", ids);
            return (idlist, statelist);
        }

        private void UpdateServerUserState()
        {
            CTSPUpdateUserState uss = new()
            {
                toDisconnect = false,
                receiver = user.userID,
                State = user
            };

            SettingsManager.EmitToServerCTSPacket(uss);
            UpdateUserData();
        }

        private void OnUnbanClicked()
        {
            // Clear the rap sheet.
            user.userState &= UserState.GOOD_MASK;
            user.remarks = string.Empty;

            UpdateServerUserState();
        }

        private void OnPromoteClicked()
        {
            user.userState |= UserState.Srv_admin_asstnt;

            UpdateServerUserState();
        }

        private void OnDemoteClicked()
        {
            user.userState &= ~UserState.Srv_admin_asstnt;

            UpdateServerUserState();
        }
    }
}
