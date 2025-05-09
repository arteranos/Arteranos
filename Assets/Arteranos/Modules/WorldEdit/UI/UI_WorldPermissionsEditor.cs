/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Core;
using Arteranos.Core.Managed;
using Arteranos.UI;
using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Arteranos.WorldEdit
{
    internal class ACLEntry
    {
        public UserID user;
        public WorldAccessInfoLevel accessLevel;
    }

    public class UI_WorldPermissionsEditor : ActionPage
    {
        [SerializeField] private TMP_Text lbl_title;
        [SerializeField] private Spinner spn_DefaultPermission;
        [SerializeField] private ObjectChooser obc_CustomPermissions;
        [SerializeField] private Spinner spn_addUser;
        [SerializeField] private Button btn_addUser;

        private WorldAccessInfo accessInfo => G.WorldEditorData.WorldAccessInfo;

        private string titlePattern = null;

        private List<ACLEntry> aclEntries = null;
        private List<UserID> usersToAdd = null;

        protected override void Awake()
        {
            base.Awake();

            obc_CustomPermissions.OnShowingPage += PreparePage;
            obc_CustomPermissions.OnPopulateTile += PopulateTile;

            btn_addUser.onClick.AddListener(GotAddUser);
        }

        protected override void OnDestroy()
        {
            obc_CustomPermissions.OnShowingPage -= PreparePage;
            obc_CustomPermissions.OnPopulateTile -= PopulateTile;

            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            IEnumerator Cor()
            {
                World world = G.World.World;

                yield return world.WorldInfo.WaitFor();

                titlePattern ??= lbl_title.text;

                WorldInfo info = world.WorldInfo;

                lbl_title.text = string.Format(titlePattern, info.WorldName, (string)info.Author);

                spn_DefaultPermission.value = (int)accessInfo.DefaultLevel;

                RebuildACLView();

                RebuildPossibleUsers();

                obc_CustomPermissions.ShowPage(0);
            }

            StartCoroutine(Cor());
        }

        private void RebuildPossibleUsers()
        {
            // throw new NotImplementedException();
        }

        private void RebuildACLView()
        {
            accessInfo.UserALs ??= new();

            // Sort names. OrderedDictionary is not available for us.
            List<UserID> list = accessInfo.UserALs.Keys.ToList();
            list.Sort((x, y) => x.Nickname.CompareTo(y.Nickname));

            aclEntries = (from entry in list
                          select new ACLEntry()
                          {
                              user = entry,
                              accessLevel = accessInfo.UserALs[entry],
                          }).ToList();
        }

        private void PopulateTile(int index, GameObject @object)
        {
            ACLEntry entry = aclEntries[index];

            WorldPermissionListItem item = @object.GetComponent<WorldPermissionListItem>();
            item.Parent = this;
            item.UserID = entry.user;
            item.Permission = entry.accessLevel;
        }

        private void PreparePage(int obj)
        {
            obc_CustomPermissions.UpdateItemCount(aclEntries.Count);
        }

        private void GotAddUser()
        {
            UserID newUser = usersToAdd[spn_addUser.value];

            accessInfo.UserALs[newUser] = WorldAccessInfoLevel.Nothing;

            RebuildACLView();

            obc_CustomPermissions.ShowPage(obc_CustomPermissions.CurrentPage);
        }

        public void GotACLEntryChanged(UserID userID, WorldAccessInfoLevel? newLevel)
        {
            if(newLevel == null)
            {
                // Entry to remove
                accessInfo.UserALs.Remove(userID);
                RebuildACLView();

                // If applicable, put user to the candidates to re-add
                RebuildPossibleUsers();
                obc_CustomPermissions.ShowPage(obc_CustomPermissions.CurrentPage);
                return;
            }
            else
            {
                // Entry to change
                accessInfo.UserALs[userID] = newLevel.Value;

                // ... maybe just RebuildACL() ?
                ACLEntry entry = aclEntries.Find(e => e.user == userID);
                if (entry != null) entry.accessLevel = newLevel.Value;
            }
        }
    }
}