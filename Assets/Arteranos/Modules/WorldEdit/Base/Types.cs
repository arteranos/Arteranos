/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using System.Collections.Generic;
using ProtoBuf;

using Arteranos.Core;
using UnityEngine;
using System.IO;
using System;
using Arteranos.WorldEdit.Components;

namespace Arteranos.WorldEdit
{
    public enum ColliderType
    {
        Intangible = 6,  // (Layer: Intangible) Collides nothing, passable
        Watery = 14,     // (Layer: RigidBody): Collides likewise and solids, passable
        Solid = 0,       // (Layer: Default): Collides likewise and intangibles, stops avatars
    }

    // -------------------------------------------------------------------
    #region World Edit Snapshot and Restore

    [ProtoContract]
    public class WorldDecoration : IWorldDecoration
    {
        [ProtoMember(1)]
        public WorldInfo info;

        [ProtoMember(2)]
        public List<WorldObject> objects;

        [ProtoMember(3)]
        public byte[] signature;

        public WorldInfo Info { get => info; set => info = value; }

        public void Serialize(MemoryStream stream)
        {
            if (info.Author != G.Client.MeUserID)
                throw new InvalidDataException("Cannot serialize: Author mismatch");

            signature = null;
            Serializer.Serialize(stream, this);
            stream.Position = 0;
            Client.Sign(stream.ToArray(), out signature);

            Serializer.Serialize(stream, this);
            stream.Flush();
        }

        public static WorldDecoration Deserialize(Stream stream)
        {
            WorldDecoration wd = Serializer.Deserialize<WorldDecoration>(stream);
            byte[] signature = wd.signature;

            if(signature == null)
            {
                Debug.LogWarning("Decorated world has no signature - old style Public Domain");
            }
            else
            {
                using MemoryStream ms = new();

                wd.signature = null;
                Serializer.Serialize(ms, wd);

                wd.info.Author.SignPublicKey.Verify(ms.ToArray(), signature);

                wd.signature = signature;
            }

            return wd;
        }


        public IEnumerator BuildWorld()
        {
            Transform t = WorldEditorData.FindObjectByPath(null);

            G.WorldEditorData.ClearBlueprints();

            for (int i = 0; i < objects.Count; i++)
                yield return objects[i].Instantiate(t);
        }

        public void TakeSnapshot()
        {
            objects = new();

            Transform t = WorldEditorData.FindObjectByPath(null);
            for (int i = 0; i < t.childCount; i++)
                objects.Add(t.GetChild(i).MakeWorldObject());
        }
    }

    #endregion
    // -------------------------------------------------------------------
    #region World Edit Patch operations

    [ProtoContract]
    public class WorldObjectInsertion : WorldChange
    {
        [ProtoMember(1)]
        public WorldObjectAsset asset;      // see above

        [ProtoMember(2)]
        public string name;

        [ProtoMember(3)]
        public Guid id = Guid.NewGuid();

        [ProtoMember(7)]
        public List<WOCBase> components;  // Additional properties (like teleport marker, ...)

        public override IEnumerator Apply()
        {
            Transform t = FindObjectByPath();

            WorldObject worldObject = new()
            {
                asset = asset,
                name = name,
                id = id,
                components = components
            };

            yield return worldObject.Instantiate(t);
        }
    }

    [ProtoContract]
    public class WorldObjectPaste : WorldChange
    {
        [ProtoMember(1)]
        public WorldObject WorldObject;

        public override IEnumerator Apply()
        {
            Transform t = FindObjectByPath();

            yield return WorldObject.Instantiate(t);
        }
    }

    [ProtoContract]
    public class WorldObjectPatch : WorldChange
    {
        [ProtoMember(2)]
        public List<WOCBase> components;

        public override IEnumerator Apply()
        {
            Transform t = FindObjectByPath();

            t.TryGetComponent(out WorldObjectComponent cur_woc);

            // NB: Protobuf omits empty lists, rendering them as null while deserialization.
            for (int i = 0; i < components?.Count; i++)
                cur_woc.AddOrReplaceComponent(components[i]);

            yield return null;
        }
    }

