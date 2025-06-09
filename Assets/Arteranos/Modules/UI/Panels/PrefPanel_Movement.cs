/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Arteranos.Core;

namespace Arteranos.UI
{
    public class PrefPanel_Movement : UIBehaviour
    {
        [SerializeField] private Toggle chk_Flying = null;
        [SerializeField] private TMP_Text lbl_Fly_disabled = null;
        [SerializeField] private Spinner spn_Turning= null;
        [SerializeField] private NumberedSlider sldn_SmoothTurnSpeed = null;
        [SerializeField] private Spinner spn_Teleporting= null;
        [SerializeField] private NumberedSlider sldn_ZiplineDuration = null;
        [SerializeField] private Spinner spn_Blinders = null;

        private Client cs = null;
        private bool dirty = false;

        private Dictionary<string, TurnType> spne_turn;
        private Dictionary<string, TeleportType> spne_teleport;
        private Dictionary<string, ComfortBlindersType> spne_comfortblinders;

        protected override void Awake()
        {
            base.Awake();

            chk_Flying.onValueChanged.AddListener(OnFlyingChanged);

            spn_Turning.OnChanged += OnTurningChanged;
            spn_Teleporting.OnChanged += OnTeleportingChanged;
            spn_Blinders.OnChanged += OnBlindersChanged;
            sldn_SmoothTurnSpeed.OnValueChanged += OnSmoothTurnSpeedChanged;
            sldn_ZiplineDuration.OnValueChanged += OnZipLineDurationChanged;
        }

        private bool? oldFlyingPermission = null;
        private void Update()
        {
            bool flyingPermission = Utils.IsAbleTo(Social.UserCapabilities.CanEnableFly, null);

            if(flyingPermission != oldFlyingPermission)
            {
                chk_Flying.gameObject.SetActive(flyingPermission);
                lbl_Fly_disabled.gameObject.SetActive(!flyingPermission);
                oldFlyingPermission = flyingPermission;
            }
        }

        private void OnFlyingChanged(bool arg0)
        {
            cs.Permissions.Flying = arg0;
            cs.PingXRControllersChanged();
            dirty = true;
        }

        private void OnTurningChanged(int arg1, bool arg2)
        {
            cs.Movement.Turn = spn_Turning.GetEnumValue(spne_turn);
            sldn_SmoothTurnSpeed.interactable = cs.Movement.Turn == TurnType.Smooth;
            cs.PingXRControllersChanged();
            dirty = true;
        }

        private void OnTeleportingChanged(int arg1, bool arg2)
        {
            cs.Movement.Teleport = spn_Teleporting.GetEnumValue(spne_teleport);
            sldn_ZiplineDuration.interactable = cs.Movement.Teleport == TeleportType.Zipline;
            cs.PingXRControllersChanged();
            dirty = true;
        }

        private void OnBlindersChanged(int arg1, bool arg2)
        {
            cs.Movement.ComfortBlinders = spn_Blinders.GetEnumValue(spne_comfortblinders);
            cs.PingXRControllersChanged();
            dirty = true;
        }

        private void OnSmoothTurnSpeedChanged(float obj)
        {
            cs.Movement.SmoothTurnSpeed = sldn_SmoothTurnSpeed.value;
            cs.PingXRControllersChanged();
            dirty = true;
        }

        private void OnZipLineDurationChanged(float obj)
        {
            cs.Movement.ZipLineDuration = sldn_ZiplineDuration.value;
            cs.PingXRControllersChanged();
            dirty = true;
        }

        protected override void Start()
        {
            base.Start();

            cs = G.Client;

            MovementSettingsJSON movement = cs.Movement;

            chk_Flying.isOn = cs.Permissions.Flying ?? false;
            spn_Turning.FillSpinnerEnum(out spne_turn, movement.Turn);
            spn_Teleporting.FillSpinnerEnum(out spne_teleport, movement.Teleport);
            spn_Blinders.FillSpinnerEnum(out spne_comfortblinders, movement.ComfortBlinders);

            sldn_ZiplineDuration.value = movement.ZipLineDuration;
            sldn_ZiplineDuration.interactable = movement.Teleport == TeleportType.Zipline;

            sldn_SmoothTurnSpeed.value = movement.SmoothTurnSpeed;
            sldn_SmoothTurnSpeed.interactable = movement.Turn == TurnType.Smooth;

            dirty = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Might be to disabled before it's really started, so cs may be null yet.
            if(dirty) cs?.Save();
        }
    }
}
