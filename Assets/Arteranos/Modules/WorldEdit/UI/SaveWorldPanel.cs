/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine.UI;
using TMPro;
using System;
using Arteranos.Core;
using Arteranos.Services;
using System.Threading.Tasks;
using Ipfs;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using System.Threading;
using System.Collections;
using Ipfs.Unity;
using UnityEngine;
using Arteranos.Core.Managed;
using System.Text.RegularExpressions;

namespace Arteranos.WorldEdit
{
    public class SaveWorldPanel : ActionPage
    {
        public Button btn_ReturnToList;
        public TextMeshProUGUI lbl_Author;
        public TextMeshProUGUI lbl_Template;
        public TMP_InputField txt_WorldName;
        public TMP_InputField txt_WorldDescription;

        public Toggle chk_Violence;
        public Toggle chk_Nudity;
        public Toggle chk_Suggestive;
        public Toggle chk_ExViolence;
        public Toggle chk_ExNudity;

        public Button btn_SaveAsZip;
        public Button btn_SaveInGallery;

        public GameObject bp_ScreenshotCamera;

        private string templatePattern;
        private string worldTemplateCid;

        private GameObject ScreenshotCamera;
        private bool needUpdateTag = false;

        private UserID WorldAuthor;
        private WorldAccessInfo WorldAccessInfo;

        protected override void Awake()
        {
            base.Awake();

            templatePattern = lbl_Template.text;

            btn_ReturnToList.onClick.AddListener(GotRTLClick);
            btn_SaveAsZip.onClick.AddListener(GotSaveAsZipClick);
            btn_SaveInGallery.onClick.AddListener(GotSaveInGalleryClick);

            txt_WorldName.onValueChanged.AddListener(GotWorldNameChange);
            txt_WorldDescription.onValueChanged.AddListener(GotWorldDescriptionChange);

            chk_Violence.onValueChanged.AddListener(
                b => GotCWChanged(b, ref G.WorldEditorData.ContentWarning.Violence));
            chk_Nudity.onValueChanged.AddListener(
                b => GotCWChanged(b, ref G.WorldEditorData.ContentWarning.Nudity));
            chk_Suggestive.onValueChanged.AddListener(
                b => GotCWChanged(b, ref G.WorldEditorData.ContentWarning.Suggestive));
            chk_ExViolence.onValueChanged.AddListener(
                b => GotCWChanged(b, ref G.WorldEditorData.ContentWarning.ExcessiveViolence));
            chk_ExNudity.onValueChanged.AddListener(
                b => GotCWChanged(b, ref G.WorldEditorData.ContentWarning.ExplicitNudes));


            G.NetworkStatus.OnNetworkStatusChanged += GotNetworkStatusChange;
        }

        private void GotCWChanged(bool b, ref bool? cwItem) => cwItem = b;

        protected override void Start()
        {
            base.Start();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            G.NetworkStatus.OnNetworkStatusChanged -= GotNetworkStatusChange;
        }

        private void GotWorldNameChange(string name)
        {
            G.WorldEditorData.WorldName = name;
            needUpdateTag = false;
        }

        private void GotWorldDescriptionChange(string description) => G.WorldEditorData.WorldDescription = description;

        private void GotNetworkStatusChange(ConnectivityLevel level1, OnlineLevel level2) 
            => RefreshContentWarning();

        protected override void OnEnable()
        {
            base.OnEnable();

            IEnumerator Cor()
            {
                if (!ScreenshotCamera) ScreenshotCamera = Instantiate(bp_ScreenshotCamera);

                string worldName = G.WorldEditorData.WorldName;

                txt_WorldName.text = worldName;
                txt_WorldDescription.text = G.WorldEditorData.WorldDescription;

                //WorldDownloader's info may be outdated if we fell back to offline.
                if (G.World.World == null)
                {
                    lbl_Author.text = G.Client.MeUserID;
                    worldTemplateCid = null;
                    lbl_Template.text = "None";
                }
                else
                {
                    World World = G.World.World;

                    yield return World.WorldInfo.WaitFor();                    
                    yield return World.TemplateInfo.WaitFor();

                    WorldInfo templateInfo = World.TemplateInfo;
                    WorldInfo worldInfo = World.WorldInfo;

                    // Retain the world authorship
                    WorldAuthor = worldInfo.Author;
                    WorldAccessInfo accessInfo = worldInfo.AccessInfo;

                    worldTemplateCid = templateInfo.WorldCid;
                    lbl_Author.text = worldInfo.Author;

                    lbl_Template.text = string.Format(templatePattern,
                        templateInfo.WorldCid[^12..],
                        templateInfo.WorldName);
                }

                // As long as the user changes the name, we need the "updated" tag.
                needUpdateTag = true;

                EnableSaveButtons();

                RefreshContentWarning();
            }

            StartCoroutine(Cor());
        }

