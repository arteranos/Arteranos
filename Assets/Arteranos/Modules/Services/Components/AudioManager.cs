/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Audio;
using UnityEngine;
using Arteranos.Core;
using System;
using UnityEngine.Audio;

namespace Arteranos.Services
{
    public class AudioManager : MonoBehaviour, IAudioManager
    {
        public IVoiceInput MicInput { get; set; }

        public event Action<int, byte[]> OnSegmentReady
        {
            add => MicInput.OnSegmentReady += value;
            remove => MicInput.OnSegmentReady -= value;
        }

        public event Action<float[]> OnSampleReady
        {
            add => MicInput.OnSampleReady += value;
            remove => MicInput.OnSampleReady -= value;
        }

        public AudioMixerGroup MixerGroupVoice
        {
            get
            {
                AudioMixerGroup[] audioMixerGroups = mixer.FindMatchingGroups("Master/Voice");
                return (audioMixerGroups?.Length ?? 0) > 0 ? audioMixerGroups[0] : null;
            }
        }

        public AudioMixerGroup MixerGroupEnv
        {
            get
            {
                AudioMixerGroup[] audioMixerGroups = mixer.FindMatchingGroups("Master/Environment");
                return (audioMixerGroups?.Length ?? 0) > 0 ? audioMixerGroups[0] : null;
            }
        }

        public float VolumeMaster
        {
            get => GetVolume("Master");
            set => SetVolume("Master", value);
        }
        public float VolumeVoice
        {
            get => GetVolume("Voice");
            set => SetVolume("Voice", value);
        }
        public float VolumeEnv
        {
            get => GetVolume("Env");
            set => SetVolume("Env", value);
        }

        public float MicGain
        {
            get => MicInput.Gain;
            set => MicInput.Gain = value;
        }

        public int MicAGCLevel
        {
            get => micAGCLevel;
            set
            {
                micAGCLevel = value;
                MicInput.SetAGCLevel(value);
            }
        }

        public int ChannelCount => MicInput.ChannelCount;

        public int SampleRate => MicInput.SampleRate;

        private int micAGCLevel;

        private static AudioMixer mixer => BP.I.AudioMixer;

        private void Awake()
        {
            G.AudioManager = this;
        }

        private void Start()
        {
            MicInput = Audio.MicInput.New(GetDeviceId(), 24000);

            PullVolumeSettings();
        }

        public void RenewMic() => Audio.MicInput.Renew(GetDeviceId(), 24000);

        public int? GetDeviceId()
        {
            string device = G.Client.AudioSettings.InputDevice;
            int? deviceId = Array.IndexOf(Microphone.devices, device);
            deviceId = (deviceId < 0) ? null : deviceId;
            return deviceId;
        }
        private static void SetVolume(string group, float volume) => mixer.SetFloat($"Vol{group}", volume - 80.0f);

        private static float GetVolume(string group)
        {
            mixer.GetFloat($"Vol{group}", out float val);
            return val + 80.0f;
        }

        public void PullVolumeSettings()
        {
            ClientAudioSettingsJSON audioSettings = G.Client.AudioSettings;
            VolumeEnv = audioSettings.EnvVolume;
            VolumeMaster = audioSettings.MasterVolume;
            VolumeVoice = audioSettings.VoiceVolume;

            MicInput.Gain = Utils.LoudnessToFactor(audioSettings.MicInputGain);
            MicInput.SetAGCLevel(audioSettings.AGCLevel);
        }

        public void PushVolumeSettings()
        {
            ClientAudioSettingsJSON audioSettings = G.Client.AudioSettings;
            audioSettings.MasterVolume = VolumeMaster;
            audioSettings.VoiceVolume= VolumeVoice;
            audioSettings.EnvVolume = VolumeEnv;
        }

        public IVoiceOutput GetVoiceOutput(int SampleRate, int ChannelCount) 
            => AudioOutput.New(SampleRate, ChannelCount, MixerGroupVoice);
    }
}

