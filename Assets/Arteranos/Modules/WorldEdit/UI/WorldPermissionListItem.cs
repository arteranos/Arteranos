/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
using Arteranos.UI;
using Arteranos.Core;

namespace Arteranos.WorldEdit
{
    public class WorldPermissionListItem : UIBehaviour
    {
        [SerializeField] private IPFSImage img_Icon;
        [SerializeField] private TMP_Text lbl_Name;
        [SerializeField] private Spinner spn_Permission;
        [SerializeField] private Button btn_Remove;

        public string Icon => img_Icon.Path;
        public UserID UserID
        {
            get => m_UserID;
            set
            {
                m_UserID = value;
                lbl_Name.text = (string) m_UserID;

                // Prevent pulling the rug under yourself
                bool otherone = m_UserID != G.Client.MeUserID;
                spn_Permission.enabled = otherone;
                btn_Remove.interactable = otherone;
            }
        }
        public WorldAccessInfoLevel Permission
        {
            get => (WorldAccessInfoLevel) spn_Permission.value;
            set => spn_Permission.value = (int) value;
        }
        public UI_WorldPermissionsEditor Parent { get; set; }

        private UserID m_UserID = null;

        protected override void Awake()
        {
            base.Awake();

            btn_Remove.onClick.AddListener(GotRemoveClicked);
            spn_Permission.OnChanged += GotValueChanged;
        }

        protected override void OnDestroy()
        {
            spn_Permission.OnChanged -= GotValueChanged;

            base.OnDestroy();
        }

        private void GotValueChanged(int arg1, bool arg2)
        {
            Parent.GotACLEntryChanged(m_UserID, Permission);
        }

        private void GotRemoveClicked()
        {
            Parent.GotACLEntryChanged(m_UserID, null);
        }
    }
}