        protected override void OnDisable()
        {
            if (ScreenshotCamera) Destroy(ScreenshotCamera);
            ScreenshotCamera = null;

            base.OnDisable();
        }

        private void EnableSaveButtons()
        {
            // World needs templates....
            btn_SaveAsZip.interactable = !string.IsNullOrEmpty(worldTemplateCid);
            btn_SaveInGallery.interactable = !string.IsNullOrEmpty(worldTemplateCid);
        }

        private void RefreshContentWarning()
        {
            static void PresetPermission(Toggle tg, bool? perm, ref bool? cw)
            {
                if (perm == null) // Server says, don't care
                {
                    tg.interactable = true;
                    tg.isOn = cw ?? false;
                }
                else if (perm == false) // Server forbids the content
                {
                    tg.interactable = false;
                    tg.isOn = false;
                    cw = false;
                }
                else if (perm == true) // Server allows the content, maybe even likely in use
                {
                    tg.interactable = true;
                    tg.isOn = cw ?? true;
                }
            }

            ServerPermissions p = G.NetworkStatus.GetOnlineLevel() == OnlineLevel.Offline
                ? new()
                {
                    Violence = null,
                    Nudity = null,
                    Suggestive = null,
                    ExcessiveViolence = null,
                    ExplicitNudes = null,
                }
                : SettingsManager.ActiveServerData.Permissions;

            ServerPermissions cw = G.WorldEditorData.ContentWarning;

            // Restrict the permission settings depending on the active server.
            // Like, disallowing to build XXX content on a PG-13 server.
            // If the world builder wants to, he'd have to switch servers.
            // Or set up his own.
            PresetPermission(chk_Violence, p.Violence, ref cw.Violence);
            PresetPermission(chk_Nudity, p.Nudity, ref cw.Nudity);
            PresetPermission(chk_Suggestive, p.Suggestive, ref cw.Suggestive);
            PresetPermission(chk_ExViolence, p.ExcessiveViolence, ref cw.ExcessiveViolence);
            PresetPermission(chk_ExNudity, p.ExplicitNudes, ref cw.ExplicitNudes);
        }

        private void GotSaveInGalleryClick()
        {
            IEnumerator Cor()
            {
                ScreenshotCamera.TryGetComponent(out Camera cam);

                using MemoryStream ms = new();
                yield return Utils.TakePhoto(cam, ms);

                WorldDecoration decor = AssembleWorldDecoration();

                IFileSystemNode fsn = null;

                yield return Asyncs.Async2Coroutine(
                    () => AssembleWorldDirectory(worldTemplateCid, decor, ms.ToArray()),
                    result => fsn = result);


                // Pin and enter as a favourite
                World World = fsn.Id;
                World.Favourite();

                Debug.Log($"Full world CID: {fsn.Id}");

                // And, re-enable the button.
                EnableSaveButtons();
            }

            // Disable the button for the duration of the saving process
            btn_SaveInGallery.interactable = false;

            StartCoroutine(Cor());
        }

        private void GotSaveAsZipClick()
        {
            throw new NotImplementedException();
        }

