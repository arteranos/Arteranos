/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.InputSystem;
using Arteranos.Core;
using AssetBundle = Arteranos.Core.Managed.AssetBundle;
using Arteranos.WorldEdit.Components;


namespace Arteranos.WorldEdit
{
    public class WorldEditorData : MonoBehaviour, IWorldEditorData
    {
        struct UndoBuffer
        {
            public string hash;
            public List<byte[]> SerializedWorldObjects;
        }

        public WBP Blueprints;

        public InputActionHandler KMWorldEditorActions;
        public InputActionHandler KMWorldEditorModeSelect;
        public InputActionHandler KMWorldEditorValueSelect;

        public event Action<IWorldChange> OnWorldChanged;
        public event Action<bool> OnEditorModeChanged;

        public GameObject FocusedWorldObject
        {
            get => currentWorldObject;
            set
            {
                currentWorldObject = value;
                PlaceGizmo();
            }
        }

        // Movement and rotation constraints
        public bool LockXAxis { get; set; } = false;
        public bool LockYAxis { get; set; } = false;
        public bool LockZAxis { get; set; } = false;

        // Are we in the edit mode at all?
        public bool IsInEditMode
        {
            get => isInEditMode;
            set
            {
                bool old = isInEditMode;
                isInEditMode = value;
                if (old != value) NotifyEditorModeChanged();
                PlaceGizmo();
            }
        }

        // Are we using a global or the local coordinate system?
        public bool UsingGlobal 
        { 
            get => usingGlobal; 
            set => usingGlobal = value; 
        }

        // The name
        public string WorldName { get; set; }

        // The description
        public string WorldDescription { get; set; }

        // Content warnings
        public ServerPermissions ContentWarning { get; set; }

        // World Access Info while editing
        public WorldAccessInfo WorldAccessInfo { get; set; }

        // Paste Buffer
        public byte[] PasteBuffer { get; set; }


        private readonly Dictionary<IWorldObjectAsset, GameObject> AssetBlueprints = new();

        private bool isInEditMode = false;
        private bool usingGlobal = false;
        private GameObject currentWorldObject;
        private GameObject gizmoObject;

        // ---------------------------------------------------------------
        #region Start/Stop
        public void Awake()
        {
            WBP.I = Blueprints;
            G.WorldEditorData = this;

            if (KMWorldEditorActions == null) return;

            UserID meUserID = G.Client.MeUserID;

            IWorldDecoration id = G.World.World?.DecorationContent.Result;
            WorldInfo info = id?.Info;

            if (info == null)
            {
                ContentWarning = new()
                {
                    Violence = false,
                    Suggestive = false,
                    Nudity = false,
                    ExcessiveViolence = false,
                    ExplicitNudes = false,
                };

                WorldName = $"{(string)meUserID}'s unnamed world";
                WorldDescription = "";
            }
            else
            {
                ContentWarning = info.ContentRating.Clone() as ServerPermissions;
                WorldName = info.WorldName;
                WorldDescription = info.WorldDescription;
                WorldAccessInfo = info.AccessInfo;
            }

            // Bare template or old style public domain - assume ownership
            WorldAccessInfo ??= WorldAccessInfo.Create(meUserID);

            // React only to the up flank
            KMWorldEditorModeSelect.PerformCallback = DoModeSelect;
            KMWorldEditorValueSelect.PerformCallback = DoValueSelect;

            // React to the hi state
            KMWorldEditorActions.PerformCallback = DoTransformAction;
            KMWorldEditorActions.CancelCallback = StopTransformAction;
        }

        public void OnDestroy()
        {
            ClearBlueprints();

            ClearKitAssetBundles();
        }

        public void OnEnable()
        {
            if (KMWorldEditorActions == null) return;

            KMWorldEditorModeSelect.BindAction();
            KMWorldEditorValueSelect.BindAction();
            KMWorldEditorActions.BindAction();
        }

        public void OnDisable()
        {
            if (KMWorldEditorActions == null) return;

            KMWorldEditorModeSelect.UnbindAction();
            KMWorldEditorValueSelect.UnbindAction();
            KMWorldEditorActions.UnbindAction();
        }

        public void Update()
        {
            HandleKMObjectEdit();
        }

