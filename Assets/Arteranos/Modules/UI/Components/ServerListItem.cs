/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using TMPro;
using UnityEngine;

using Arteranos.Core;

namespace Arteranos.UI
{
    public class ServerListItem : ListItemBase
    {
        public string PeerID = null;
        public IPFSImage img_Icon = null;
        public TMP_Text lbl_Caption = null;

        public Color BgndRegular;
        public Color BgndWarning;

        private HoverButton btn_Add = null;
        private HoverButton btn_Info = null;
        private HoverButton btn_Visit = null;
        private HoverButton btn_Delete = null;

        private ServerInfo si = null;

        public static ServerListItem New(Transform parent, string PeerID)
        {
            GameObject go = Instantiate(BP.I.UIComponents.ServerListItem);
            go.transform.SetParent(parent, false);
            ServerListItem serverListItem = go.GetComponent<ServerListItem>();
            serverListItem.PeerID = PeerID;
            return serverListItem;
        }

        protected override void Awake()
        {
            base.Awake();

            btn_Add = btns_ItemButton[0];
            btn_Info = btns_ItemButton[1];
            btn_Visit = btns_ItemButton[2];
            btn_Delete = btns_ItemButton[3];

            btn_Add.onClick.AddListener(OnAddClicked);
            btn_Info.onClick.AddListener(OnInfoClicked);
            btn_Visit.onClick.AddListener(OnVisitClicked);
            btn_Delete.onClick.AddListener(OnDeleteClicked);

            lbl_Caption.text = "Loading...";
        }

        protected override void Start()
        {
            base.Start();

            UpdateServerData();
        }

        public void UpdateServerData()
        {
            void ShowOnlineDetails()
            {
                // Refresh data
                si = new(PeerID);

                TMP_Text btn_VisitText = btn_Visit.transform.GetChild(0).GetComponent<TMP_Text>(); ;
                btn_VisitText.text = !si.IsOnline
                    ? "Try to\nvisit"
                    : "Visit";
                // TODO Inactive if the world is restricted to client's user
                btn_Visit.gameObject.SetActive(true); //  (si.IsOnline);

                if (!si.IsOnline)
                    lbl_Caption.text = $"{si.Name} (Offline)";
                else
                    lbl_Caption.text =
                        $"Server: {$"{si.Name}"} (Users: {si.UserCount}, Friends: {si.FriendCount})\n" +
                        $"Current World: {si.CurrentWorldName}";
            }

            void ShowServerDetails()
            {
                // Refresh data
                si = new(PeerID);

                btn_Add.gameObject.SetActive(false);
                btn_Delete.gameObject.SetActive(false);

                btn_Background.image.color = (si.UsesCustomTOS && !G.Client.AllowCustomTOS)
                    ? BgndWarning
                    : BgndRegular;

                if (si.IsValid)
                {
                    lbl_Caption.text = $"{si.Name} (Unknown)";

                    btn_Add.gameObject.SetActive(false);
                    btn_Delete.gameObject.SetActive(true);
                    return;
                }


                lbl_Caption.text = $"({PeerID}) (Unknown)";

                btn_Add.gameObject.SetActive(true);
                btn_Delete.gameObject.SetActive(true);

            }

            ShowServerDetails();

            img_Icon.Path = si.ServerIcon;

            // With Pubsub, we get the data delivered. Without Pubsub, we need to ask for it.
            ShowOnlineDetails();
        }

        private void OnVisitClicked()
        {
            btn_Visit.interactable = false;                
            // NOTE: Initiating transition, needs to be unhooked from the server list item, which will vanish!
            TaskScheduler.ScheduleCoroutine(() => G.ConnectionManager.ConnectToServer(PeerID, null));

            // Can be removed because of the TOS afreement window, ot other things.
            if(btn_Visit != null) btn_Visit.interactable = true;
        }

        private void OnAddClicked()
        {
            Client cs = G.Client;

            // Put it down into our bookmark list.
            if(!cs.ServerList.Contains(PeerID))
            {
                cs.ServerList.Add(PeerID);
                cs.Save();
            }

            UpdateServerData();
        }

        private void OnDeleteClicked()
        {
            Client cs = G.Client;

            // Strike it from our list
            if(cs.ServerList.Contains(PeerID))
                cs.ServerList.Remove(PeerID);

            // The server public is entered with the server port, not the MD port.
            string key = si.SPKDBKey;

            if(cs.ServerPasses.ContainsKey(key))
                cs.ServerPasses.Remove(key);

            cs.Save();

            // Delete the online and public data
            si.Delete();

            // And, zip, gone.
            Destroy(gameObject);
        }

        private void OnInfoClicked()
        {
            ServerInfoUI.New(si);
        }
    }
}
