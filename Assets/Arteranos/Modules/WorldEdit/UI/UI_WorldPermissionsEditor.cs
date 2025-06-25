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
using Arteranos.Common;

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

        private WorldAccessInfo _accessInfo => G.WorldEditorData.WorldAccessInfo;

        private string _titlePattern = null;

        private List<ACLEntry> _aclEntries = null;
        private List<UserID> _usersToAdd = null;

        protected override void Awake()
        {
            base.Awake();

            obc_CustomPermissions.OnShowingPage += PreparePage;
            obc_CustomPermissions.OnPopulateTile += PopulateTile;

            btn_addUser.onClick.AddListener(GotAddUser);
            spn_DefaultPermission.OnChanged += GotDefaultChanged;
        }

        protected override void OnDestroy()
        {
            obc_CustomPermissions.OnShowingPage -= PreparePage;
            obc_CustomPermissions.OnPopulateTile -= PopulateTile;

            spn_DefaultPermission.OnChanged -= GotDefaultChanged;

            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            IEnumerator Cor()
            {
                World world = G.World.World;

                yield return world.WorldInfo.WaitFor();

                _titlePattern ??= lbl_title.text;

                WorldInfo info = world.WorldInfo;

                lbl_title.text = string.Format(_titlePattern, info.WorldName, (string)info.Author);

                spn_DefaultPermission.value = (int)_accessInfo.DefaultLevel;

                RebuildACLView();

                RebuildPossibleUsers();

                obc_CustomPermissions.ShowPage(0);
            }

            StartCoroutine(Cor());
        }

        private void RebuildPossibleUsers()
        {
            IEnumerable<UserID> q = from entry in G.UserData.GetAllStates()
                                    where entry.friendOffered
                                    select entry.target;

            _usersToAdd = q.ToList();

            if (_usersToAdd.Count > 0)
            {
                spn_addUser.Options = (from entry in _usersToAdd select (string)entry).ToArray();
                spn_addUser.value = 0;
            }
        }

        private void RebuildACLView()
        {
            _accessInfo.UserALs ??= new();

            // Sort names. OrderedDictionary is not available for us.
            List<UserID> list = _accessInfo.UserALs.Keys.ToList();
            list.Sort((x, y) => x.Nickname.CompareTo(y.Nickname));

            _aclEntries = (from entry in list
                           select new ACLEntry()
                           {
                               user = entry,
                               accessLevel = _accessInfo.UserALs[entry],
                           }).ToList();
        }

        private void PopulateTile(int index, GameObject @object)
        {
            ACLEntry entry = _aclEntries[index];

            WorldPermissionListItem item = @object.GetComponent<WorldPermissionListItem>();
            item.Parent = this;
            item.UserID = entry.user;
            item.Permission = entry.accessLevel;
        }

        private void PreparePage(int obj)
        {
            obc_CustomPermissions.UpdateItemCount(_aclEntries.Count);
        }

        private void GotAddUser()
        {
            UserID newUser = _usersToAdd[spn_addUser.value];

            _accessInfo.UserALs[newUser] = WorldAccessInfoLevel.Nothing;

            RebuildACLView();

            obc_CustomPermissions.ShowPage(obc_CustomPermissions.CurrentPage);
        }

        public void GotACLEntryChanged(UserID userID, WorldAccessInfoLevel? newLevel)
        {
            if (newLevel == null)
            {
                // Entry to remove
                _accessInfo.UserALs.Remove(userID);
                RebuildACLView();

                // If applicable, put user to the candidates to re-add
                RebuildPossibleUsers();
                obc_CustomPermissions.ShowPage(obc_CustomPermissions.CurrentPage);
                return;
            }
            else
            {
                // Entry to change
                _accessInfo.UserALs[userID] = newLevel.Value;

                // ... maybe just RebuildACL() ?
                ACLEntry entry = _aclEntries.Find(e => e.user == userID);
                if (entry != null) entry.accessLevel = newLevel.Value;
            }
        }

        private void GotDefaultChanged(int arg1, bool arg2)
            => _accessInfo.DefaultLevel = (WorldAccessInfoLevel)spn_DefaultPermission.value;
    }
}