        private WorldDecoration AssembleWorldDecoration()
        {
            string worldName = G.WorldEditorData.WorldName;

            if(needUpdateTag)
            {
                //dateTime.ToString("u")
                string updatedPattern = @" \(updated ....-..-.. ..:..:..Z\)$";

                // Remove existing tag
                worldName = Regex.Replace(worldName, updatedPattern, "");

                string nowStr = DateTime.UtcNow.ToString("u");

                worldName = $"{worldName} (updated {nowStr})";
            }

            // TODO Move WorldAccessInfo holding to WorldEditorData
            // Null WAI means Public Domain, like templates. Building world up from a template means taking ownership.
            if(WorldAccessInfo == null)
            {
                WorldAuthor = G.Client.MeUserID;
                WorldAccessInfo = WorldAccessInfo.Create(WorldAuthor);
            }

            WorldInfo wi = new()
            {
                Author = WorldAuthor,
                ContentRating = G.WorldEditorData.ContentWarning,
                Created = DateTime.UtcNow,
                WorldName = worldName,
                WorldDescription = G.WorldEditorData.WorldDescription,
                WorldCid = null, // Cannot create a self-reference, delay it to the WorldDownloader
                AccessInfo = WorldAccessInfo,
            };

            WorldDecoration wd = new()
            { 
                info = wi
            };

            wd.TakeSnapshot();

            return wd;
        }

        private async Task<IFileSystemNode> AssembleReferencesDirectory(WorldDecoration decor, CancellationToken cancel = default)
        {
            Debug.Log($"Starting gathering asset references");
            // Gather all of the references
            HashSet<AssetReference> references = new();

            foreach(WorldObject objects in decor.objects)
                references.UnionWith(objects.GetAssetReferences(true));

            // Group the gathered references
            Dictionary<string, List<string>> dict = new();
            foreach(AssetReference reference in references)
            {
                if (!dict.ContainsKey(reference.type))
                    dict[reference.type] = new();

                dict[reference.type].Add(reference.cid);
            }

            Debug.Log($"Found {dict.Count} reference types");

            // Build the entry list with the subdirectories
            List<IFileSystemLink> dirEntries = new();
            foreach(KeyValuePair<string, List<string>> entry in dict)
            {
                Debug.Log($"Creating {entry.Key} entry for {entry.Value.Count} entries");

                List<IFileSystemLink> subDirEntries = new();
                foreach(string subEntry in entry.Value)
                {
                    Debug.Log($"  Looking for entry {subEntry}");
                    IFileSystemNode subfsn = await G.IPFSService.ListFile(subEntry, cancel: cancel);
                    subDirEntries.Add(subfsn.ToLink(subEntry));
                }

                IFileSystemNode subdirfsn = await G.IPFSService.CreateDirectory(subDirEntries, cancel: cancel);
                dirEntries.Add(subdirfsn.ToLink(entry.Key));
                Debug.Log($"Created {entry.Key} entry for {entry.Value.Count} entries");
            }

            Debug.Log($"Finished creating reference directory");
            IFileSystemNode dirfsn = await G.IPFSService.CreateDirectory(dirEntries, cancel: cancel);
            return dirfsn;
        }

        private async Task<IFileSystemNode> AssembleWorldDirectory(
            Cid template, 
            WorldDecoration decor, 
            byte[] ScreenshotPNG,
            CancellationToken cancel = default)
        {
            List<IFileSystemLink> rootEntries = new();

            // Add the decoration as a file
            using MemoryStream ms = new();
            Serializer.Serialize(ms, decor);
            ms.Position = 0;
            IFileSystemNode fsnDecor = await G.IPFSService.AddStream(ms, "Decoration", cancel: cancel).ConfigureAwait(false);
            rootEntries.Add(fsnDecor.ToLink());

            // Add the template as a hard link [to a directory]
            IFileSystemNode fsnTemplate = await G.IPFSService.ListFile(template, cancel: cancel).ConfigureAwait(false);
            rootEntries.Add(fsnTemplate.ToLink("Template"));

            // Add the references as a directory tree with hard links [to files}
            IFileSystemNode fsnReferences = await AssembleReferencesDirectory(decor, cancel);
            rootEntries.Add(fsnReferences.ToLink("References"));

            using MemoryStream stream = new(ScreenshotPNG);
            IFileSystemNode fsnSreenshot = await G.IPFSService.AddStream(stream, "Screenshot.png", cancel: cancel).ConfigureAwait(false);
            rootEntries.Add(fsnSreenshot.ToLink());

            // Bundle them all together.
            return await G.IPFSService.CreateDirectory(rootEntries, cancel: cancel).ConfigureAwait(false);
        }

        private void GotRTLClick() => BackOut(null);
    }
}
