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
                yield return World.ScreenshotPNG.WaitFor();
                yield return World.WorldInfo.WaitFor();

                // Cid seems to be invalid, or expired, or unreachable.
                if (World.WorldInfo == null)
                {
                    lbl_Caption.text = "(unavailable)";
                    yield break;
                }

                if (!World.CanView(G.Client.MeUserID))
                {
                    Hidden = true;
                    lbl_Caption.text = "(not viewable)";
                    yield break;
                }

                WorldInfo worldInfo = World.WorldInfo;
                ServerPermissions permission = worldInfo.ContentRating;
                AllowedForThis = permission != null && !permission.IsInViolation(SettingsManager.ActiveServerData.Permissions);

                VisualizeWorldData();
            }

            if(World == null)
            {
                lbl_Caption.text = "(deleted)";
                return;
            }

            StartCoroutine(Cor());
        }

        private void VisualizeWorldData()
        {
            WorldInfo WorldInfo = World.WorldInfo;

            // If we're in Host mode, you're the admin of your own server, so we're able to
            // change the world. And you still have the great responsibility...
            btn_Visit.gameObject.SetActive(G.NetworkStatus.GetOnlineLevel() != OnlineLevel.Host);
            btn_ChangeWorld.gameObject.SetActive(
                Core.Utils.IsAbleTo(Social.UserCapabilities.CanInitiateWorldTransition, null)
                && AllowedForThis
                && !G.World.ChangeInProgress);

            bool pinnable = !World.IsFavourited && World.CanPin(G.Client.MeUserID);

            btn_Add.gameObject.SetActive(pinnable);
            btn_Delete.gameObject.SetActive(World.IsFavourited);

            string lvstr = (World.LastSeen == DateTime.MinValue)
                ? "Never"
                : World.LastSeen.ToShortDateString();

            lbl_Caption.text = string.Format(patternCaption,
                WorldInfo.WorldName,
                lvstr,
                ServersCount,
                UsersCount,
                FriendsMax);

            if (World.ScreenshotPNG != null)
                img_Screenshot.ImageData = World.ScreenshotPNG;
        }

        private void OnVisitClicked(bool inPlace)
        {
            if(!string.IsNullOrEmpty(World.RootCid))
            {
                if (inPlace)
                    SettingsManager.EnterWorld(World.RootCid);
                else
                    ServerSearcher.InitiateServerTransition(World.RootCid);

                World.UpdateLastSeen();
            }
        }

        private void OnAddClicked()
        {
            World.Favourite();
            PopulateWorldData();
        }

        private void OnDeleteClicked()
        {
            World.Unfavourite();
            World = null;
            PopulateWorldData();
        }
    }
}
