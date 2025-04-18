/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AssetBundle = Arteranos.Core.Managed.AssetBundle;

namespace Arteranos.WorldEdit
{
    public enum WorldEditMode
    {
        Translation = 0,
        Rotation,
        Scale
    }

    public interface IWorldEditorData
    {
        bool IsInEditMode { get; set; }
        bool LockXAxis { get; set; }
        bool LockYAxis { get; set; }
        bool LockZAxis { get; set; }
        bool UsingGlobal { get; set; }
        GameObject FocusedWorldObject { get; set; }
        string WorldName { get; set; }
        string WorldDescription { get; set; }
        ServerPermissions ContentWarning { get; set; }
        byte[] PasteBuffer { get; set; }
        WorldAccessInfo WorldAccessInfo { get; set; }

        event Action<bool> OnEditorModeChanged;
        event Action<IWorldChange> OnWorldChanged;

        void AddBlueprint(IWorldObjectAsset woa, GameObject gameObject);
        void BuilderRequestedRedo();
        void BuilderRequestsUndo();
        IEnumerator BuildWorld(IWorldDecoration worldDecoration);
        void ClearBlueprints();
        void ClearKitAssetBundles();
        bool CreateSpawnObject(CTSObjectSpawn spawn, Transform hookObject, Action<GameObject> callback);
        IWorldDecoration DeserializeWD(Stream stream);
        void DoApply(Stream stream);
        void DoApply(IWorldChange worldChange);
        AsyncLazy<AssetBundle> LoadKitAssetBundle(string path);
        void NotifyEditorModeChanged();
        void NotifyWorldChanged(IWorldChange worldChange);
        void RecallFromPasteBuffer(Transform root);
        IEnumerator RecallUndoState(string hash);
        void SaveToPasteBuffer(GameObject go);
        IWorldDecoration TakeSnapshot();
        bool TryGetBlueprint(IWorldObjectAsset woa, out GameObject gameObject);
        public Transform FindObjectByPath_S(List<Guid> path);
        public List<Guid> GetPathFromObject_S(Transform t);
        bool GotWorldObjectClicked(GameObject go);
        bool GotWorldObjectGrabbed(GameObject go);
        bool GotWorldObjectReleased(GameObject go, Vector3 detachVelocity, Vector3 detachAngularVelocity);
        bool GotWorldObjectHeld(GameObject go, Vector3 position, Quaternion rotation);
        bool GotWorldObjectClicked(List<Guid> go);
        bool GotWorldObjectGrabbed(List<Guid> go);
        bool GotWorldObjectReleased(List<Guid> go, Vector3 detachVelocity, Vector3 detachAngularVelocity);
        bool GotWorldObjectHeld(List<Guid> go, Vector3 position, Quaternion rotation);
    }
}