        public void NotifyWorldChanged(IWorldChange worldChange)
            => OnWorldChanged?.Invoke(worldChange);

        public void NotifyEditorModeChanged()
            => OnEditorModeChanged?.Invoke(IsInEditMode);
        #endregion
        // ---------------------------------------------------------------
        #region Undo/Redo

        private List<UndoBuffer> undoStack = new();
        private int undoCount = 0;

        public IEnumerator RecallUndoState(string hash)
        {
            static IEnumerator Cor(List<byte[]> serialized)
            {
                Transform t = FindObjectByPath(null);

                // First, try to remember the previous gameobjects.
                List<Transform> old = new();
                for (int i = 0; i < t.childCount; i++) old.Add(t.GetChild(i));

                // And, let them fall under the radar.
                t.DetachChildren();

                // Rebuild the state from the undo buffer
                foreach (byte[] ser in serialized)
                {
                    using MemoryStream ms = new(ser);
                    WorldObject wo = WorldObject.Deserialize(ms);
                    yield return wo.Instantiate(t);
                }

                // And now, destroy the remembered gameobjects.
                foreach (Transform item in old)
                    Destroy(item.gameObject);
            }

            UndoBuffer? found = null;
            foreach(var item in undoStack)
                if(item.hash == hash)
                {
                    found = item; break;
                }

            // Maybe it is a latecomer who hasn't the undo state that far back, or
            // he's desynced.
            if (found == null) yield break;

            yield return Cor(found.Value.SerializedWorldObjects);
        }

        public void BuilderRequestsUndo()
        {
            // Before navigating back and forth through the undo stack, we need to add the _current_ statw
            // on top of the stack.
            if(undoCount == 0)
            {
                AddUndoEntry();
                undoCount = 1;
            }

            if (undoCount >= undoStack.Count) return;
            undoCount++;

            EmitUndoRedo();
        }

        public void BuilderRequestedRedo()
        {
            if (undoCount <= 1) return;
            undoCount--;

            EmitUndoRedo();
        }

        #endregion
        // ---------------------------------------------------------------
        #region Cut and Paste

        public void SaveToPasteBuffer(GameObject go)
        {
            WorldObjectPaste paste = new() { WorldObject = go.MakeWorldObject(true) };

            // Serialize to prevent cross-referencing
            using MemoryStream ms = new();
            paste.Serialize(ms);
            PasteBuffer = ms.ToArray();            
        }

        public void RecallFromPasteBuffer(Transform root)
        {
            static void RerollIDs(WorldObject wo)
            {
                wo.id = Guid.NewGuid();
                if (wo.children != null)
                    foreach (WorldObject child in wo.children)
                        RerollIDs(child);
            }

            if (PasteBuffer == null) return;

            WorldChange ch = WorldChange.Deserialize(new MemoryStream(PasteBuffer));

            WorldObjectPaste paste = ch as WorldObjectPaste;

            RerollIDs(paste.WorldObject);

            paste.SetPathFromThere(root);

            paste.EmitToServer();
        }

        #endregion
        // ---------------------------------------------------------------
        #region World Change Application
        public void DoApply(IWorldChange worldChange)
        {
            IEnumerator Cor()
            {
                if (worldChange is not WorldRollbackRequest)
                    AddUndoEntry();

                // TODO Need to add a notify when the world is about to change, e.g. remembering
                // the object list's current root.

                // Make the changes real.
                // Rollback request will recall an arbitrary snapshot from the stack.
                yield return worldChange.Apply();

                NotifyWorldChanged(worldChange);
            }

            StartCoroutine(Cor());
        }

        public void DoApply(Stream stream)
        {
            IWorldChange worldChange = WorldChange.Deserialize(stream);
            DoApply(worldChange);
        }

        #endregion
        // ---------------------------------------------------------------
        #region Snapshot and recall
        public IWorldDecoration TakeSnapshot()
        {
            WorldDecoration worldDecoration = new() 
            { 
                info = null, 
                objects = new() 
            };
            worldDecoration.TakeSnapshot();
            return worldDecoration;
        }

        public IEnumerator BuildWorld(IWorldDecoration worldDecoration)
            => worldDecoration.BuildWorld();

