/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */


using Arteranos.Core;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arteranos.UI
{
    public class ServerInfoUI : UIBehaviour
    {
        public static ServerInfoUI New(ServerInfo si)
        {
            BP.I.UI.ServerInfo.SetActive(false);
            GameObject go = Instantiate(BP.I.UI.ServerInfo);
            ServerInfoUI serverInfoUI = go.GetComponent<ServerInfoUI>();
            serverInfoUI.si = si;
            go.SetActive(true);
            return serverInfoUI;
        }

        [SerializeField] private Button btn_Close;
        [SerializeField] private TMP_Text lbl_Name;
        [SerializeField] private TMP_Text lbl_Address;
        [SerializeField] private IPFSImage img_Icon;
        [SerializeField] private TMP_Text lbl_LastUpdated;
        [SerializeField] private TMP_Text lbl_LastOnline;
        [SerializeField] private TMP_Text lbl_MatchIndex;
        [SerializeField] private TMP_Text lbl_AdminList;
        [SerializeField] private TMP_Text lbl_World;
        [SerializeField] private TMP_Text lbl_Description;

        private ServerInfo si = null;

        protected override void Awake()
        {
            base.Awake();

            btn_Close.onClick.AddListener(() => Destroy(gameObject));
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            Populate();
        }

        private void Populate()
        {
            string peerID = si.PeerID.ToString();
            lbl_Name.text = si.Name;
            lbl_Address.text = $"{peerID[..12]}...{peerID[^12..]}";
            lbl_LastUpdated.text = si.LastUpdated.HumanReadable();
            lbl_LastOnline.text =  si.LastOnline != System.DateTime.MinValue ? si.LastOnline.HumanReadable() : "Never seen so far";
            lbl_MatchIndex.text = si.Permissions.HumanReadableMI(
                G.Client.Permissions
                ).ToString();
            lbl_Description.text = si.Description.ToString();

            lbl_AdminList.text = string.Join(", ", si.AdminNames);

            img_Icon.Path = si.ServerIcon;

            lbl_World.text = si.CurrentWorldName;
        }
    }
}