/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Avatar;
using Arteranos.Core;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Management;

namespace Arteranos.XR
{
    public class XRControl : MonoBehaviour, IXRControl
    {
        public XROrigin VRRig;

        public IAvatarBrain Me { get; set; }
        public Vector3 CameraLocalOffset { get; private set; }
        public float EyeHeight { get; set; } = 1.75f;
        public float BodyHeight { get; set; } = 1.85f;

        public Vector3 heightAdjustment => CurrentVRRig.Origin.transform.up * CurrentVRRig.CameraInOriginSpaceHeight;

        private XROrigin CurrentVRRig { get; set; }

        public Transform rigTransform
        {
            get => CurrentVRRig.transform;
        }

        public Transform cameraTransform
        {
            get => CurrentVRRig.Camera.transform;
        }

        public void Awake()
        {
            G.XRControl = this;
            CurrentVRRig = FindObjectOfType<XROrigin>();
        }

        bool quitting = false;
        public IEnumerator VRLoopCoroutine()
        {
            while (true)
            {
                bool desiredVRMode = G.Client.DesiredVRMode && !quitting;
                bool actualVRMode = G.Client.VRMode;


                if (desiredVRMode && !actualVRMode)
                {
                    if (XRGeneralSettings.Instance.Manager.activeLoader == null)
                    {
                        Debug.Log("Initializing XR loader...");
                        XRGeneralSettings.Instance.Manager.InitializeLoaderSync();
                    }
                    // OpenXR restarter would be a good idea, but its implementation leaves a lot to be desired.
                    GameObject go = GameObject.Find("~oxrestarter");
                    if (go != null) Destroy(go);


                    // Successful, now?
                    if (XRGeneralSettings.Instance.Manager.activeLoader != null)
                    {
                        Debug.Log("Starting XR subsystems.");
                        XRGeneralSettings.Instance.Manager.StartSubsystems();
                        UpdateXROrigin(true);
                    }
                    else // Doesn't work now, switch off for now.
                        G.Client.DesiredVRMode = false;
                }
                if (!desiredVRMode && actualVRMode)
                {
                    StopXR();
                    UpdateXROrigin(false);
                }

                yield return new WaitForSeconds(2);
            }
        }


        void StopXR()
        {
            Debug.Log("Stopping XR...");

            // Implies StopSubsystems()
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            Debug.Log("XR stopped completely.");
        }

        // Start is called before the first frame update
        void Start()
        {
            UpdateXROrigin(false);
            StartCoroutine(VRLoopCoroutine());
        }


        void UpdateXROrigin(bool useVR)
        {
            IEnumerator ReconfigureCR()
            {
                yield return new WaitForSeconds(0.5f);
                ReconfigureXRRig();
            };

            G.Client.VRMode = useVR;

            CurrentVRRig = FindObjectOfType<XROrigin>();

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;

            if (CurrentVRRig != null)
            {
                position = CurrentVRRig.transform.position;
                rotation = CurrentVRRig.transform.rotation;

                Destroy(CurrentVRRig.gameObject);
            }

            CurrentVRRig = Instantiate(VRRig, position, rotation);
            StartCoroutine(ReconfigureCR());
        }

        // Cleanly shut down XR on exit.
        void OnApplicationQuit()
        {
            quitting = true;
            StopAllCoroutines();
            if (G.Client.VRMode) StopXR();
        }

        /// <summary>
        /// Reconfigure the XR rig to the new dimensions if we're switched
        /// from VR to 2D mode or switched avatars.
        /// </summary>
        public void ReconfigureXRRig()
        {
            Camera cam = CurrentVRRig.Camera;
            GameObject offsetObject = CurrentVRRig.CameraFloorOffsetObject;

            CameraLocalOffset = cam.transform.localPosition;

            // Oculus Quest 2's floor-to-eye adjustment is horribly lacking.
            // Even with in a seated position, the height measurement was off.
            // So, use the avatar's grounded standing eye height as the reference.
            CurrentVRRig.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.NotSpecified;
            CurrentVRRig.CameraYOffset = EyeHeight - CameraLocalOffset.y;
            offsetObject.transform.localPosition = new Vector3(0, EyeHeight, 0.2f) - CameraLocalOffset;

            CharacterController cc = CurrentVRRig.GetComponent<CharacterController>();
            cc.height = BodyHeight;
            cc.center = new Vector3(0, BodyHeight / 2 + cc.skinWidth, 0);

            // TODO Too wide means the floating feet, or I have to
            //      improve the feet IK up to the root pose.
            cc.radius = 0.01f;

        }

        public void FreezeControls(bool value)
        {
            XROrigin xro = CurrentVRRig;
            if (xro == null) return;

            ActionBasedSnapTurnProvider snapTurnProvider =
                xro.gameObject.GetComponent<ActionBasedSnapTurnProvider>();

            ActionBasedContinuousMoveProvider continuousMoveProvider =
                xro.gameObject.GetComponent<ActionBasedContinuousMoveProvider>();

            ActionBasedContinuousTurnProvider ContTurnProvider =
                xro.gameObject.GetComponent<ActionBasedContinuousTurnProvider>();


            KMTrackedPoseDriver kMTrackedPoseDriver =
                xro.gameObject.GetComponentInChildren<KMTrackedPoseDriver>();

            MovementSettingsJSON mcs = G.Client.Movement;

            bool smooth = mcs.Turn == TurnType.Smooth;
            if (snapTurnProvider != null) snapTurnProvider.enabled = !value && !smooth;
            if (ContTurnProvider != null) ContTurnProvider.enabled = !value && smooth;

            if (continuousMoveProvider != null) continuousMoveProvider.enabled = !value;
            if (kMTrackedPoseDriver != null) kMTrackedPoseDriver.enabled = !value;
        }

        private IEnumerator MoveRigCoroutine()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            Vector3 startPosition = Vector3.zero;
            Quaternion startRotation = Quaternion.identity;

            Transform spawn = User.SpawnManager.GetStartPosition();

            if (spawn != null)
            {
                startPosition = spawn.position;
                startRotation = spawn.rotation;
            }

            startPosition += G.XRControl.heightAdjustment;

            XROrigin xro = CurrentVRRig;

            xro.MatchOriginUpCameraForward(startRotation * Vector3.up, startRotation * Vector3.forward);
            xro.MoveCameraToWorldLocation(startPosition);
            Physics.SyncTransforms();

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
        }

        public void MoveRig()
        {
            StartCoroutine(MoveRigCoroutine());
        }

        public void Teleport(Vector3 position, Quaternion rotation) 
        {
            XROrigin xro = CurrentVRRig;
            if (!xro || !xro.gameObject.TryGetComponent(out UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider tp)) return;

            tp.QueueTeleportRequest(new()
            {
                destinationPosition = position,
                destinationRotation = rotation,
                matchOrientation = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.TargetUpAndForward,
                requestTime = Time.time
            });
        }
    }
}
