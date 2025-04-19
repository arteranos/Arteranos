/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using UnityEngine;

using Newtonsoft.Json;
using System.ComponentModel;
using System.Collections.Generic;
using Arteranos.Social;
using Arteranos.Core.Cryptography;
using Ipfs.Cryptography.Proto;
using Ipfs;

namespace Arteranos.Core
{
    public enum Visibility
    {
        Invalid = 0,
        [Description("Invisible")]
        Invisible,      // Appear offline
        [Description("Do Not Disturb")]
        DND,            // Do Not Disturb - no direct messaging
        [Description("Away")]
        AFK,            // Been idle / HMD on standby
        [Description("Online")]
        Online          // Ready
    }

    public enum VKUsage
    {
        [Description("never")]
        Never = 0,
        [Description("VR only")]
        VROnly,
        [Description("always")]
        Always
    }

    public enum VKLayout
    {
        [Description("de-DE (full)")]
        de_DE_full = 0,
        [Description("en-US (full)")]
        en_US_full
    }

    public enum RayType
    {
        [Description("line")]
        Straight = 0,
        [Description("low arc")]
        LowArc,
        [Description("high arc")]
        HighArc
    }

    public enum StickType
    {
        Off = 0,
        Turn,
        Strafe
    }

    public enum TurnType
    {
        [Description("Smooth")]
        Smooth = 0,
        [Description("Snap 90°")]
        Snap90,
        [Description("Snap 45°")]
        Snap45,
        [Description("Snap 30°")]
        Snap30,
        [Description("Snap 22.5°")]
        Snap225
    }

    public enum TeleportType
    {
        [Description("Instant")]
        Instant = 0,
        [Description("Blink")]
        Blink,
        [Description("Zipline")]
        Zipline
    }

    public enum ComfortBlindersType
    {
        [Description("Off")]
        Off = 0,
        [Description("Low")]
        Low,
        [Description("Medium")]
        Medium,
        [Description("High")]
        High,
    }

    public struct ServerPass
    {
        // The server's public key. Differing keys could mean an attack.
        public byte[] ServerPublicKey;

        // Hash of the privacy and TOS agreement. Same as with KnownDefaultTOS.
        public byte[] PrivacyTOSHash;
    }

    public class AvatarDescriptionJSON : IEquatable<AvatarDescriptionJSON>
    {
        // Avatar designator, valid only for the selected avatar provider
        public string AvatarCidString { get; set; }

        // Avatar height in cm
        public float AvatarHeight { get; set; } = 175;

        public override bool Equals(object obj) => obj is AvatarDescriptionJSON jSON && Equals(jSON);
        public bool Equals(AvatarDescriptionJSON other) 
            => AvatarCidString == other.AvatarCidString 
            && AvatarHeight == other.AvatarHeight;
        public override int GetHashCode() => HashCode.Combine(AvatarCidString, AvatarHeight);

        public static bool operator ==(AvatarDescriptionJSON left, AvatarDescriptionJSON right) => left.Equals(right);
        public static bool operator !=(AvatarDescriptionJSON left, AvatarDescriptionJSON right) => !(left == right);
    }

    public class ClientAudioSettingsJSON
    {
        // Reminder: Unity measures volumes as -80..+20 dB. So 80 means 0 dB.
        // Master Volume, 0 to 100
        public float MasterVolume { get; set; } = 80;

        // Voice volume, 0 to 100
        public float VoiceVolume { get; set; } = 80;

        // Environment volume, 0 to 100
        public float EnvVolume { get; set; } = 80;

        // Mic Input Device, null means system default device
        public string InputDevice { get; set; } = null;

        // Mic Input Gain in dB, -6 to +6, meaning factor 0.5 to 2
        public float MicInputGain { get; set; } = 0;

        // Automatic Gain Control level, from none to high
        public int AGCLevel { get; set; } = 0;
    }

    public class ControlSettingsJSON
    {
        public VKUsage VK_Usage { get; set; } = VKUsage.VROnly;

        public VKLayout VK_Layout { get; set; } = VKLayout.en_US_full;

        public float NameplateIn { get; set; } = 0.5f;

        public float NameplateOut { get; set; } = 4.0f;

