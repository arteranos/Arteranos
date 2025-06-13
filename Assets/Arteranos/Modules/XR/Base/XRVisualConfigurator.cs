/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Common;
using Arteranos.Core;
using UnityEngine;
using UnityEngine.Rendering;


namespace Arteranos.XR
{

    public class XRVisualConfigurator : MonoBehaviour, IXRVisualConfigurator
    {
        // The required speed (2m/s) to fully close the shutters (or rather to fully use the effects)
        [SerializeField] private float VelocityMax = 2.0f;

        // 1/4th of a second to close/open the shutters
        [SerializeField] private float BlinderDuration = 0.25f;

        private Vector3 pos = Vector3.zero;
        private float BlindersMaxValue = 0.0f;
        private float BlinderStrength = 0.0f;
        private Volume BlinderVolume;

        private float FadeTargetStrength = 0.0f;
        private float FadeDuration = 0.0f;
        private float FadeStrength = 0.0f;
        private Volume FaderVolume;

        private void Awake()
        {
            G.XRVisualConfigurator = this;
            FadeTargetStrength = 1.0f;
            FadeStrength = 1.0f; // Start with opaque
        }

        private void OnDestroy()
        {
            G.Client.OnXRControllerChanged -= DownloadControlSettings;
            G.XRVisualConfigurator = null;
        }

        void Start()
        {
            Volume[] volumes = GetComponentsInChildren<Volume>();
            BlinderVolume = volumes[0];
            FaderVolume = volumes[1];

            G.Client.OnXRControllerChanged += DownloadControlSettings;
            G.Client.PingXRControllersChanged();
        }

        void Update()
        {
            float d;
        
            Transform t = Camera.main != null ? Camera.main.transform : null;

            if(t == null) return;

            d = Vector3.Magnitude(t.position - pos) / Time.deltaTime;
            d = Mathf.Clamp01(d / VelocityMax);
            pos = t.position;

            Core.Utils.Tween(ref BlinderStrength, d, BlinderDuration);
            Core.Utils.Tween(ref FadeStrength, FadeTargetStrength, FadeDuration);

            // Too much hassle to pick out the vignette. Just use the whole global volume.
            if (BlinderVolume != null) BlinderVolume.weight = BlinderStrength * BlindersMaxValue;
            if (FaderVolume != null) FaderVolume.weight = FadeStrength;
        }

        private void DownloadControlSettings(ControlSettingsJSON ccs, MovementSettingsJSON mcs, PermissionsJSON sp)
        {
            if(mcs == null) return;

            switch(mcs.ComfortBlinders)
            {
                case ComfortBlindersType.Off:
                    BlindersMaxValue = 0.0f;
                    break;
                case ComfortBlindersType.Low:
                    BlindersMaxValue = 0.33f;
                    break;
                case ComfortBlindersType.Medium:
                    BlindersMaxValue = 0.66f;
                    break;
                case ComfortBlindersType.High:
                    BlindersMaxValue = 1.0f;
                    break;
            }
        }

        public void StartFading(float opacity, float duration = 0.5f)
        {
            FadeTargetStrength = opacity;
            FadeDuration = duration;
        }
    }
}
