/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Arteranos.Core;

namespace Arteranos.UI
{
    public abstract class UserPanelBase : UIBehaviour
    {
        public RectTransform lvc_UserList;

        public virtual bool LocationVisible => false;

        protected Client cs = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            cs = G.Client;

            if(cs == null) return;

            foreach(KeyValuePair<UserID, UserSocialEntryJSON> entry in GetSocialListTab())
                UserListItem.New(lvc_UserList.transform, entry.Key, LocationVisible);
        }

        protected override void OnDisable()
        {
            List<GameObject> list = new();

            for(int i = 0, c = lvc_UserList.transform.childCount;i < c; ++i)
                list.Add(lvc_UserList.transform.GetChild(i).gameObject);

            foreach(GameObject item in list)
                Destroy(item);

            base.OnDisable();
        }

        public abstract IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialListTab();
    }
}