        public bool Controller_left { get; set; } = true;

        public bool Controller_right { get; set; } = true;

        public bool Controller_active_left { get; set; } = true;

        public bool Controller_active_right { get; set; } = true;

        public StickType StickType_Left { get; set; } = StickType.Strafe;

        public StickType StickType_Right { get; set; } = StickType.Turn;

        public RayType Controller_Type_left { get; set; } = RayType.Straight;

        public RayType Controller_Type_right { get; set; } = RayType.Straight;
    }

    public class MovementSettingsJSON
    {
        public TurnType Turn { get; set; } = TurnType.Snap45;

        public float SmoothTurnSpeed { get; set; } = 60.0f;

        public TeleportType Teleport { get; set; } = TeleportType.Instant;

        public float ZipLineDuration { get; set; } = 1.0f;

        public ComfortBlindersType ComfortBlinders { get; set; } = ComfortBlindersType.Off;
    }

    public class UserSocialEntryJSON
    {
        // The user's friend (or blocked) state
        public ulong State { get; set; } = 0;

        // The user's Icon file CID, if available
        public Cid Icon { get; set; } = null;
    }

    public class UserDataSettingsJSON
    {
        // User's signature key pair
        public byte[] UserSignKeyPair = null;

        // The display name of the user. Generate if null
        public string Nickname { get; set; } = SessionConstants.Instance.DefaultUserName;

        // The user's 2D Icon.
        public Cid UserIconCid { get; set; } = null;

        // Current avatar
        public AvatarDescriptionJSON CurrentAvatar { get; set; } = new() 
        {
            AvatarCidString = null, // First-time startup will load a dafault avatar
            AvatarHeight = 175
        };

        // Avatar storage
        public List<AvatarDescriptionJSON> AvatarGallery { get; set; } = new();

        // The user's social state to others
        public Dictionary<UserID, UserSocialEntryJSON> SocialList { get; set; } = new();
    }

    public class UserHUDSettingsJSON
    {
        public float AxisX { get; set; } = -1.3f;   // * 10

        public float AxisY { get; set; } = -0.6f;   // * 10

        public float Log2Size { get; set; } = 0;    // 2^x (-2 ... 2 <=> 0.25 ... 4 )

        public float Tightness { get; set; } = 0.1f;

        public float Delay { get; set; } = 2;

        public int ClockDisplay { get; set; } = 2;  // 0 to 2 should be self explanatory, right?

        public bool Seconds { get; set; } = false;
    }

    public struct WOCEntry
    {
        public string IPFSPath;
        public string FriendlyName;
    }

    public class WorldEditorAC
    {
        // world objects collection (glTF files)
        public List<WOCEntry> WorldObjectsGLTF { get; set; } = new();

        // world objects collection (kits a.k.a asset bundles)
        public List<WOCEntry> WorldObjectsKits { get; set; } = new();
    }

    public class ClientSettingsJSON
    {
        // More personal data
        public virtual UserDataSettingsJSON Me { get; set; } = new();

        // Guides the online and availability state
        // public virtual Visibility Visibility { get; set; } = Visibility.Online;

        // Current VR mode
        public virtual bool VRMode { get; set; } = false;

        // Desired VR mode
        public virtual bool DesiredVRMode { get; set; } = false;

        // Friends bubble size
        public virtual float SizeBubbleFriends { get; set; } = 1.0f;

        // Strangers bubble size
        public virtual float SizeBubbleStrangers { get; set; } = 1.0f;

        // Privacy settings
        public virtual UserPrivacy UserPrivacy { get; set; } = new() { UIDRepresentation = UIDRepresentation.Dice_5 };

        // The audio settings
        public virtual ClientAudioSettingsJSON AudioSettings { get; set; } = new();

        // The content filter preferences for sorting the servers
        public virtual ServerPermissions ContentFilterPreferences { get; set; } = new(true);

        // The controls settings
        public virtual ControlSettingsJSON Controls { get; set; } = new();

        // The movement settings
        public virtual MovementSettingsJSON Movement { get; set; } = new();

        public virtual UserHUDSettingsJSON UserHUD { get; set; } = new();

        // The server collection
        public virtual List<string> ServerList { get; set; } = new();

