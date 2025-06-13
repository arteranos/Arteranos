/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit;

using Arteranos.Core;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Arteranos.Common;

namespace Arteranos.XR
{
    public class XRRigConfigurator : MonoBehaviour
    {
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor LeftInteractor = null;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual LeftLineVisual = null;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor RightInteractor = null;
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual RightLineVisual = null;

        private AvatarSnapTurnProvider SnapTurnProvider = null;
        private AvatarContinuousTurnProvider ContTurnProvider = null;
        private AvatarMoveProvider MoveProvider = null;
        private CTeleProvider CTeleProvider = null;
        private InputActionManager InputActionManager = null;

        private readonly Gradient onlyValidVisibleRay = new()
        {
            colorKeys = new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0f, 1f) },
        };

        private readonly Gradient alwaysVisibleRay = new()
        {
            colorKeys = new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
        };

        private void Awake()
        {
            SnapTurnProvider = GetComponent<AvatarSnapTurnProvider>();
            ContTurnProvider = GetComponent<AvatarContinuousTurnProvider>();
            MoveProvider     = GetComponent<AvatarMoveProvider>();
            CTeleProvider    = GetComponent<CTeleProvider>();
            InputActionManager = GetComponent<InputActionManager>();
        }

        private void Start()
        {
            G.Client.OnXRControllerChanged += DownloadControlSettings;
            G.Client.PingXRControllersChanged();
        }

        private void OnDestroy() => G.Client.OnXRControllerChanged -= DownloadControlSettings;


        private void DownloadControlSettings(ControlSettingsJSON ccs, MovementSettingsJSON mcs, PermissionsJSON sp)
        {
            if (ccs == null) return;

            bool vrmode = G.Client.VRMode;

            bool left_on = ccs.Controller_left && vrmode;
            bool right_on = ccs.Controller_right && vrmode;

            // The configuration could be a leftover from the desktop mode, set
            // the right controller active as an emergency.
            if (vrmode && !left_on && !right_on)
                right_on = true;

            if (LeftInteractor != null)
            {
                LeftInteractor.enabled = left_on;
                LeftLineVisual.enabled = left_on;

                LeftLineVisual.invalidColorGradient = ccs.Controller_active_left
                    ? alwaysVisibleRay
                    : onlyValidVisibleRay;

                LeftInteractor.lineType = ccs.Controller_Type_left == RayType.Straight
                    ? UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor.LineType.StraightLine
                    : UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor.LineType.ProjectileCurve;

                LeftInteractor.acceleration = ccs.Controller_Type_left == RayType.HighArc
                    ? 5.0f
                    : 9.8f;
            }

            if (RightInteractor != null)
            {
                RightInteractor.enabled = right_on;
                RightLineVisual.enabled = right_on;

                RightLineVisual.invalidColorGradient = ccs.Controller_active_right
                    ? alwaysVisibleRay
                    : onlyValidVisibleRay;

                RightInteractor.lineType = ccs.Controller_Type_right == RayType.Straight
                    ? UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor.LineType.StraightLine
                    : UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor.LineType.ProjectileCurve;

                RightInteractor.acceleration = ccs.Controller_Type_right == RayType.HighArc
                    ? 5.0f
                    : 9.8f;
            }

            MoveProvider.useGravity = !(sp.Flying ?? false                               // User preferences
                && Core.Utils.IsAbleTo(Social.UserCapabilities.CanEnableFly, null));        // Server restrictions (no server = true)

            TurnType turnType = mcs.Turn;
            StartCoroutine(ReconfigureTurnType(turnType));

            ContTurnProvider.turnSpeed = mcs.SmoothTurnSpeed;

            CTeleProvider.TravelDuration = mcs.Teleport == TeleportType.Instant
                ? 0.0f
                : mcs.ZipLineDuration;

            CTeleProvider.TeleportType = mcs.Teleport;

            // Set turning/strafing options.
            MoveProvider.EnableStrafeLeft = ccs.StickType_Left == StickType.Strafe;
            MoveProvider.EnableStrafeRight = ccs.StickType_Right == StickType.Strafe;

            SnapTurnProvider.EnableTurnLeft = ccs.StickType_Left == StickType.Turn;
            SnapTurnProvider.EnableTurnRight = ccs.StickType_Right == StickType.Turn;
            ContTurnProvider.EnableTurnLeft = ccs.StickType_Left == StickType.Turn;
            ContTurnProvider.EnableTurnRight = ccs.StickType_Right == StickType.Turn;
        }

        private IEnumerator ReconfigureTurnType(TurnType turnType)
        {
            bool actual = ContTurnProvider.enabled;
            bool desired = turnType == TurnType.Smooth;

            bool update = (actual != desired)
                || !(ContTurnProvider.enabled || SnapTurnProvider.enabled);

            if (update)
            {
                yield return new WaitForSeconds(0.25f);

                InputActionManager.enabled = false;

                yield return new WaitForSeconds(0.25f);

                SnapTurnProvider.enabled = false;
                ContTurnProvider.enabled = false;

                yield return new WaitForSeconds(0.25f);

                SnapTurnProvider.enabled = !desired;
                ContTurnProvider.enabled = desired;

                yield return new WaitForSeconds(0.25f);

                InputActionManager.enabled = true;
            }

            switch (turnType)
            {
                case TurnType.Snap90:
                    SnapTurnProvider.turnAmount = 90;
                    break;
                case TurnType.Snap45:
                    SnapTurnProvider.turnAmount = 45;
                    break;
                case TurnType.Snap30:
                    SnapTurnProvider.turnAmount = 30;
                    break;
                case TurnType.Snap225:
                    SnapTurnProvider.turnAmount = 22.5f;
                    break;
            }

        }
    }
}
