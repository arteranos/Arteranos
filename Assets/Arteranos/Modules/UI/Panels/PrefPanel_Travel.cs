/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Arteranos.Core;
using System;

namespace Arteranos.UI
{
    public class PrefPanel_Travel : UIBehaviour
    {
        [SerializeField] private Button btn_WorldGallery = null;
        [SerializeField] private Button btn_ServerGallery = null;
        [SerializeField] private Button btn_SetContent = null;
        [SerializeField] private Toggle chk_AllowCustomTOS = null;

        private Client cs = null;
        private bool dirty = false;

        protected override void Awake()
        {
            base.Awake();

            btn_WorldGallery.onClick.AddListener(() => ActionRegistry.Call("worldPanel"));
            btn_ServerGallery.onClick.AddListener(() => ActionRegistry.Call("serverList"));
            btn_SetContent.onClick.AddListener(() => ActionRegistry.Call(
                "contentFilter",
                G.Client.Permissions,
                callback: r => G.Client?.Save()));
            chk_AllowCustomTOS.onValueChanged.AddListener(OnCustomTOSToggled);
        }

        protected override void Start()
        {
            base.Start();

            cs = G.Client;

            chk_AllowCustomTOS.isOn = cs.AllowCustomTOS;

            // Reset the state as it's the initial state, not the blank slate.
            dirty = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Might be to disabled before it's really started, so cs may be null yet.
            if(dirty) cs?.Save();
            dirty = false;
        }

        private void OnCustomTOSToggled(bool arg0)
        {
            cs.AllowCustomTOS = chk_AllowCustomTOS.isOn;
            dirty = true;
        }
    }
}