        // The known privacy and TOS agreements.
        public virtual List<string> KnownAgreements { get; set; } = new();

        // Server keys we've encountered (host ip/name => ServerPass)
        public virtual Dictionary<string, ServerPass> ServerPasses { get; set; } = new();

        // Should we allow connecting to servers which bear custom privacy agreements?
        public virtual bool AllowCustomTOS { get; set; } = false;

        // Null: User doesn't know.
        // Same hash: User does know.
        // Inequal hash: User does know outdated default TOS.
        public virtual byte[] KnowsDefaultTOS { get; set; } = null;

        // Favourited worlds
        public List<Cid> FavouritedWorlds { get; set; } = new();

        // World Editor Assets Collections
        public WorldEditorAC WEAC { get; set; } = new();

        // The text message templates
        public virtual List<string> PresetStrings { get; set; } = new();
    }

    public class Client : ClientSettingsJSON
    {
        #region Change events

        public const string PATH_CLIENT_SETTINGS = "UserSettings.json";

        public event Action<string, float> OnAvatarChanged;
        public event Action<bool> OnVRModeChanged;
        public event Action<float, float> OnPrivacyBubbleChanged;
        public event Action<ControlSettingsJSON, MovementSettingsJSON, ServerPermissions> OnXRControllerChanged;
        public event Action<UserHUDSettingsJSON> OnUserHUDSettingsChanged;
        public event Action<UserPrivacy> OnUserPrivacyChanged;

        [JsonIgnore]
        public string AvatarCidString
        {
            get => Me.CurrentAvatar.AvatarCidString;
            set 
            {
                string old = Me.CurrentAvatar.AvatarCidString;
                Me.CurrentAvatar.AvatarCidString = value;
                if(old != value) OnAvatarChanged?.Invoke(Me.CurrentAvatar.AvatarCidString, Me.CurrentAvatar.AvatarHeight);
            }
        }

        [JsonIgnore]
        public float AvatarHeight
        {
            get => Me.CurrentAvatar.AvatarHeight;
            set
            {
                float old = Me.CurrentAvatar.AvatarHeight;
                Me.CurrentAvatar.AvatarHeight = value;
                if (old != value) OnAvatarChanged?.Invoke(Me.CurrentAvatar.AvatarCidString, Me.CurrentAvatar.AvatarHeight);
            }
        }

        [JsonIgnore]
        private CryptoMessageHandler CMH = null;

        [JsonIgnore]
        public PublicKey UserSignPublicKey => CMH.SignPublicKey;

        [JsonIgnore]
        public PublicKey UserAgrPublicKey => CMH.AgreePublicKey;

        [JsonIgnore]
        public UserID MeUserID => new(UserSignPublicKey, Me.Nickname, Me.UserIconCid);

        [JsonIgnore]
        public override bool VRMode
        {
            get => !ConfigUtils.Unity_Server && base.VRMode;
            set {
                bool old = VRMode;
                base.VRMode = value;
                if(old != VRMode) OnVRModeChanged?.Invoke(VRMode);
            }
        }

        public override float SizeBubbleFriends
        {
            get => base.SizeBubbleFriends;
            set
            {
                base.SizeBubbleFriends = value;
                OnPrivacyBubbleChanged?.Invoke(SizeBubbleFriends, SizeBubbleStrangers);
            }
        }

        public override float SizeBubbleStrangers
        {
            get => base.SizeBubbleStrangers;
            set
            {
                base.SizeBubbleStrangers = value;
                OnPrivacyBubbleChanged?.Invoke(SizeBubbleFriends, SizeBubbleStrangers);
            }
        }

        public override UserPrivacy UserPrivacy
        {
            get => base.UserPrivacy;
            set
            {
                base.UserPrivacy = value;
                OnUserPrivacyChanged?.Invoke(value);
            }
        }

        public void PingXRControllersChanged() => OnXRControllerChanged?.Invoke(Controls, Movement, ContentFilterPreferences);

        public void PingUserHUDChanged() => OnUserHUDSettingsChanged?.Invoke(UserHUD);

        public void PingUserPrivacyChanged() => OnUserPrivacyChanged?.Invoke(UserPrivacy);

        #endregion
        // ---------------------------------------------------------------
        #region Crypto operations

