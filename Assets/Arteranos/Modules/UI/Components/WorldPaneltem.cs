/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections;
using TMPro;

using Arteranos.Core;
using Arteranos.Services;
using Arteranos.Core.Operations;
using Arteranos.Core.Managed;
using Arteranos.Common;
using Arteranos.Common.Cryptography;

namespace Arteranos.UI
{
    public class WorldPaneltem : ListItemBase
    {
        private HoverButton btn_Add = null;
        private HoverButton btn_Visit = null;
        private HoverButton btn_Delete = null;
        private HoverButton btn_ChangeWorld = null;

        public IPFSImage img_Screenshot = null;
        public TMP_Text lbl_Caption = null;

        public World World { get; internal set; } = null;
        public PublicWorldData PublicWorldData { get; internal set; } = default;
        public int ServersCount { get; internal set; } = 0;
        public int UsersCount { get; internal set; } = 0;
        public int FriendsMax { get; internal set; } = 0;
        public bool Hidden { get; internal set; } = false;

        private bool AllowedForThis = true;
        private string patternCaption = null;

        protected override void Awake()
        {
            base.Awake();

            patternCaption = lbl_Caption.text;

            btn_Add = btns_ItemButton[0];
            btn_Visit= btns_ItemButton[1];
            btn_Delete= btns_ItemButton[2];
            btn_ChangeWorld = btns_ItemButton[3];

            btn_Add.onClick.AddListener(OnAddClicked);
            btn_Visit.onClick.AddListener(() => OnVisitClicked(false));
            btn_Delete.onClick.AddListener(OnDeleteClicked);
            btn_ChangeWorld.onClick.AddListener(() => OnVisitClicked(true));
        }

        protected override void Start()
        {
            base.Start();

            lbl_Caption.text = "Loading...";

            PopulateWorldData();
        }

        private void PopulateWorldData()
        {
            IEnumerator Cor()
            {
                if (!PublicWorldData.CanView(G.UserData))
                {
                    Hidden = true;
                    lbl_Caption.text = "(not viewable)";
                    yield break;
                }

                PermissionsJSON permission = PublicWorldData.Permissions;
                AllowedForThis = permission != null && !permission.IsInViolation(SettingsManager.ActiveServerData.Permissions);

                VisualizeWorldData();
            }

            if(PublicWorldData.WorldFP == (Fingerprint) null)
            {
                lbl_Caption.text = "(deleted)";
                return;
            }

            StartCoroutine(Cor());
        }

        private void VisualizeWorldData()
        {
            bool deleteable;
            bool addable;
            string lvstr;

            if (World != null)
            {
                // World CID is known, either it's favourited or we're in the current server
                deleteable = World.IsFavourited;
                addable = !deleteable && PublicWorldData.CanPin(G.UserData);
                lvstr = (World.LastSeen == DateTime.MinValue)
                    ? "Never"
                    : World.LastSeen.ToShortDateString();
            }
            else
            {
                // World CID is unknown, we got just a hint from remote server
                deleteable = false;
                addable = false;
                lvstr = "Unknown";
            }

            // If we're in Host mode, you're the admin of your own server, so we're able to
            // change the world. And you still have the great responsibility...
            btn_Visit.gameObject.SetActive(
                G.NetworkStatus.GetOnlineLevel() != OnlineLevel.Host                    // We're in anything but Host mode
                && ServersCount > 0                                                     // We got at least one server to connect to
            );

            // We want to change the world, both on server or locally.
            btn_ChangeWorld.gameObject.SetActive(
                Core.Utils.IsAbleTo(UserCapabilities.CanInitiateWorldTransition, null)  // The user has the server's admin powers
                && AllowedForThis                                                       // The world's content matches
                && World != null                                                        // We have a hold on the world asset
                && !G.World.ChangeInProgress                                            // ... and we're not in a transition.
            );

            btn_Add.gameObject.SetActive(addable);
            btn_Delete.gameObject.SetActive(deleteable);


            lbl_Caption.text = string.Format(patternCaption,
                PublicWorldData.Name,
                lvstr,
                ServersCount,
                UsersCount,
                FriendsMax);

            if (PublicWorldData.ScreenshotCid != null)
                img_Screenshot.Path = PublicWorldData.ScreenshotCid;
        }

        private void OnVisitClicked(bool changeWorld)
        {
            if (changeWorld)
            {
                if (World != null) SettingsManager.EnterWorld(World.RootCid);
                // Change World wouldn't be available at all.
            }
            else
            {
                // Server will know the world assets and tell us about it when we log in.
                ServerSearcher.InitiateServerTransition(PublicWorldData);
            }

            World?.UpdateLastSeen();
        }

        private void OnAddClicked()
        {
            World.Favourite();
            PopulateWorldData();
        }

        private void OnDeleteClicked()
        {
            World.Unfavourite();
            PublicWorldData = PublicWorldData.OfflineWorld();
            PopulateWorldData();
        }
    }
}
