/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using TMPro;
using System;
using Arteranos.UI;
using System.Collections;
using Arteranos.Core;
using Arteranos.Core.Operations;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

namespace Arteranos.WorldEdit
{
    public class Panel_Kit : NewObjectPanel
    {
        public List<Kit> KitEntries { get; private set; } = null;

        public ObjectChooser Chooser;
        public GameObject KitItem;

        protected override void Awake()
        {
            base.Awake();

            Chooser.OnShowingPage += PreparePage;
            Chooser.OnPopulateTile += PopulateTile;
            Chooser.OnAddingItem += GotRequestToAdd;
        }

        protected override void OnDestroy()
        {
            Chooser.OnShowingPage -= PreparePage;
            Chooser.OnPopulateTile -= PopulateTile;
            Chooser.OnAddingItem -= GotRequestToAdd;

            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            KitEntries = (from kitCid in Kit.ListFavourites()
                         select new Kit(kitCid)).ToList();
        }

        protected override void Start()
        {
            base.Start();

            Chooser.ShowPage(0);
        }

        private void PreparePage(int obj)
        {
            Chooser.UpdateItemCount(KitEntries.Count);
        }

        private void PopulateTile(int index, GameObject @object)
        {
            IEnumerator Cor()
            {
                Kit entry = KitEntries[index];
                IPFSImage image = @object.GetComponentInChildren<IPFSImage>();
                TMP_Text text = @object.GetComponentInChildren<TMP_Text>();
                Button button = @object.GetComponentInChildren<Button>();

                yield return entry.KitInfo.WaitFor();
                yield return entry.ScreenshotPNG.WaitFor();

                image.ImageData = entry.ScreenshotPNG;
                text.text = entry.KitInfo.Result.KitName;

                button.onClick.AddListener(() => OnTileClicked(index));
            }

            StartCoroutine(Cor());
        }

        private void GotRequestToAdd(string _)
        {
            ActionRegistry.Call("fileBrowser",
                            new FileBrowserData() { Pattern = @".*\.(tar|zip)" },
                            callback: GotAddingKit);
        }

        private void GotAddingKit(object obj)
        {
            if (obj == null)
            {
                Chooser.Btn_AddItem.interactable = true;
                return;
            }

            string sourceURL = obj as string;

            IEnumerator Cor()
            {
                Chooser.Btn_AddItem.interactable = false;

                (AsyncOperationExecutor<Context> ao, Context co) =
                    AssetUploader.PrepareUploadToIPFS(sourceURL, true); // Kit Archive

                ao.ProgressChanged += (ratio, msg) => Chooser.Lbl_PageCount = $"{msg}";

                AggregateException ex = null;
                yield return ao.ExecuteCoroutine(co, (_status, _) => ex = _status);

                Kit newKit = new(AssetUploader.GetUploadedCid(co));
                newKit.Favourite();

                Chooser.Btn_AddItem.interactable = true;

                KitEntries = (from kitCid in Kit.ListFavourites()
                              select new Kit(kitCid)).ToList();

                Chooser.ShowPage(0);
            }

            StartCoroutine(Cor());
        }
        private void OnTileClicked(int index)
        {
            ActionRegistry.Call(
                "embedded.worldEditor.kitItemChoice",
                data: KitEntries[index],
                KitItem.GetComponent<IActionPage>(),
                callback: GotKitItem
                );
        }

        private void GotKitItem(object obj)
        {
            if (obj == null) return;

            AddingNewObject(obj as WorldObjectInsertion);
        }
    }
}
