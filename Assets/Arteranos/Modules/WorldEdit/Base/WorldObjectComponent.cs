/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */


using System.Collections.Generic;

using UnityEngine;
using System;
using UnityEngine.XR.Interaction.Toolkit;
using Arteranos.WorldEdit.Components;
using Arteranos.Services;
using Arteranos.XR;
using System.Linq;
using Arteranos.Core;

namespace Arteranos.WorldEdit
{
    // Just for keeping the data for converting the gameobject to a serializable world object.
    public class WorldObjectComponent : MonoBehaviour, IEnclosedObject
    {
        public WorldObjectAsset Asset { get; set; } = null;
        public Guid Id { get; set; } = new();
        public List<WOCBase> WOComponents { get; set; } = null;
        public DateTime ExpirationTime { get; set; } = DateTime.MaxValue;
        public Transform DataObject { get; set; } = null;
        public GameObject EnclosingObject
        {
            get => enclosingObject;
            set
            {
                enclosingObject = value;
                UpdatePhysicsState();
            }
        }
        public bool? IsOnServer 
            => EnclosingObject && EnclosingObject.TryGetComponent(out IEnclosingObject o) 
            ? o.IsOnServer 
            : null;

        public bool IsLocked
        {
            get => isLocked;
            set
            {
                isLocked = value;
                UpdatePhysicsState();
            }
        }

        public IClickable IsClickable
        {
            get
            {
                foreach (WOCBase component in WOComponents)
                    if (component is IClickable cl) return cl;
                return null;
            }
        }

        public IRigidBody IsGrabbable
        {
            get
            {
                foreach (WOCBase component in WOComponents)
                    if (component is IRigidBody gr) return gr;
                return null;
            }
        }


        private bool isLocked = false;

        private Rigidbody body = null;
        private ArteranosGrabInteractable mover = null;
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable clicker = null;
        private GameObject enclosingObject = null;

        public void Awake()
        {
            body = gameObject.AddComponent<Rigidbody>();

            mover = gameObject.AddComponent<ArteranosGrabInteractable>();
            mover.throwOnDetach = true;
            mover.smoothPosition = true;
            mover.smoothRotation = true;
            mover.enabled = false;

            clicker = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            clicker.enabled = false;

            mover.firstSelectEntered.AddListener(GotObjectGrabbed);
            mover.lastSelectExited.AddListener(GotObjectReleased);
            mover.OnDetach += GotObjectDetach;

            clicker.activated.AddListener(GotObjectClicked);

            G.WorldEditorData.OnEditorModeChanged += GotEditorModeChanged;

            UpdatePhysicsState();
        }

        public void OnDestroy()
        {
            G.WorldEditorData.OnEditorModeChanged -= GotEditorModeChanged;

            // If it's a spawned object, sign ourselves off. No matter if clients miscounted,
            // the server has the authority.
            if (DataObject && DataObject.TryGetComponent(out WorldObjectData worldObjectData))
                worldObjectData.SpawnedItems--;
        }

        private void GotEditorModeChanged(bool editing) => UpdatePhysicsState();

        public void Update()
        {
            if (ExpirationTime < DateTime.UtcNow)
            {
                // It it's networked, target the shell instead of the object itself.
                GameObject toGo = EnclosingObject ? EnclosingObject : gameObject;

                // Do it on the server or on offline, not on a client
                if (IsOnServer != false)
                    Destroy(toGo);
            }
                
            foreach (WOCBase component in WOComponents)
                component.Update();
        }

        public void LateUpdate()
        {
            foreach (WOCBase component in WOComponents)
                component.LateUpdate();
        }

        private void GotObjectGrabbed(SelectEnterEventArgs arg0)
        {
            // Grabbing means it's the focused object.
            if (!G.WorldEditorData.IsInEditMode)
                IsGrabbable?.GotGrabbed();
            else
                G.WorldEditorData.FocusedWorldObject = gameObject;
        }

        private void GotObjectReleased(SelectExitEventArgs arg0)
        {
            if (G.WorldEditorData.IsInEditMode)
            {
                // We're in edit mode, we are actually changing the world.
                TryGetWOC(out WOCTransform woct);

                (Vector3 position, Vector3 eulerRotation) = ConstrainMovement(woct.position, woct.rotation);

                woct.SetState(
                    position,
                    eulerRotation,
                    woct.scale);

                WorldObjectPatch wop = new() { components = new() { woct } };
                wop.SetPathFromThere(transform);
                wop.EmitToServer();
            }
        }

        // Needed release even after LateUpdate, so it's pushed back with GrabInteractable's
        // Detach()
        private void GotObjectDetach(Vector3 velocity, Vector3 angularVelocity)
        {
            if (!G.WorldEditorData.IsInEditMode)
                IsGrabbable?.GotReleased(velocity, angularVelocity);
        }

        private void GotObjectClicked(ActivateEventArgs arg0)
        {
            if (!G.WorldEditorData.IsInEditMode) 
                IsClickable?.GotClicked();
        }

