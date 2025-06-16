/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Arteranos.Core;
using Arteranos.Avatar;

namespace Arteranos.UI
{
    public class KickBanUI : UIBehaviour, IKickBanUI
    {
        [SerializeField] private TMP_Text lbl_Reason = null;
        [SerializeField] private Spinner spn_Reason = null;
        [SerializeField] private TMP_InputField txt_ReasonDetail = null;
        [SerializeField] private TMP_Text txt_RD_Hint = null;
        [SerializeField] private TMP_Text lbl_BanHow = null;
        [SerializeField] private GameObject grp_BanHow = null;
        [SerializeField] private Toggle chk_Ban_UID = null;
        [SerializeField] private Toggle chk_Ban_Address = null;
        [SerializeField] private Toggle chk_Ban_DID = null;
        [SerializeField] private Button btn_Cancel = null;
        [SerializeField] private Button btn_Commit = null;
        [SerializeField] private TMP_Text btn_Commit_txt = null;

        public IAvatarBrain Target { get; set; } = null;

        protected override void Awake()
        {
            base.Awake();

            spn_Reason.Options = BanHandling.ReasonList().ToArray();
            spn_Reason.OnChanged += OnReasonChange;
            

            chk_Ban_UID.onValueChanged.AddListener(OnBanMethodChange);
            chk_Ban_Address.onValueChanged.AddListener(OnBanMethodChange);
            chk_Ban_DID.onValueChanged.AddListener(OnBanMethodChange);

            btn_Commit.onClick.AddListener(OnCommitClicked);
            btn_Cancel.onClick.AddListener(OnCancelClicked);

            OnBanMethodChange(false);
        }

        protected override void Start()
        {
            base.Start();

            if(Target == null) throw new ArgumentNullException("target");

            // If you cannot ban the target, hide the portion of the UI
            if(!Utils.IsAbleTo(Core.UserCapabilities.CanBanUser, Target))
            {
                lbl_BanHow.gameObject.SetActive(false);
                grp_BanHow.SetActive(false);
            }

            lbl_Reason.text = string.Format(lbl_Reason.text, Target.Nickname);
        }

        private void OnReasonChange(int arg1, bool arg2)
        {
            txt_RD_Hint.text = BanHandling.GetReasonText(arg1);
            OnBanMethodChange(false);
        }

        private void OnBanMethodChange(bool arg0)
        {
            btn_Commit_txt.text = IsBanning()
                ? "Ban"
                : "Kick";
        }

        private bool IsBanning()
        {
            return (chk_Ban_DID.isOn || chk_Ban_UID.isOn || chk_Ban_Address.isOn) &&
                spn_Reason.value != 0;
        }

        private void OnCommitClicked()
        {
            ulong banbits = BanHandling.GetReasonBit(spn_Reason.value) |
                (IsBanning() ? UserState.Banned : 0);

            // Fill out the pattern of the targeted user action and transmit it
            // to the server to fill the fields and do the final decision.

            CTSPUpdateUserState uss = new()
            {
                toDisconnect = true,
                receiver = Target.UserID,
                State = new()
                {
                    userID = chk_Ban_UID.isOn ? Target.UserID : null,
                    userState = banbits,
                    address = chk_Ban_Address.isOn ? string.Empty : null,
                    deviceUID = chk_Ban_DID.isOn ? string.Empty : null,
                    remarks = txt_ReasonDetail.text,
                }
            };

            SettingsManager.EmitToServerCTSPacket(uss);
            Destroy(gameObject);
        }

        private void OnCancelClicked() => Destroy(gameObject);
    }
}