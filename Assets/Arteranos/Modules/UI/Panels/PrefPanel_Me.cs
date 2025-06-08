using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;
using Arteranos.Core;
using System.Linq;
using System.Collections;
using Ipfs.Unity;
using System.IO;
using Ipfs;

namespace Arteranos.UI
{
    public class PrefPanel_Me : UIBehaviour
    {
        [SerializeField] private Spinner spn_OnlineStatus = null;
        [SerializeField] private TMP_InputField txt_Nickname = null;
        [SerializeField] private IconSelectorBar bar_IconSelector = null;
        [SerializeField] private TMP_Text tro_UserID = null;
        [SerializeField] private NumberedSlider sldn_AvatarHeight = null;
        [SerializeField] private Button btn_CreateAvatar = null;
        [SerializeField] private Button btn_AvatarGallery = null;

        private readonly Dictionary<string, Visibility> statusNames = new();

        private Client cs = null;
        private bool dirty = false;

        protected override void Awake()
        {
            base.Awake();

            foreach(Visibility v in Enum.GetValues(typeof(Visibility)))
            {
                string name = Utils.GetEnumDescription(v);
                if(name != null)
                    statusNames.Add(name, v);
            }

            spn_OnlineStatus.Options = statusNames.Keys.ToArray();

            spn_OnlineStatus.OnChanged += OnVisibilityChanged;
            txt_Nickname.onValueChanged.AddListener((string current) => dirty = true);
            sldn_AvatarHeight.OnValueChanged += (float height) => dirty = true;

            btn_CreateAvatar.onClick.AddListener(() => ActionRegistry.Call("addAvatar"));
            btn_AvatarGallery.onClick.AddListener(() => ActionRegistry.Call("avatarGallery"));

            bar_IconSelector.OnIconChanged += Bar_IconSelector_OnIconChanged;
        }

        private void Bar_IconSelector_OnIconChanged(byte[] obj)
        {
            IEnumerator UploadIcon(byte[] data)
            {
                using MemoryStream ms = new(obj);
                ms.Position = 0;
                yield return Asyncs.Async2Coroutine(() => G.IPFSService.AddStream(ms), _fsn => cs.UserIDJSON.Icon = _fsn.Id);

                dirty = true;
            }

            StartCoroutine(UploadIcon(obj));
        }

        // Using OnEnable() instead Start() because of the need to update the UserID from
        // outside means, like the Login panel.
        protected override void OnEnable()
        {
            base.OnEnable();

            cs = G.Client;

            UIDRepresentation fpmodeSet = cs.UserPrivacy.UIDRepresentation;

            string fpmode = Utils.GetUIDFPEncoding(fpmodeSet);

            spn_OnlineStatus.value = Array.IndexOf(spn_OnlineStatus.Options, Utils.GetEnumDescription(cs.UserPrivacy.Visibility));
            txt_Nickname.text = (string) cs.UserIDJSON;

            tro_UserID.text = cs.GetFingerprint(fpmode);
            sldn_AvatarHeight.value = cs.AvatarHeight;

            bar_IconSelector.IconPath = (Cid) cs.UserIDJSON;

            // Reset the state as it's the initial state, not the blank slate.
            dirty = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            cs.AvatarHeight = sldn_AvatarHeight.value;
            cs.UserIDJSON.Nickname = txt_Nickname.text;

            // Might be to disabled before it's really started, so cs may be null yet.
            if(dirty) cs?.Save();
            dirty = false;
        }

        private void OnVisibilityChanged(int old, bool current)
        {
            string vname = spn_OnlineStatus.Options[spn_OnlineStatus.value];

            if(statusNames.TryGetValue(vname, out Visibility v))
            {
                cs.UserPrivacy.Visibility = v;
                cs.PingUserPrivacyChanged();
            }

            dirty = true;
        }
    }
}
