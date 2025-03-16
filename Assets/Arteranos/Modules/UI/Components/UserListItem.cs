/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Arteranos.Avatar;
using Arteranos.XR;
using Arteranos.Core;
using System.Linq;
using Arteranos.Social;
using TMPro;
using Arteranos.Services;
using Ipfs;
using UnityEngine.UI;
using System.Text;

namespace Arteranos.UI
{
    public class UserListItem : ListItemBase
    {
        [SerializeField] private TMP_Text lbl_caption = null;
        [SerializeField] private IPFSImage img_Icon = null;

        private HoverButton btn_AddFriend = null; // Offering Friend or accepting the request
        private HoverButton btn_DelFriend = null; // Revoking Friend offer or unfriend
        private HoverButton btn_Block = null; // Block user
        private HoverButton btn_Unblock= null; // Unblock user
        private HoverButton btn_SendText= null; // Message user
        private HoverButton btn_TravelTo=null; // Travel to specific user

        public UserID targetUserID = null;

        private IAvatarBrain Me = null;
        private Client cs = null;

        private MultiHash server = null;
        private int updateDelay = 0;

        public static UserListItem New(Transform parent, UserID targetUserID)
        {
            GameObject go = Instantiate(BP.I.UIComponents.UserListItem);
            go.transform.SetParent(parent, false);
            UserListItem UserListItem = go.GetComponent<UserListItem>();
            UserListItem.targetUserID = targetUserID;
            return UserListItem;
        }

        protected override void Awake()
        {
            base.Awake();

            btn_AddFriend = btns_ItemButton[0];
            btn_DelFriend= btns_ItemButton[1];
            btn_Block= btns_ItemButton[2];
            btn_Unblock= btns_ItemButton[3];
            btn_SendText= btns_ItemButton[4];
            btn_TravelTo = btns_ItemButton[5];

            btn_AddFriend.onClick.AddListener(GotAddFriendButtonClick);
            btn_DelFriend.onClick.AddListener(GotDelFriendButtonClick);
            btn_Block.onClick.AddListener(GotBlockButtonClick);
            btn_Unblock.onClick.AddListener(GotUnblockButtonClick);
            btn_SendText.onClick.AddListener(GotSendTextButtonClick);
            btn_TravelTo.onClick.AddListener(GotTravelToClick);

            Me = G.Me;
            cs = G.Client;
        }

        protected override void Start()
        {
            base.Start();

            UpdateCaption();

            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);
            Cid Icon;

            if (targetUser == null)
            {
                // Offline user, fetch icon from the social database
                IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> q = G.Client.GetSocialList(targetUserID);
                Icon = q.Any() ? q.First().Value.Icon : null;
            }
            else
            {
                // Online user, fetch icon directly from the avatar
                Icon = targetUser.UserIcon;
            }

            img_Icon.Path = Icon;
        }

        private void UpdateCaption()
        {
            StringBuilder sb = new();
            sb.Append((string)targetUserID);

            (MultiHash server, Cid world) = G.Community.FindFriend(HexString.Encode(targetUserID.Fingerprint));
            if (server != null)
            {
                ServerInfo si = new(server);
                string servername = si.Name ?? "Unknown server";
                string worldname = si.CurrentWorldName ?? "Unknown world";
                sb.Append($"\n{servername}");

                if (world != null)
                {
                    sb.Append($" ({worldname})");
                }
            }

            lbl_caption.text = sb.ToString();
            this.server = server;

            // Spread spectrum, avoid peaks.
            updateDelay = UnityEngine.Random.Range(55, 70);
        }

        private void Update()
        {
            if (--updateDelay >= 0)
                UpdateCaption();

            // When it's hovered, watch for the status updates - both internal and external causes.
            if (go_Overlay.activeSelf)
            {
                IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);

                IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> q = G.Client.GetSocialList(targetUserID);
                
                ulong currentState = q.Any() ? q.First().Value.State : SocialState.None;

                bool friends = SocialState.IsFriendRequested(currentState);

                bool blocked = SocialState.IsBlocked(currentState);

                btn_AddFriend.gameObject.SetActive(!friends && !blocked);
                btn_DelFriend.gameObject.SetActive(friends && !blocked);

                btn_Block.gameObject.SetActive(!blocked && !friends);
                btn_Unblock.gameObject.SetActive(blocked && !friends);

                // Connot send texts to offline users. They could want to deny them.
                if (targetUser != null && G.Me != null)
                    btn_SendText.gameObject.SetActive(Utils.IsAbleTo(UserCapabilities.CanSendText, targetUser));
                else
                    btn_SendText.gameObject.SetActive(false);

                btn_TravelTo.gameObject.SetActive(server != null);
            }
        }


        private void GotAddFriendButtonClick()
        {
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);
            if(targetUser != null)
            {
                Me.OfferFriendship(targetUser, true);
                return;
            }

            cs.UpdateSocialListEntry(targetUserID, (x) =>
            {
                ulong state = x;
                SocialState.SetFriendState(ref state, true);
                return state;
            });
        }

        private void GotDelFriendButtonClick()
        {
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);
            if(targetUser != null)
            {
                Me.OfferFriendship(targetUser, false);
                return;
            }

            cs.UpdateSocialListEntry(targetUserID, (x) =>
            {
                ulong state = x;
                SocialState.SetFriendState(ref state, false);
                return state;
            });
        }

        private void GotBlockButtonClick()
        {
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);
            if(targetUser != null)
            {
                Me.BlockUser(targetUser, true);
                return;
            }

            cs.UpdateSocialListEntry(targetUserID, (x) =>
            {
                ulong state = x;
                SocialState.SetBlockState(ref state, true);
                return state;
            });
        }

        private void GotUnblockButtonClick()
        {
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);
            if(targetUser != null)
            {
                Me.BlockUser(targetUser, false);
                return;
            }
            cs.UpdateSocialListEntry(targetUserID, (x) =>
            {
                ulong state = x;
                SocialState.SetBlockState(ref state, false);
                return state;
            });
        }

        private void GotSendTextButtonClick()
        {
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(targetUserID);
            if (targetUser == null)
            {
                IDialogUI dialog = Factory.NewDialog();
                dialog.Text = "User is offline.";
                dialog.Buttons = new string[] { "OK" };
                return;
            }

            G.SysMenu.CloseSysMenus();
            Factory.NewTextMessage(targetUser);
        }

        private void GotTravelToClick()
        {
            btn_TravelTo.interactable = false;
            // NOTE: Initiating transition, needs to be unhooked from the server list item, which will vanish!
            TaskScheduler.ScheduleCoroutine(() => G.ConnectionManager.ConnectToServer(server, null));

            // Can be removed because of the TOS afreement window, ot other things.
            if (btn_TravelTo != null) btn_TravelTo.interactable = true;
        }
    }
}