        public string GetFingerprint(string fmt = null) => CryptoHelpers.ToString(fmt, UserSignPublicKey.Serialize());
        public static void TransmitMessage(byte[] data, PublicKey receiver, out CMSPacket messageData)
            => G.Client.CMH.TransmitMessage(data, receiver, out messageData);
        public static void ReceiveMessage(CMSPacket messageData, out byte[] data, out PublicKey signerPublicKey)
            => G.Client.CMH.ReceiveMessage(messageData, out data, out signerPublicKey);
        public static void Sign(byte[] data, out byte[] signature)
            => G.Client.CMH.Sign(data, out signature);

        #endregion
        // ---------------------------------------------------------------
        #region Social States

        public void SaveSocialStates(UserID userID, ulong state, Cid icon /* = null */)
        {
            bool dirty = false;

            if(Me.SocialList.TryGetValue(userID, out UserSocialEntryJSON oldstate_))
            {
                ulong oldstate = oldstate_.State;
                if (oldstate != state || oldstate != SocialState.None) dirty = true;
            }
            else if(state != SocialState.None) dirty = true;

            if (dirty)
            {
                if (state != SocialState.None)
                    Me.SocialList[userID] = new()
                    {
                        State = state,
                        Icon = icon
                    };
                else
                    Me.SocialList.Remove(userID);
                Save();
            }
        }

        public void SaveSocialStates(UserID userID, ulong state)
        {
            Cid icon = null;
            if(Me.SocialList.TryGetValue(userID, out UserSocialEntryJSON oldstate_))
                icon = oldstate_.Icon;

            SaveSocialStates(userID, state, icon);
        }

        /// <summary>
        /// Get the social relations list
        /// </summary>
        /// <param name="userID">the targeted user, null if everyone</param>
        /// <param name="p">Additional search limitations</param>
        /// <returns>The matching entries with the equivalent UserIDs</returns>
        public IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> GetSocialList(
            UserID userID = null, Func<KeyValuePair<UserID, UserSocialEntryJSON>, bool> p = null)
        {
            p ??= (x) => true;

            foreach (var socialListEntry in Me.SocialList)
                if((userID == null || socialListEntry.Key == userID) && 
                    p.Invoke(socialListEntry)) yield return socialListEntry;
        }

        public void UpdateSocialListEntry(UserID userID, Func<ulong, ulong> modification)
        {
            UserSocialEntryJSON state_ = Me.SocialList.ContainsKey(userID)
                ? Me.SocialList[userID]
                : new()
                {
                    State = SocialState.None,
                    Icon = null,
                };

            state_.State = modification(state_.State);

            SaveSocialStates(userID, state_.State, state_.Icon);
        }

#endregion
        // ---------------------------------------------------------------
        #region Save & Load

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                ConfigUtils.WriteTextConfig(PATH_CLIENT_SETTINGS, json);
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Failed to save user settings: {e.Message}");
            }
        }

        public static Client Load()
        {
            Client cs;

            try
            {
                string json = ConfigUtils.ReadTextConfig(PATH_CLIENT_SETTINGS);
                cs = JsonConvert.DeserializeObject<Client>(json);
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Failed to load user settings: {e.Message}");
                cs = new();
            }

            {

                SignKey userKey;
                if (cs.Me.UserSignKeyPair == null)
                {
                    userKey = SignKey.Generate();
                    userKey.ExportPrivateKey(out cs.Me.UserSignKeyPair);

                    cs.Save();
                }
                else
                    userKey = SignKey.ImportPrivateKey(cs.Me.UserSignKeyPair);

                cs.CMH = new(userKey);
            }

            return cs;
        }

        public static void UpdateServerPass(ServerInfo serverInfo, bool TOS, byte[] serverKey)
        {
            Client client = G.Client;

            client.ServerPasses.TryGetValue(serverInfo.SPKDBKey, out ServerPass sp);

            if (TOS) sp.PrivacyTOSHash = serverInfo.PrivacyTOSNoticeHash;
            if (serverKey != null) sp.ServerPublicKey = serverKey;

            client.ServerPasses[serverInfo.SPKDBKey] = sp;
            client.Save();
        }

        #endregion
    }
}