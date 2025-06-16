using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Codecs.Opus;
using Unity.Codecs.Opus.Enums;
using UnityEngine;

namespace Arteranos.Audio
{
    /// <summary>
    /// An <see cref="IAudioInput"/> implementation based on UniMic.
    /// For more on UniMic, visit https://www.github.com/adrenak/unimic
    /// </summary>
    public class MicInput : MonoBehaviour, IVoiceInput
    {
        public const float AGCDecayFactor = 1.0e-05f;
        private const float AGCAttackFactor = 1.0f;

        public event Action<int, byte[]> OnSegmentReady;
        public event Action<float[]> OnSampleReady;

        public int SampleRate { get; private set; }
        public int ChannelCount { get; private set; }

        public string deviceName;

        // In plain factor, to convert from dB use...
        // Fout = 10^(Q/20) * Fin
        public float Gain { get; set; } = 1.0f;

        // The maximun amplification level for the AGC in dB, when it's silent.
        public float AGCGainLevel = 0.0f;

        private AudioSource audiorecorder = null;
        private readonly List<float> micBuffer = new();
        private OpusEncoder encoder;
        private int packetndex = 0;
        private int packetSize;


        private AudioClip recorderClip
        {
            get => audiorecorder.clip;
            set => audiorecorder.clip = value;
        }

        [Obsolete("Cannot use new keyword to create an instance. Use .New() method instead")]
        public MicInput() { }

        private int ValidateSampleRate(int sampleRate)
        {
            switch((SamplingRate) sampleRate)
            {
                case SamplingRate.Sampling08000:
                    break;
                case SamplingRate.Sampling12000:
                    break;
                case SamplingRate.Sampling16000:
                    break;
                case SamplingRate.Sampling24000:
                    break;
                case SamplingRate.Sampling48000:
                    break;
                default:
                    Debug.LogWarning($"SamplingRate of {sampleRate} outside of the Opus specification, setting to 8kHz");
                    sampleRate = (int) SamplingRate.Sampling08000;
                    break;
            }

            return sampleRate;
        }

        public static MicInput New(int? micDeviceId = 0, int? desiredRate = null)
        {
            GameObject go = new("_Microphone");
            DontDestroyOnLoad(go);
            go.transform.SetParent(Core.SettingsManager.Purgatory);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return go.AddComponent<MicInput>().New_(micDeviceId, desiredRate);
        }

        public static MicInput Renew(int? micDeviceId = 0, int? desiredRate = null)
        {
            MicInput uvmi = FindFirstObjectByType<MicInput>();
            return uvmi.Renew_(micDeviceId, desiredRate);
        }

        private MicInput Renew_(int? micDeviceId, int? desiredRate)
        {
            SetupMic(micDeviceId, desiredRate, true);

            return this;
        }

        private MicInput New_(int? micDeviceId, int? desiredRate)
        {
            audiorecorder = gameObject.AddComponent<AudioSource>();
            audiorecorder.loop = true;

            SetupMic(micDeviceId, desiredRate);

            encoder = new((SamplingRate) SampleRate, (Channels) ChannelCount)
            {
                EncoderDelay = Delay.Delay20ms,
                SignalHint = SignalHint.Voice,
                MaxBandwidth = Bandwidth.Wideband,
                Bitrate = SampleRate
            };

            //encoder.ForceChannels = Unity.Codecs.Opus.Enums.ForceChannels.NoForce;
            //encoder.Complexity = Unity.Codecs.Opus.Enums.Complexity.Complexity0;
            //encoder.DtxEnabled = true;
            //encoder.ExpectedPacketLossPercentage = 0;
            //encoder.UseInbandFEC = true;
            //encoder.UseUnconstrainedVBR = true;

            Debug.Log($"Framesize: {encoder.FrameSizePerChannel}, {encoder.InputChannels}");

            // the encoder delay has some influence on the amout of data we need to send, but it's not a multiplication of it
            packetSize = encoder.FrameSizePerChannel * ChannelCount;

            if(recorderClip != null)
            {
                OnSampleReady += DeliverCompressedAudio;
                StartCoroutine(ReadRawAudio());
            }
            else
            {
                Debug.Log("Microphone capture is idle, probably there is none.");
            }

            return this;
        }