        public IWorldDecoration DeserializeWD(Stream stream)
            => WorldDecoration.Deserialize(stream);

        #endregion
        // ---------------------------------------------------------------
        #region Object blueprint management

        public void ClearBlueprints()
        {
            foreach(var bp in AssetBlueprints)
                Destroy(bp.Value);

            AssetBlueprints.Clear();
        }

        public bool TryGetBlueprint(IWorldObjectAsset woa, out GameObject gameObject) 
            => AssetBlueprints.TryGetValue(woa, out gameObject);

        public void AddBlueprint(IWorldObjectAsset woa, GameObject gameObject) 
            => AssetBlueprints.Add(woa, gameObject);

        #endregion
        // ---------------------------------------------------------------
        #region Runtime Object Spawn

        public bool CreateSpawnObject(CTSObjectSpawn spawn, Transform shellObject, Action<GameObject> callback)
        {
            Transform spawnerT = null;

            IEnumerator Cor()
            {
                // More complex objects take more time to be constructed.
                WorldObject blueprint = null;
                yield return new WaitUntil(() =>
                {
                    // The spawner may have not yet completed, wait a turn.
                    if (spawnerT.childCount <= spawn.Pick) return false;

                    blueprint = spawnerT.GetChild(spawn.Pick).MakeWorldObject();
                    return blueprint != null;
                });

                // One way to do deep copying, especially complex structures? Serializing!
                using MemoryStream ms = new();
                blueprint.Serialize(ms);
                ms.Position = 0;
                WorldObject wo = WorldObject.Deserialize(ms);

                GameObject spawnedWO = null;
                yield return wo.Instantiate(shellObject, _res => spawnedWO = _res);
                spawnedWO.TryGetComponent(out WorldObjectComponent woc);
                woc.EnclosingObject = shellObject.gameObject;
                woc.ExpirationTime = DateTime.UtcNow + TimeSpan.FromSeconds(spawn.Lifetime);
                woc.DataObject = spawnerT;

                if (woc.TryGetWOC(out WOCRigidBody rb)) rb.InitForce = spawn.Force;

                spawnedWO.name = $"{spawnedWO.name} (with {woc.EnclosingObject.name})";

                callback?.Invoke(spawnedWO);
            }

            try
            {
                spawnerT = FindObjectByPath(spawn.SpawnerPath);
                StartCoroutine(Cor());
                return true;
            }
            catch 
            {
                // Multiplayer network primed the spawned object, but the world is not
                // yet created. Report back to schedule delays.
                return false;
            }
            // NOTREACHED
        }
        #endregion
        // ---------------------------------------------------------------
        #region Object Editor

        private readonly List<float> TranslationValues = new() { 0.01f, 0.1f, 1f, 10f };
        private readonly List<float> RotationValues = new() { 1f, 5f, 10f, 22.5f, 45f, 90f, 180f };
        private readonly List<float> ScaleValues = new() { 0.01f, 0.1f, 1f, 10f };
        private readonly List<WorldEditMode> EditModes = new()
        {
            WorldEditMode.Translation,
            WorldEditMode.Rotation,
            WorldEditMode.Scale,
        };

        private int TranslationValueIndex;
        private int RotationValueIndex;
        private int ScaleValueIndex;
        private int EditModeIndex;

        private Vector3 TransformAction = Vector3.zero;

        private void DoValueSelect(InputAction.CallbackContext obj) 
            => CycleTRSValue(obj.ReadValue<float>() > 0 ? 1 : -1);

        private void DoModeSelect(InputAction.CallbackContext obj) 
            => CycleMode(obj.ReadValue<float>() > 0 ? 1 : -1);

        private void DoTransformAction(InputAction.CallbackContext obj) 
            => TransformAction = obj.ReadValue<Vector3>();

        private void StopTransformAction(InputAction.CallbackContext obj) 
            => TransformAction = Vector3.zero;

        public T CycleValue<T>(List<T> values, int direction, ref int index)
        {
            index = (index + values.Count + direction) % values.Count;
            return values[index];
        }

