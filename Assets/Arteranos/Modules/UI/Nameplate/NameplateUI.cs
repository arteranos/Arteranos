/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Arteranos.Avatar;
using Arteranos.XR;
using Arteranos.Social;
using UnityEngine.Diagnostics;
using System;
using Arteranos.Common;

namespace Arteranos.UI
{
    public class NameplateUI : UIBehaviour, INameplateUI
    {
        public IAvatarBrain Bearer { get; set; } = null;

        [SerializeField] private TextMeshProUGUI lbl_Name = null;
        [SerializeField] private TextMeshProUGUI lbl_Caption = null;
        [SerializeField] private Button btn_mute = null;
        [SerializeField] private Button btn_unmute = null;
        [SerializeField] private Button btn_friend_add = null;
        [SerializeField] private Button btn_block = null;
        [SerializeField] private Button btn_send_text = null;
        [SerializeField] private Button btn_kick_user = null;

        private readonly Vector3 aboutFace = new(0, 180, 0);

        protected override void Awake()
        {
            base.Awake();

            btn_mute.onClick.AddListener(OnMuteButtonClicked);
            btn_unmute.onClick.AddListener(OnMuteButtonClicked);
            btn_friend_add.onClick.AddListener(OnFriendAddButtonClicked);
            btn_block.onClick.AddListener(OnBlockButtonClicked);
            btn_send_text.onClick.AddListener(OnSendTextButtonClicked);
            btn_kick_user.onClick.AddListener(OnKickUserButtonClicked);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            lbl_Name.text = GetLabelText();

            Bearer.OnAppearanceStatusChanged += OnAppearanceStatusChanged;
            float fullHeight = Bearer.Body.AvatarMeasures.FullHeight; // Maybe the avatar isn#t loaded yet.
            // TODO Up vector? LocalPosition respects the root rotation, but...
            transform.SetLocalPositionAndRotation(new Vector3(0, fullHeight + 0.2f, 0), Quaternion.Euler(0, 180, 0));
            OnAppearanceStatusChanged(Bearer.AppearanceStatus);
        }

        protected override void OnDisable()
        {
            Bearer.OnAppearanceStatusChanged -= OnAppearanceStatusChanged;

            base.OnDisable();
        }

        private void Update()
        {
            lbl_Caption.text = GetCaptionText();

            // LookAt can take a second vector as the Up vector.
            // Seems that we'd use _the client's_ Up vector, not the targeted avatar's.
            transform.LookAt(G.XRControl.cameraTransform);
            transform.Rotate(aboutFace);
        }

        private string GetLabelText() => Bearer.Nickname;

        private string GetCaptionText()
        {
            string capMessage;

            if (Core.Utils.IsAbleTo(UserCapabilities.CanViewUsersID, Bearer))
                capMessage = CryptoHelpers.ToString(
                    Core.Utils.GetUIDFPEncoding(Bearer.UserPrivacy.UIDRepresentation), Bearer.UserID);
            else capMessage = "<Undisclosed user name>";

            DateTime now = DateTime.Now;

            // Overlay the informational message for three out of six seconds.
            if(now.Second % 6 > 2)
            {
                if (SocialState.IsFriends(Bearer)) capMessage = "Friend";
                else if (SocialState.IsFriendOffered(Bearer)) capMessage = "Wants to be your friend";
                else if (SocialState.IsFriendRequested(Bearer)) capMessage = "Friend request sent";
            }

            return capMessage;
        }

        private void OnAppearanceStatusChanged(int status)
        {
            // No point of dealing with the blocked status - it isn't visible if it is blocked.
            btn_friend_add.gameObject.SetActive(Core.Utils.IsAbleTo(UserCapabilities.CanFriendUser, Bearer));

            btn_block.gameObject.SetActive(Core.Utils.IsAbleTo(UserCapabilities.CanBlockUser, Bearer));

            //       Design: Speaker on if the audio status okay and not individually muted
            //               Speaker off it it's anything else
            // Interactable: Only if it's not self-muted and not gagged
            Button current = (!AppearanceStatus.IsSilent(status)) ? btn_mute : btn_unmute;

            btn_mute.gameObject.SetActive(current == btn_mute);
            btn_unmute.gameObject.SetActive(current == btn_unmute);

            // Only interactable if there's no other reason for it.
            current.interactable = (status & ~AppearanceStatus.Muted) == AppearanceStatus.OK;
            // And if the viewed user is an admin, you cannot mute him at all. :)
            current.gameObject.SetActive(Core.Utils.IsAbleTo(UserCapabilities.CanMuteUser, Bearer));

            // Self explanatory.
            btn_send_text.gameObject.SetActive(Core.Utils.IsAbleTo(UserCapabilities.CanSendText, Bearer));
            
            // Combined function, further inquiry if it's for kicking or banning.
            btn_kick_user.gameObject.SetActive(
Core.Utils.IsAbleTo(UserCapabilities.CanKickUser, Bearer) ||
Core.Utils.IsAbleTo(UserCapabilities.CanBanUser, Bearer)
                );
        }

        private void OnMuteButtonClicked()
        {
            // Regular users can mute other users
            Bearer.AppearanceStatus ^= AppearanceStatus.Muted;

            // Elevated users can gag other users.
            // TODO User privileges management
            // Bearer.AudioStatus ^= Avatar.AudioStatus.Gagged;

            // Update the visible state
            OnAppearanceStatusChanged(Bearer.AppearanceStatus);
        }

        private void OnBlockButtonClicked()
        {
            G.Me.BlockUser(Bearer);
            OnAppearanceStatusChanged(Bearer.AppearanceStatus);
        }

        private void OnFriendAddButtonClicked()
        {
            G.Me.OfferFriendship(Bearer);
            OnAppearanceStatusChanged(Bearer.AppearanceStatus);
        }

        private void OnSendTextButtonClicked()
        {
            G.SysMenu.CloseSysMenus();
            Factory.NewTextMessage(Bearer);
        }

        private void OnKickUserButtonClicked()
        {
            G.SysMenu.CloseSysMenus();
            Factory.NewKickBan(Bearer);
        }
    }
}