    [ProtoContract]
    public class WorldObjectDeletion : WorldChange
    {
        // Nothing more to need
        public override IEnumerator Apply()
        {
            Transform t = FindObjectByPath();

            // Unhook the object from the hierarchy first because we consider it deleted,
            // even if it's not yet destroyed in the current frame.
            t.SetParent(null);
            UnityEngine.Object.Destroy(t.gameObject);

            yield return null;
        }
    }

    [ProtoContract]
    public class WorldRollbackRequest : WorldChange
    {
        [ProtoMember(2)]
        public string hash;
        public override IEnumerator Apply()
        {
            yield return G.WorldEditorData.RecallUndoState(hash);
        }
    }

    #endregion
    // -------------------------------------------------------------------
    #region World Edit Patch root

    [ProtoContract]
    [ProtoInclude(65537, typeof(WorldObjectInsertion))]
    [ProtoInclude(65538, typeof(WorldObjectPatch))]
    [ProtoInclude(65539, typeof(WorldObjectDeletion))]
    [ProtoInclude(65540, typeof(WorldRollbackRequest))]
    [ProtoInclude(65541, typeof(WorldObjectPaste))]
    public abstract class WorldChange : IWorldChange
    {
        [ProtoMember(1)]
        public List<Guid> path;

        public void Serialize(Stream stream)
            => Serializer.Serialize(stream, this);

        public static WorldChange Deserialize(Stream stream)
            => Serializer.Deserialize<WorldChange>(stream);

        protected Transform FindObjectByPath() => WorldEditorData.FindObjectByPath(path);

        public abstract IEnumerator Apply();

        public void SetPathFromThere(Transform t) => path = WorldEditorData.GetPathFromObject(t);

        public void EmitToServer()
        {
#if UNITY_EDITOR
            // Shortcut in 'lean' setup/test scene.
            if(SettingsManager.Instance == null)
            {
                G.WorldEditorData.DoApply(this);
                return;
            }
#endif
            using MemoryStream ms = new();
            Serialize(ms);
            CTSWorldObjectChange cts_wc = new() { changerequest = ms.ToArray() };
            SettingsManager.EmitToServerCTSPacket(cts_wc);
        }
    }

    #endregion
    // -------------------------------------------------------------------
    public static class GameObjectExtensions
    {
        internal static WorldObject MakeWorldObject(this Transform t, bool includeChildren = true)
        {
            WorldObject wo = new();

            if (t.TryGetComponent(out WorldObjectComponent woc))
                wo.asset = woc.Asset;
            else return null; // filter out alien GameObjects

            wo.name = t.name;
            wo.components = woc.WOComponents;
            wo.id = woc.Id;

            if (includeChildren)
            {
                for (int i = 0; i < t.childCount; ++i)
                {
                    WorldObject item = MakeWorldObject(t.GetChild(i), includeChildren);
                    if (item != null) wo.children.Add(item);
                }
            }

            return wo;
        }

        public static WorldObjectPatch MakePatch(this Transform t, bool complete = false)
        {
            if (!t.TryGetComponent(out WorldObjectComponent woc))
                throw new ArgumentException("GameObject is not in the world object hierarchy");

            return woc.MakePatch(complete);
        }

        public static WorldObjectInsertion MakeInsertion(this Transform t)
        {
            if (!t.TryGetComponent(out WorldObjectComponent woc))
                throw new ArgumentException("GameObject is not in the world object hierarchy");

            return woc.MakeInsertion();
        }

        public static WorldObject MakeWorldObject(this GameObject go, bool includeChildren = true)
            => go.transform.MakeWorldObject(includeChildren);

        public static WorldObjectPatch MakePatch(this GameObject go, bool complete = false)
            => go.transform.MakePatch(complete);

        public static WorldObjectInsertion MakeInsertion(this GameObject go)
            => go.transform.MakeInsertion();
    }
}