        public void CycleTRSValue(int direction)
        {
            switch (EditModes[EditModeIndex])
            {
                case WorldEditMode.Translation:
                    CycleValue(TranslationValues, direction, ref TranslationValueIndex); break;
                case WorldEditMode.Rotation:
                    CycleValue(RotationValues, direction, ref RotationValueIndex); break;
                case WorldEditMode.Scale:
                    CycleValue(ScaleValues, direction, ref ScaleValueIndex); break;
                default:
                    throw new NotImplementedException();
            }
        }

        public void CycleMode(int direction)
        {
            CycleValue(EditModes, direction, ref EditModeIndex);
            PlaceGizmo();
        }

        private void HandleKMObjectEdit()
        {
            if (!FocusedWorldObject || !isInEditMode) return;

            if (TransformAction == Vector3.zero) return;

            FocusedWorldObject.TryGetComponent(out WorldObjectComponent woc);
            if (woc.IsLocked) return;

            Vector3 v = TransformAction * Time.deltaTime;

            woc.TryGetWOC(out WOCTransform woct);

            Vector3 p = woct.position;
            Vector3 r = woct.rotation;
            Vector3 s = woct.scale;

            Quaternion parentRotation = currentWorldObject.transform.parent.rotation;

            switch (EditModes[EditModeIndex])
            {
                case WorldEditMode.Translation:
                    if(usingGlobal)
                        p += Quaternion.Inverse(parentRotation) * (v * TranslationValues[TranslationValueIndex]);
                    else
                        p += Quaternion.Euler(r) * (v * TranslationValues[TranslationValueIndex]);
                    break;
                case WorldEditMode.Rotation:
                    if(usingGlobal)
                    {
                        Vector3 worldRotAngles = Quaternion.Inverse(parentRotation) * (v * RotationValues[RotationValueIndex]);
                        r = (Quaternion.Euler(r) * Quaternion.Euler(worldRotAngles)).eulerAngles;
                    }
                    else
                        r = (Quaternion.Euler(r) * Quaternion.Euler(v * RotationValues[RotationValueIndex])).eulerAngles;
                    break;
                case WorldEditMode.Scale:
                    s += v * ScaleValues[ScaleValueIndex];
                    break;
                default: 
                    throw new NotImplementedException();
            }

            woct.SetState(p, r, s);
            FocusedWorldObject.MakePatch(false).EmitToServer();
        }

        private void PlaceGizmo()
        {
            if (gizmoObject)
            {
                Destroy(gizmoObject);
                gizmoObject = null;
            }

            if (!IsInEditMode || !FocusedWorldObject) return;
            FocusedWorldObject.TryGetComponent(out WorldObjectComponent woc);

            if (woc.IsLocked) return;

            GameObject gobp = EditModes[EditModeIndex] switch
            {
                WorldEditMode.Translation => WBP.I.Objects.TranslationGizmo,
                WorldEditMode.Rotation => WBP.I.Objects.RotationGizmo,
                WorldEditMode.Scale => WBP.I.Objects.ScalingGizmo,
                _ => throw new NotImplementedException()
            };

            gizmoObject = Instantiate(gobp, FocusedWorldObject.transform);
        }


        #endregion
        // ---------------------------------------------------------------
        #region Kit Asset Handling

        private readonly Dictionary<string, Kit> KitAssetBundles = new();

        public void ClearKitAssetBundles()
        {
            // Explicitly dispose in case lingering kits collide with the same ones
            // during reuse
            foreach(Kit item in KitAssetBundles.Values)
                item.Dispose();

            KitAssetBundles.Clear();
        }

        public AsyncLazy<AssetBundle> LoadKitAssetBundle(string path)
        {
            if (!KitAssetBundles.ContainsKey(path))
                KitAssetBundles[path] = new(path);

            return KitAssetBundles[path].KitContent;
        }

        #endregion
        // ---------------------------------------------------------------
        #region Interactables dispatching

        private bool AffectedComponent<T>(GameObject go, out T wocc)  where T : class
        {
            wocc = null;

            if(!go) return false;

            return go.TryGetComponent(out WorldObjectComponent woc) && woc.TryGetWOC(out wocc);
        }

        public bool GotWorldObjectClicked(GameObject go)
        {
            if(AffectedComponent(go, out IClickable cl))
            {
                cl.ServerGotClicked();
                return true;
            }

            return false;
        }