        private void SetupMic(int? micDeviceId, int? desiredRate, bool renew = false)
        {
            string oldDeviceName = deviceName;

            SampleRate = desiredRate ?? AudioSettings.outputSampleRate;
            SampleRate = ValidateSampleRate(SampleRate);

            deviceName = micDeviceId.HasValue ? Microphone.devices[micDeviceId.Value] : null;

            Debug.Log($"setup mic with {deviceName}, samplerate={SampleRate}");

            ChannelCount = 1;

            try
            {
                if(renew)
                    Microphone.End(oldDeviceName);

                recorderClip = Microphone.Start(
                    deviceName,
                    true,
                    1,
                    SampleRate);

                ChannelCount = recorderClip.channels;


                Debug.Log($"Clip samples={recorderClip.samples}, channels={ChannelCount}");
            }
            catch
            {
                Debug.Log("No microphone present");
            }

            // Force the reallocation of the temp buffer
            temp = null;
        }

        private void OnDestroy()
        {
            StopCoroutine(ReadRawAudio());
            OnSampleReady -= DeliverCompressedAudio;
            encoder.Dispose();
        }

        public void SetAGCLevel(int level)
        {
            switch(level)
            {
                case 0: AGCGainLevel = 0.0f; break;
                case 1: AGCGainLevel = 4.0f; break;
                case 2: AGCGainLevel = 8.0f; break;
                case 3: AGCGainLevel = 12.0f; break;
            }
        }

        private float[] temp = null;
        IEnumerator ReadRawAudio()
        {
            int loops = 0;
            int readAbsPos = 0;
            int prevPos = 0;

            temp ??= new float[packetSize];

            while(true)
            {
                AudioClip AudioClip = recorderClip;

                while(AudioClip != null)
                {
                    int currPos = Microphone.GetPosition(deviceName);
                    if(currPos < prevPos)
                        loops++;
                    prevPos = currPos;

                    int currAbsPos = loops * AudioClip.samples + currPos;
                    int nextReadAbsPos = readAbsPos + temp.Length;

                    if(nextReadAbsPos >= currAbsPos) break;

                    AudioClip.GetData(temp, readAbsPos % AudioClip.samples);

                    // Amplifying/attenuating.
                    for(int i = 0, c = temp.Length; i < c; ++i)
                    {
                        // AGC: Instantly go up, go slooooowly down.
                        Core.Utils.CalcVU(temp[i], ref AGCCharge, AGCAttackFactor, AGCDecayFactor);

                        // AGC Gain slides from 0 dB to (AGCGainLevel) dB when it's silent.
                        // ( = AGCCharge near 0)
                        float ActualAGCFactor = Core.Utils.LoudnessToFactor(
                            (1 - Mathf.Clamp01(AGCCharge * 25.0f)) * AGCGainLevel);

                        //if((samplesCount++ % 24000) == 0)
                        //    Debug.Log(ActualAGCFactor);

                        temp[i] *= Gain * ActualAGCFactor;
                        temp[i] = Mathf.Clamp(temp[i], -1.0f, 1.0f);
                    }


                    OnSampleReady?.Invoke(temp);

                    readAbsPos = nextReadAbsPos;
                }
                yield return null;
            }
        }

        private float AGCCharge = 0.0f;

        private void DeliverCompressedAudio(float[] data)
        {
            micBuffer.AddRange(data);

            int packets = micBuffer.Count / packetSize;
            for (int i = 0; i < packets; i++)
            {
                byte[] encodedData = encoder.Encode(micBuffer.GetRange(i * packetSize, packetSize).ToArray());
                OnSegmentReady?.Invoke(packetndex++, encodedData);
            }
            micBuffer.RemoveRange(0, packets * packetSize);
        }

        public void Dispose() => Destroy(audiorecorder.gameObject);
    }
}