        private (Vector3 position, Vector3 eulerRotation) ConstrainMovement(Vector3 oldPosition, Vector3 oldEulerRotation)
        {
            if(!G.WorldEditorData.LockXAxis && !G.WorldEditorData.LockYAxis && !G.WorldEditorData.LockZAxis)
                return (transform.localPosition, transform.localEulerAngles);

            Vector3 position = transform.localPosition;
            Vector3 eulerRotation = transform.localEulerAngles;

            if(G.WorldEditorData.LockXAxis)
            {
                position.x = oldPosition.x;
                eulerRotation.x = oldEulerRotation.x;
            }

            if(G.WorldEditorData.LockYAxis)
            {
                position.y = oldPosition.y;
                eulerRotation.y = oldEulerRotation.y;
            }

            if(G.WorldEditorData.LockZAxis)
            {
                position.z = oldPosition.z;
                eulerRotation.z = oldEulerRotation.z;
            }

            return (position, eulerRotation);
        }

        public IEnumerable<T> GetWOCs<T>() where T : class
        {
            IEnumerable<T> results = Enumerable.Empty<T>();
            foreach (object w in WOComponents) 
                if (w is T woc) 
                    results = results.Append(woc);
            return results;
        }

        public bool TryGetWOC<T>(out T woComponent) where T : class
        {
            foreach (object w in WOComponents)
                if (w is T woc)
                {
                    woComponent = woc;
                    return true;
                }
            woComponent = null;
            return false;
        }

        public bool TryGetWOC(out WOCBase woComponent, Type t)
        {
            foreach (WOCBase w in WOComponents)
                if (w.GetType() == t)
                {
                    woComponent = w;
                    return true;
                }
            woComponent = null;
            return false;
        }

        public bool AddOrReplaceComponent(WOCBase wocb)
        {
            for (int i = 0; i < WOComponents.Count; i++)
            {
                WOCBase w = WOComponents[i];
                if (wocb.GetType() == w.GetType())
                {
                    w.ReplaceValues(wocb);
                    w.CommitState();
                    return true;
                }
            }

            WOComponents.Add(wocb);
            wocb.GameObject = gameObject;
            wocb.CommitState();
            return false;
        }

        public void RemoveComponent(int index) => WOComponents.RemoveAt(index);

        public void CommitStates()
        {
            foreach(WOCBase w in WOComponents)
                w.CommitState();
        }

        public void UpdatePhysicsState()
        {
            bool isInEditMode = G.WorldEditorData.IsInEditMode;
            bool needsKinematic = IsOnServer == false;

            // It's in a not (yet) instantiated object, take it as-is within CommitState()
            if (!body || !mover || !clicker) return;

            // Children of spawners are like 'blueprints', not actual in-use objects.
            if(transform.parent != null)
            {
                WorldObjectComponent parentWOC = transform.parent.GetComponent<WorldObjectComponent>();
                if (parentWOC != null && parentWOC.TryGetWOC<WOCSpawner>(out _))
                    gameObject.SetActive(isInEditMode);
            }

            // Disable both of them to prevent the warning about conflicting interactables
            mover.enabled = false;
            clicker.enabled = false;

            // Fixup - Unity yells at you if it is a concave collider and it's physics-controlled.
            MeshCollider[] colliders = gameObject.GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider collider in colliders)
                needsKinematic |= !collider.convex;

            // Fallbacks if we haven't the RigidBody component
            body.isKinematic = needsKinematic;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.useGravity = false;

            foreach(IPhysicsWOC pwoc in GetWOCs<IPhysicsWOC>())
                pwoc.UpdatePhysicsState(isInEditMode);

            // Configure Grab Interactor
            mover.trackRotation = !(G.WorldEditorData.IsInEditMode && !G.Client.VRMode);
            mover.enabled = isInEditMode
                ? !IsLocked
                : IsGrabbable?.IsMovable ?? false;
            mover.customReticle = !isInEditMode && (IsGrabbable?.IsMovable ?? false)
                ? BP.I.GrabTargetReticle : null;

            // Configure Click (=Trigger) Interactor
            clicker.enabled = !isInEditMode && (IsClickable != null);
            clicker.customReticle = !isInEditMode ? BP.I.ClickTargetReticle : null;
        }

        public WorldObjectPatch MakePatch(bool complete = false)
        {
            WorldObjectPatch wop = new();

            if (complete)
                wop.components = WOComponents;
            else
            {
                wop.components = new();
                foreach (WOCBase component in WOComponents)
                    if (component.Dirty) wop.components.Add(component);
            }

            wop.SetPathFromThere(transform);
            return wop;
        }

        public WorldObjectInsertion MakeInsertion()
        {
            transform.TryGetComponent(out WorldObjectComponent woc);
            WorldObjectInsertion woi = new()
            {
                asset = woc.Asset,
                components = woc.WOComponents,
                name = transform.name,
                id = Guid.NewGuid(), // Creating a copy of an existing one, so spawn a new guid
            };

            woi.SetPathFromThere(transform.parent);
            return woi;
        }

    }
}