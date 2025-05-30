using Arteranos;
using Arteranos.Core;
using Arteranos.XR;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine.Assertions;

namespace UnityEngine.XR.Interaction.Toolkit
{
    /// <summary>
    /// The <see cref="TeleportationProvider"/> is responsible for moving the XR Origin
    /// to the desired location on the user's request.
    /// </summary>
    [AddComponentMenu("XR/Locomotion/Crtled Tele Provider", 11)]
    public class CTeleProvider : UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider
    {
        private ActionBasedContinuousMoveProvider MoveProvider = null;

        public TeleportType TeleportType = TeleportType.Instant;
        public float TravelDuration = 0.0f;

        private IEnumerator ZipLineToDestination(Vector3 src, Quaternion srcRotation, Vector3 dest, Vector3 destUp, Vector3 destForward)
        {
            Vector3 srcForward = srcRotation * Vector3.forward;

            // destUp zero: No rotstion change
            // destForward zero : Rotate around forward (from src), to match Up again.
            Quaternion destRotation = destUp != Vector3.zero
                ? Quaternion.LookRotation(destForward != Vector3.zero ? destForward : srcForward, destUp)
                : srcRotation;

            float progress = 0.0f;

            // Suspend the gravity for the teleport travel duration
            bool hadGravity = MoveProvider.useGravity;
            MoveProvider.useGravity = false;

            while(true)
            {
                yield return null;

                progress += Time.deltaTime;

                float t = Mathf.Clamp01(progress / TravelDuration);
                Vector3 actual = Vector3.Lerp(src, dest, t);
                Quaternion actualRotation = Quaternion.Slerp(srcRotation, destRotation, t);

                Vector3 actualUp = destUp != Vector3.zero ? actualRotation * Vector3.up : Vector3.zero;
                Vector3 actualForward = destForward != Vector3.zero ? actualRotation * Vector3.forward : Vector3.zero;

                ReorientView(actualUp, actualForward);
                system.xrOrigin.MoveCameraToWorldLocation(actual);
                Physics.SyncTransforms();

                if(t >= 1.0f) break;
            }

            MoveProvider.useGravity = hadGravity;
            EndLocomotion();
        }

        private IEnumerator BlinkToDestination(Vector3 dest, Vector3 up, Vector3 forward)
        {
            G.XRVisualConfigurator.StartFading(1.0f, 0.25f);
            yield return new WaitForSeconds(0.25f);

            ReorientView(up, forward);
            system.xrOrigin.MoveCameraToWorldLocation(dest);
            Physics.SyncTransforms();

            G.XRVisualConfigurator.StartFading(0.0f, 0.25f);
            yield return new WaitForSeconds(0.25f);

            EndLocomotion();
        }


        protected override void Awake()
        {
            base.Awake();

            MoveProvider = GetComponent<ActionBasedContinuousMoveProvider>();
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected override void Update()
        {
            if (!validRequest || !BeginLocomotion())
                return;

            XROrigin xrOrigin = system.xrOrigin;

            Vector3 targetUp = Vector3.zero;
            Vector3 targetForward = Vector3.zero;

            if (xrOrigin != null)
            {
                switch (currentRequest.matchOrientation)
                {
                    case UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.WorldSpaceUp:
                        targetUp = Vector3.up;
                        break;
                    case UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.TargetUp:
                        targetUp = currentRequest.destinationRotation * Vector3.up;
                        break;
                    case UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.TargetUpAndForward:
                        targetUp = currentRequest.destinationRotation * Vector3.up;
                        targetForward = currentRequest.destinationRotation * Vector3.forward;
                        break;
                    case UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.None:
                        // Change nothing. Maintain current origin rotation.
                        break;
                    default:
                        Assert.IsTrue(false, $"Unhandled {nameof(UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation)}={currentRequest.matchOrientation}.");
                        break;
                }

                Vector3 heightAdjustment = xrOrigin.Origin.transform.up * xrOrigin.CameraInOriginSpaceHeight;

                Vector3 cameraDestination = currentRequest.destinationPosition + heightAdjustment;

                if(TeleportType == TeleportType.Instant)
                {
                    ReorientView(targetUp, targetForward);
                    xrOrigin.MoveCameraToWorldLocation(cameraDestination);
                    Physics.SyncTransforms();
                    EndLocomotion();
                }
                else if(TeleportType == TeleportType.Blink)
                {
                    StartCoroutine(BlinkToDestination(cameraDestination, targetUp, targetForward));
                }
                else if(TeleportType == TeleportType.Zipline)
                {
                    StartCoroutine(ZipLineToDestination(
                        xrOrigin.Origin.transform.position + heightAdjustment,
                        xrOrigin.Origin.transform.rotation,
                        cameraDestination, targetUp, targetForward));
                }
            }

            validRequest = false;
        }

        private void ReorientView(Vector3 up, Vector3 forward)
        {
            if(up == Vector3.zero) return;

            if (forward == Vector3.zero)
                system.xrOrigin.MatchOriginUp(up);
            else
                system.xrOrigin.MatchOriginUpCameraForward(up, forward);
        }
    }
}
