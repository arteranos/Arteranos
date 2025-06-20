/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;

using Arteranos.Avatar;
using Arteranos.Core;
using TMPro;
using Ipfs;
using System.Text;
using Arteranos.Common;

namespace Arteranos.UI
{
    public class UserListItem : ListItemBase
    {
        [SerializeField] private TMP_Text lbl_caption = null;
        [SerializeField] private IPFSImage img_Icon = null;

        public UserID TargetUserID { get; set; } = null;
        public bool LocationVisible { get; set; } = false;

        private HoverButton btn_AddFriend = null; // Offering Friend or accepting the request
        private HoverButton btn_DelFriend = null; // Revoking Friend offer or unfriend
        private HoverButton btn_Block = null; // Block user
        private HoverButton btn_Unblock= null; // Unblock user
        private HoverButton btn_SendText= null; // Message user
        private HoverButton btn_TravelTo=null; // Travel to specific user


        private IAvatarBrain Me = null;
        private Client cs = null;

        private MultiHash server = null;
        private int updateDelay = 0;

        public static UserListItem New(Transform parent, UserID targetUserID, bool locationVisible)
        {
            GameObject go = Instantiate(BP.I.UIComponents.UserListItem);
            go.transform.SetParent(parent, false);
            UserListItem UserListItem = go.GetComponent<UserListItem>();
            UserListItem.TargetUserID = targetUserID;
            UserListItem.LocationVisible = locationVisible;
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
        }

        private void UpdateCaption()
        {
            StringBuilder sb = new();
            sb.Append((string)TargetUserID);

            (MultiHash server, Cid world) = G.Community.FindFriend(HexString.Encode(TargetUserID.Fingerprint));
            if (server != null && LocationVisible)
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
            this.server = LocationVisible ? server : null;

            img_Icon.Path = (Cid)TargetUserID;

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
                IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(TargetUserID);

                bool friends = G.UserData.IsFriendOffered(TargetUserID);

                bool blocked = G.UserData.IsBlocked(TargetUserID);

                btn_AddFriend.gameObject.SetActive(!friends && !blocked);
                btn_DelFriend.gameObject.SetActive(friends && !blocked);

                btn_Block.gameObject.SetActive(!blocked && !friends);
                btn_Unblock.gameObject.SetActive(blocked && !friends);

                // Cannot send texts to offline users. They could want to deny them.
                if (targetUser != null && G.Me != null)
                    btn_SendText.gameObject.SetActive(Core.Utils.IsAbleTo(UserCapabilities.CanSendText, targetUser));
                else
                    btn_SendText.gameObject.SetActive(false);

                btn_TravelTo.gameObject.SetActive(server != null);
            }
        }


        private void GotAddFriendButtonClick()
        {
            G.UserData.OfferFriend(TargetUserID, true);
            G.UserData.Save();
        }

        private void GotDelFriendButtonClick()
        {
            G.UserData.OfferFriend(TargetUserID, false);
            G.UserData.Save();
        }

        private void GotBlockButtonClick()
        {
            G.UserData.ImposeBlock(TargetUserID, true);
            G.UserData.Save();
        }

        private void GotUnblockButtonClick()
        {
            G.UserData.ImposeBlock(TargetUserID, false);
            G.UserData.Save();
        }

        private void GotSendTextButtonClick()
        {
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(TargetUserID);
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
