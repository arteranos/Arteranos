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

using Arteranos.Services;
using Arteranos.Core;
using Ipfs;
using System.ComponentModel;

namespace Arteranos.UI
{
    public class PrefPanel_Info : UIBehaviour
    {
        [SerializeField] private TMP_Text lbl_Version = null;
        [SerializeField] private TMP_Text lbl_DeviceName = null;
        [SerializeField] private TMP_Text lbl_DeviceType = null;
        [SerializeField] private TMP_Text lbl_GraphicsDevice = null;
        [SerializeField] private TMP_Text lbl_GraphicsVersion = null;
        [SerializeField] private TMP_Text lbl_LauncherLink = null;
        [SerializeField] private TMP_Text lbl_PublicUserID = null;

        [SerializeField] private Button btn_CopyLLToClipboard = null;
        [SerializeField] private Button btn_CopyUIDToClipboard = null;
        [SerializeField] private Button btn_license = null;
        [SerializeField] private Button btn_3rdParty = null;

        protected override void Awake()
        {
            base.Awake();

            Version v = Version.Load();

            Client cs = G.Client;
            TypeConverter typeConverter = new UserIDConverter();
            UserID userID = cs.MeUserID;
            string publicUIDstring = (string) typeConverter.ConvertTo(userID, typeof(string));

            lbl_Version.text = v.Full;
            lbl_DeviceName.text = SystemInfo.deviceName;
            lbl_DeviceType.text = SystemInfo.deviceType.ToString();
            lbl_GraphicsDevice.text = SystemInfo.graphicsDeviceName;
            lbl_GraphicsVersion.text = SystemInfo.graphicsDeviceVersion;
            lbl_PublicUserID.text = publicUIDstring;


            btn_license.onClick.AddListener(() => OnLicenseClicked(false));
            btn_3rdParty.onClick.AddListener(() => OnLicenseClicked(true));
            btn_CopyLLToClipboard.onClick.AddListener(() => OnClipboardClicked(lbl_LauncherLink));
            btn_CopyUIDToClipboard.onClick.AddListener(() => OnClipboardClicked(lbl_PublicUserID));
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            MultiHash RemotePeerId = SettingsManager.GetServerConnectionData();
            string lltext = "Offline";

            if (RemotePeerId != null)
                lltext = $"arteranos://{RemotePeerId}/";

            lbl_LauncherLink.text = lltext;
        }

        private void Update()
        {
            btn_CopyLLToClipboard.interactable = G.NetworkStatus.GetOnlineLevel() != OnlineLevel.Offline;
        }

        private void OnLicenseClicked(bool thirdparty)
        {
            G.SysMenu.CloseSysMenus();

            LicenseTextUI.New(thirdparty);
        }

        private void OnClipboardClicked(TMP_Text btn) 
            => GUIUtility.systemCopyBuffer = btn.text;

    }
}