        public bool GotWorldObjectClicked(List<Guid> go)
            => GotWorldObjectClicked(FindObjectByPath(go).gameObject);

        public bool GotWorldObjectGrabbed(GameObject go)
        {
            if(AffectedComponent(go,out IRigidBody body))
            {
                body.ServerGotGrabbed();
                return true;
            }

            return false;
        }

        public bool GotWorldObjectGrabbed(List<Guid> go)
            => GotWorldObjectGrabbed(FindObjectByPath(go).gameObject);

        public bool GotWorldObjectReleased(GameObject go, Vector3 detachVelocity, Vector3 detachAngularVelocity)
        {
            if (AffectedComponent(go, out IRigidBody body))
            {
                body.ServerGotReleased(detachVelocity, detachAngularVelocity);
                return true;
            }

            return false;
        }

        public bool GotWorldObjectReleased(List<Guid> go, Vector3 detachVelocity, Vector3 detachAngularVelocity)
            => GotWorldObjectReleased(FindObjectByPath(go).gameObject, detachVelocity, detachAngularVelocity);

        public bool GotWorldObjectHeld(GameObject go, Vector3 position, Quaternion rotation)
        {
            if (AffectedComponent(go, out IRigidBody body))
            {
                body.ServerGotObjectHeld(position, rotation);
                return true;
            }

            return false;
        }

        public bool GotWorldObjectHeld(List<Guid> go, Vector3 position, Quaternion rotation)
            => GotWorldObjectHeld(FindObjectByPath(go).gameObject, position, rotation);

        #endregion
        // ---------------------------------------------------------------
        #region Internal
        private IEnumerable<WorldObject> MakeSnapshot()
        {
            Transform t = FindObjectByPath(null);
            for (int i = 0; i < t.childCount; i++)
            {
                WorldObject wo = t.GetChild(i).MakeWorldObject();
                if (wo != null) yield return wo;
            }
        }

        private void AddUndoEntry()
        {
            UndoBuffer buffer = new()
            {
                SerializedWorldObjects = new()
            };


            // Cut off the undo stack, discarding the more recent snapshots
            if (undoCount != 0)
            {
                undoStack = undoStack.GetRange(0, undoStack.Count - undoCount);
                undoCount = 0;
            }

            // Has to be reproducible across all clients, so no GUID.NewGuid()...
            Hash128 hash = new();

            // And now, take a new snapshot as the most recent one
            foreach (WorldObject wo in MakeSnapshot())
            {
                using MemoryStream ms = new();
                wo.Serialize(ms);
                byte[] item = ms.ToArray();
                hash.Append(item);
                buffer.SerializedWorldObjects.Add(item);
            }
            buffer.hash = hash.ToString();

            undoStack.Add(buffer);
        }

        private void EmitUndoRedo()
        {
            WorldRollbackRequest request = new()
            {
                // Has to be same across all clients, or they'd be desynced.
                hash = undoStack[^undoCount].hash,
            };
            request.EmitToServer();
        }

        public List<Guid> GetPathFromObject_S(Transform t)
            => GetPathFromObject(t);

        public static List<Guid> GetPathFromObject(Transform t)
        {
            List<Guid> path = new();
            while (t.TryGetComponent(out WorldObjectComponent woc))
            {
                path.Add(woc.Id);
                t = t.parent;
            }
            path.Reverse();
            return path;
        }

        public Transform FindObjectByPath_S(List<Guid> path)
            => FindObjectByPath(path);

        public static Transform FindObjectByPath(List <Guid> path)
        {
            Transform t = SettingsManager.SetupWorldObjectRoot().transform;

            if (path == null) return t;

            foreach (Guid id in path)
            {
                Transform found = null;
                for (int i = 0; i < t.childCount; i++)
                {
                    if (!t.GetChild(i).TryGetComponent(out WorldObjectComponent woc)) continue;

                    if (id == woc.Id)
                    {
                        found = t.GetChild(i);
                        break;
                    }
                }

                if (!found)
                    throw new ArgumentException($"Unknown path element: {id}");

                t = found;
            }

            return t;
        }
        #endregion
    }
}