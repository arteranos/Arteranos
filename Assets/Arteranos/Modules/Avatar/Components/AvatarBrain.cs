/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using UnityEngine;
using Mirror;

using Arteranos.Core;
using System;
using Arteranos.UI;
using Random = UnityEngine.Random;
using Arteranos.Common.Cryptography;
using Ipfs.Cryptography.Proto;
using System.Collections.Generic;
using Arteranos.Services;
using Arteranos.Common;
using Ipfs;
using System.Linq;

namespace Arteranos.Avatar
{
    internal enum AVKeys : byte
    {
        _Invalid = 0,
        NetAppearanceStatus
    }

    public class AvatarBrain : NetworkBehaviour, IAvatarBrain
    {
        #region Interface
        public event Action<int> OnAppearanceStatusChanged;

        private int LocalAppearanceStatus = 0;

        private IHitBox HitBox = null;

        public uint NetID => netIdentity.netId;

        // Client-side settings
        public string AvatarCidString { get => m_AvatarCidString; private set => CmdPropagateAvatarURL(value); }

        public float AvatarHeight { get => m_AvatarHeight; private set => CmdPropagateAvatarHeight(value); }

        public UserPrivacy UserPrivacy { get => m_UserPrivacy; private set => CmdPropagateUserPrivacy(value); }

        public string UserIcon { get => m_UserIcon; private set => CmdPropagateUserIcon(value); }

        // Okay to use the setter. The SyncVar would yell at you if you fiddle with the privilege client-side
        public UserID UserID
        {
            get => m_userID;
            set
            {
                m_userID = value;
                if (!isClient) OnUserIDChanged(null, m_userID);
            }
        }

        public PublicKey AgreePublicKey { get => m_AgreePublicKey; set => m_AgreePublicKey = value; }

        public string Nickname { get => m_userID; }

        public ulong UserState { get => m_UserState; set => m_UserState = value; }

        public string Address { get => m_Address; set => m_Address = value; }

        public string DeviceID { get => m_DeviceID; set => m_DeviceID = value; }

        public int AppearanceStatus
        {
            get
            {
                // Join the local and the net-wide status
                return m_NetAppearanceStatus | LocalAppearanceStatus;
            }

            set
            {
                // Split the local and the net-wide status
                LocalAppearanceStatus = value & Avatar.AppearanceStatus.MASK_LOCAL;

                int newNetAS = value & ~Avatar.AppearanceStatus.MASK_LOCAL;

                if (m_NetAppearanceStatus != newNetAS)
                    // Spread the word...
                    CmdPropagateNetAppearanceStatus(newNetAS);
                else
                    // Or, follow through.
                    UpdateNetAppearanceStatus();
            }
        }

        public void SetAppearanceStatusBit(int ASBit, bool set)
        {
            if (set)
                AppearanceStatus |= ASBit;
            else
                AppearanceStatus &= ~ASBit;
        }

        public IAvatarBody Body => GetComponent<IAvatarBody>();

        public void NotifyBubbleBreached(IAvatarBrain touchy, bool isFriend, bool entered)
        {
            // Not for the desired sphere of influence
            if (isFriend != G.UserData.IsFriends(touchy.UserID)) return;

            touchy.SetAppearanceStatusBit(Avatar.AppearanceStatus.Bubbled, entered);
        }

        #endregion
        // ---------------------------------------------------------------
        #region Debug Messages

        private string PrefixMessage(object message)
        {
            string modestr = string.Empty;

            if (isServer && isClient) modestr += "Host";
            else if (isServer) modestr += "Server";
            else if (isClient) modestr += "Client";
            else modestr += "?";

            if (isLocalPlayer) modestr += ", Local";

            string objmsg = (message is string msgstr) ? msgstr : message.ToString();
            return UserID != null
                ? $"[{Nickname}][{modestr}] {objmsg}"
                : $"[???][{modestr}] {objmsg}";
        }

        public void LogDebug(object message) => Debug.unityLogger.Log(LogType.Log, PrefixMessage(message));

        public void LogWarning(object message) => Debug.unityLogger.Log(LogType.Warning, PrefixMessage(message));

        public void LogError(object message) => Debug.unityLogger.Log(LogType.Error, PrefixMessage(message));

        #endregion
        // ---------------------------------------------------------------
        #region Start & Stop
        public override void OnStartServer()
        {
            base.OnStartServer();
        }

        public override void OnStartClient()
        {
            IEnumerator GreetOtherUsers()
            {
                yield return new WaitForSeconds(0.5f);

                IEnumerable<UserID> q = from entry in G.NetworkStatus.GetOnlineUsers()
                                        select entry.UserID;

                List<UserID> users = q.ToList();

                for (int i = 0; i < users.Count; i++)
                {
                    UserID entry = users[i];
                    if (entry == G.Me.UserID) continue;

                    RelayFriendState(entry);

                    yield return new WaitForEndOfFrame();
                }
            }

            IEnumerator GreetOtherUser()
            {
                yield return new WaitForSeconds(0.5f);

                if (G.Me != null) G.Me.RelayFriendState(UserID);
            }

            Client cs = G.Client;

            base.OnStartClient();

            cs.OnAvatarChanged += CommitAvatarChanged;
            cs.OnUserPrivacyChanged += UPChanged;

            if (isLocalPlayer)
            {
                // That's me, set aside from the unwashed crowd. :)
                G.Me = this;

                DownloadClientSettings();

                _ = Factories.NewBubbleCoordinator(this);

                PostOffice.Load();

                StartCoroutine(DoTextMessageLoopCoroutine());

                // We've just arrived at a party, now do the shaking hands....
                StartCoroutine(GreetOtherUsers());
            }
            else
            {
                // Alien avatars get the hit capsules to target them to call up the nameplates.
                HitBox = Factories.NewHitBox(this);

                // We've not the last guest to arrive. Or they're just stragglers.
                StartCoroutine(GreetOtherUser());
            }
        }

        private void UPChanged(UserPrivacy x)
        {
            if (isLocalPlayer) UserPrivacy = x;
        }

        private void CommitAvatarChanged(string CidString, float Height)
        {
            if (isLocalPlayer)
            {
                AvatarCidString = CidString;
                AvatarHeight = Height;
            }
        }

        public override void OnStopClient()
        {
            // Maybe it isn't owned anymore, but it would be worse the other way round.
            if (isLocalPlayer) G.Me = null;

            G.Client.OnUserPrivacyChanged -= UPChanged;
            G.Client.OnAvatarChanged -= CommitAvatarChanged;

            base.OnStopClient();
        }

        /// <summary>
        /// Download the user's client settings to his avatar's brain, and announce
        /// the data to the server to spread it to the clones.
        /// </summary>
        private void DownloadClientSettings()
        {
            Client cs = G.Client;

            string avatarCidString = cs.AvatarCidString;

            // Schrödinger's cat and today's view of people's gender... all alike.
            // ¯\_(ツ)_/¯
            avatarCidString ??= (Random.Range(0, 100) < 50)
                    ? G.DefaultAvatar.Female
                    : G.DefaultAvatar.Male;

            CommitAvatarChanged(avatarCidString, cs.AvatarHeight);

            UserPrivacy = cs.UserPrivacy;

            UserIcon = (Cid)G.UserData;
        }

        #endregion
        // ---------------------------------------------------------------
        #region Running

        private IEnumerator DoTextMessageLoopCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);

                DoTextMessage();
            }
        }

        #endregion
        // ---------------------------------------------------------------
        #region Networking

        [SyncVar(hook = nameof(OnNetAppearanceStatusChanged))]
        private int m_NetAppearanceStatus = 0;

        [SyncVar(hook = nameof(OnUserIDChanged))]
        private UserID m_userID = null;

        [SyncVar]
        private PublicKey m_AgreePublicKey = null;

        [SyncVar]
        private UserPrivacy m_UserPrivacy = null;

        [SyncVar] // Default if someone circumvented the Authenticator and the Network Manager.
        private ulong m_UserState = (Core.UserState.Banned | Core.UserState.Exploiting);

        [SyncVar(hook = nameof(OnAvatarCidStringChanged))]
        private string m_AvatarCidString = null;

        [SyncVar(hook = nameof(OnAvatarHeightChanged))]
        private float m_AvatarHeight = 175;

        [SyncVar]
        private string m_UserIcon = null;

        // No [SyncVar] - server only for privacy reasons
        private string m_Address = null;

        // No [SyncVar] - server only for privacy reasons
        private string m_DeviceID = null;

        void Awake()
        {
            syncDirection = SyncDirection.ServerToClient;
        }

        public void OnNetAppearanceStatusChanged(int _1, int _2)
            => UpdateNetAppearanceStatus();

        private void OnAvatarCidStringChanged(string _1, string _2)
            => Body?.ReloadAvatar(m_AvatarCidString, m_AvatarHeight);

        private void OnAvatarHeightChanged(float _1, float _2)
            => Body?.ReloadAvatar(m_AvatarCidString, m_AvatarHeight);

        private void OnUserIDChanged(UserID _1, UserID userID)
            => name = $"Avatar [{userID.Nickname}][netId={netId}]";

        // Maybe I have to include a second component for the client-guided variables?
        [Command]
        private void CmdPropagateNetAppearanceStatus(int value) => m_NetAppearanceStatus = value;

        [Command]
        private void CmdPropagateUserPrivacy(UserPrivacy userPrivacy) => m_UserPrivacy = userPrivacy;

        [Command]
        private void CmdPropagateAvatarURL(string URL)
        {
            m_AvatarCidString = URL;
            if (!isClient) Body?.ReloadAvatar(m_AvatarCidString, m_AvatarHeight);
        }

        [Command]
        private void CmdPropagateAvatarHeight(float height)
        {
            m_AvatarHeight = height;
            if (!isClient) Body?.ReloadAvatar(m_AvatarCidString, m_AvatarHeight);
        }

        [Command]
        private void CmdPropagateUserIcon(string icon) => m_UserIcon = icon;

        [Command]
        private void CmdPerformEmote(string emojiName) => RpcPerformEmote(emojiName);

        [ClientRpc]
        private void RpcPerformEmote(string emojiName)
        {
            // Invisible users wouldn't show the emotes
            if (Avatar.AppearanceStatus.IsInvisible(AppearanceStatus)) return;

            LoopPerformEmoji(emojiName);
        }

        #endregion
        // ---------------------------------------------------------------
        #region Appearance Status

        private void UpdateNetAppearanceStatus()
        {
            // Special case - self-modifying appearance status, like making yourself invisible,
            // and, to a lesser extent, self-muting.
            if (HitBox != null) HitBox.Interactable = !Avatar.AppearanceStatus.IsInvisible(AppearanceStatus);
            if (Body != null) Body.Invisible = Avatar.AppearanceStatus.IsInvisible(AppearanceStatus);

            OnAppearanceStatusChanged?.Invoke(AppearanceStatus);
        }


        #endregion
        // ---------------------------------------------------------------
        #region CTCP
        private void SendCTCPacket(IAvatarBrain receiver, CTCPacket packet)
        {
            if (!receiver?.gameObject)
            {
                Debug.LogWarning($"Discarding CTCP for an already logged out user");
                return;
            }

            // Wrap up, encrypt and send
            try
            {
                packet.sender = UserID;

                Client.TransmitMessage(packet.Serialize(), receiver.AgreePublicKey, out CMSPacket messageData);

                CmdRouteCTCP(new CTCPacketEnvelope()
                {
                    receiver = receiver.UserID,
                    CTCPayload = messageData
                });
            }
            catch { }
        }

        [Command]
        private void CmdRouteCTCP(CTCPacketEnvelope envelope)
        {
            AvatarBrain receiver = G.NetworkStatus.GetOnlineUser(envelope.receiver) as AvatarBrain;
            if (receiver == null)
            {
                Debug.LogWarning($"Discarding CTCP for nonexistent/offline user {envelope.receiver}");
                return;
            }
            NetworkIdentity netid = receiver.gameObject.GetComponent<NetworkIdentity>();

            Debug.Log($"[Server] Routing CTCP to {(string)envelope.receiver} ({netid.netId})");

            receiver.TargetReceiveCTCPacket(netid.connectionToClient, envelope);
        }

        [TargetRpc]
        private void TargetReceiveCTCPacket(NetworkConnectionToClient _, CTCPacketEnvelope envelope)
        {
            Debug.Log($"[Client] Received CTCP to {(string)envelope.receiver}");
            ReceiveCTCPacket(envelope);
        }

        public void ReceiveCTCPacket(CTCPacketEnvelope envelope)
        {
            CTCPacket packet;
            try
            {
                Client.ReceiveMessage(envelope.CTCPayload, out byte[] data, out PublicKey signerPublicKey);
                packet = CTCPacket.Deserialize(data);

                if (packet.sender != signerPublicKey)
                    throw new Exception($"Supposed sender {packet.sender} doesn't match its key");

                else if (packet is CTCPTextMessage packetTxt) ReactReceiveTextMessage(packetTxt);
                else throw new InvalidOperationException("Unknown CTC Packet, discarded");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            // NOTREACHED
        }

        #endregion
        // ---------------------------------------------------------------
        #region Text message reception

        private volatile IDialogUI m_txtMessageBox = null;

        private volatile IAvatarBrain replyTo = null;

        public void SendTextMessage(IAvatarBrain receiver, string text)
        {
            // Maybe the intended receiver logged off while you tried to send a goodbye message.
            if (receiver == null) return;

            SendCTCPacket(receiver, new CTCPTextMessage()
            {
                text = text
            });
        }

        private void ReactReceiveTextMessage(CTCPTextMessage received)
        {
            UserID sender = received.sender;

            PostOffice.EnqueueIncoming(sender, received.text);
            PostOffice.Save();
        }

        private bool IsTextMessageOccupied()
        {
            bool busy = false;

            // "I'm swamped with the messages."
            busy |= m_txtMessageBox != null;

            // "Dammit! Not right now!"
            busy |= G.Client.UserPrivacy.Visibility != Core.Visibility.Online;
            return busy;
        }

        private void DoTextMessage()
        {
            if (replyTo != null)
            {
                Factory.NewTextMessage(replyTo);
                replyTo = null;
                return;
            }

            if (IsTextMessageOccupied()) return;

            PostOffice.DequeueIncoming(null, out MessageEntryJSON message);

            if (message == null) return;

            PostOffice.Save();

            IAvatarBrain sender = G.NetworkStatus.GetOnlineUser(message.UserID);

            ShowTextMessage(sender, message.Nickname, message.Text);
        }

        public async void ShowTextMessage(IAvatarBrain sender, string Nickname, string text)
        {
            string[] replyText = { "OK", "Reply" };
            string[] okText = { "OK" };

            m_txtMessageBox = Factory.NewDialog();

            int rc = await m_txtMessageBox.PerformDialogAsync(
                $"Message from {Nickname}:\n\n" +
                text,
                (sender == null) ? okText : replyText);

            m_txtMessageBox = null;

            // "Goodbye!" -- "Bye!" -- "Rest we...." Drat, logged off....
            if (rc != 0 && sender != null)
                replyTo = sender;
        }
        #endregion
        // ---------------------------------------------------------------
        #region Emotes performance

        private void LoopPerformEmoji(string emojiName)
        {
            static IEnumerator CleanupEmojiPS(ParticleSystem ps)
            {
                yield return new WaitForSeconds(5);

                Destroy(ps.gameObject);
            }

            ParticleSystem ps = EmojiSettings.Load().GetEmotePS(emojiName);

            if (ps == null) return;

            ps.transform.SetParent(transform, false);

            // A little bit 'up' (relative to the user)
            Vector3 offset = transform.rotation * Vector3.up * (Body.AvatarMeasures.FullHeight * 1.10f);
            ps.transform.SetLocalPositionAndRotation(offset, Quaternion.identity);

            StartCoroutine(CleanupEmojiPS(ps));
        }

        public void PerformEmote(string emoteName) => CmdPerformEmote(emoteName);

        #endregion
        // ---------------------------------------------------------------
        #region Server User Management

        public bool IsAbleTo(UserCapabilities cap, IAvatarBrain target)
        {
            bool friends = (target != null) && G.UserData.IsFriends(target.UserID);

            bool isAnyAdmin = Core.UserState.IsSAdmin(UserState) || Core.UserState.IsWAdmin(UserState);

            bool targetIsAnyAdmin = target != null && (Core.UserState.IsSAdmin(target.UserState) || Core.UserState.IsWAdmin(target.UserState));

            bool userHigher = target == null ||
                (UserState & Core.UserState.GOOD_MASK) > (target.UserState & Core.UserState.GOOD_MASK);

            return cap switch
            {
                // User can enable flying if the current(!) server permits it
                // Maybe add G.Client.PingXRControllersChanged();
                // in the world transition, too.
                UserCapabilities.CanEnableFly => isAnyAdmin || (SettingsManager.ActiveServerData?.Permissions.Flying ?? true),

                // Maybe some accesss restriction for requesting friendships...?
                UserCapabilities.CanFriendUser => !friends,

                // Every user can mute others on their own clients, but they cannot mute admins.
                UserCapabilities.CanMuteUser => !targetIsAnyAdmin,

                // Users should not block admins. Friends should not block friends, too.
                UserCapabilities.CanBlockUser => !friends && !targetIsAnyAdmin,

                // Gag User is reserved to to admins. Note that they _can_ gag higher tiers in
                // case the don't notice when their microphone is noisy.
                UserCapabilities.CanGagUser => isAnyAdmin,

                // Kicking users is reserved to any admin, but only the lower tiers
                UserCapabilities.CanKickUser => isAnyAdmin && userHigher,

                // Banning users is for server admin only,
                UserCapabilities.CanBanUser => Core.UserState.IsSAdmin(UserState) && userHigher,

                // Edit server's user base (server admins and its deputies)
                UserCapabilities.CanAdminServerUsers => Core.UserState.IsSAdmin(UserState),

                // Edit server configuration itself (server admins and its deputies)
                UserCapabilities.CanEditServer => Core.UserState.IsSAdmin(UserState),

                // Edit world itself, both the world authors and the server admin.
                UserCapabilities.CanEditWorld => isAnyAdmin,

                // Admins can view user IDs, even if the users don't want to.
                // Especially for the risk of user impersonation
                UserCapabilities.CanViewUsersID => isAnyAdmin || (target != null && Core.Utils.IsPermitted(target.UserID, target.UserPrivacy.UIDVisibility)),

                // Admin can send text, even to higher tiers, for a reason
                UserCapabilities.CanSendText => isAnyAdmin || (target != null && Core.Utils.IsPermitted(target.UserID, target.UserPrivacy.TextReception)),

                // Both server and world admins can initiate the world transition
                UserCapabilities.CanInitiateWorldTransition => isAnyAdmin,

                _ => throw new NotImplementedException(),
            };
        }

        #endregion
        // ---------------------------------------------------------------
        #region Interacting World Objects
        public void GotObjectClicked(GameObject clicked)
            => CmdGotObjectClicked(GetEnclosingObject(clicked));
        public void GotObjectClicked(List<Guid> clicked)
            => CmdGotObjectClicked(clicked);
        public void GotObjectGrabbed(GameObject grabbed)
            => CmdGotObjectGrabbed(GetEnclosingObject(grabbed));
        public void GotObjectGrabbed(List<Guid> grabbed)
            => CmdGotObjectGrabbed(grabbed);
        public void GotObjectReleased(GameObject released, Vector3 detachVelocity, Vector3 detachAngularVelocity)
            => CmdGotObjectReleased(GetEnclosingObject(released), detachVelocity, detachAngularVelocity);
        public void GotObjectReleased(List<Guid> released, Vector3 detachVelocity, Vector3 detachAngularVelocity)
            => CmdGotObjectReleased(released, detachVelocity, detachAngularVelocity);
        public void GotObjectHeld(GameObject holding, Vector3 position, Quaternion rotation)
            => CmdGotObjectHeld(GetEnclosingObject(holding), position, rotation);
        public void GotObjectHeld(List<Guid> holding, Vector3 position, Quaternion rotation)
            => CmdGotObjectHeld(holding, position, rotation);
        public void ManageAuthorityOf(GameObject GO, bool auth)
            => CmdManageAuthorityOf(GetEnclosingObject(GO), auth);

        [Command]
        private void CmdGotObjectClicked(GameObject clicked)
            => G.WorldEditorData.GotWorldObjectClicked(GetEnclosedObject(clicked));

        [Command]
        private void CmdGotObjectClicked(List<Guid> clicked)
            => G.WorldEditorData.GotWorldObjectClicked(clicked);

        [Command]
        private void CmdGotObjectGrabbed(GameObject grabbed)
            => G.WorldEditorData.GotWorldObjectGrabbed(GetEnclosedObject(grabbed));

        [Command]
        private void CmdGotObjectGrabbed(List<Guid> grabbed)
            => G.WorldEditorData.GotWorldObjectGrabbed(grabbed);

        [Command]
        private void CmdGotObjectReleased(GameObject released, Vector3 detachVelocity, Vector3 detachAngularVelocity)
            => G.WorldEditorData.GotWorldObjectReleased(GetEnclosedObject(released), detachVelocity, detachAngularVelocity);

        [Command]
        private void CmdGotObjectReleased(List<Guid> released, Vector3 detachVelocity, Vector3 detachAngularVelocity)
            => G.WorldEditorData.GotWorldObjectReleased(released, detachVelocity, detachAngularVelocity);

        [Command]
        private void CmdGotObjectHeld(GameObject holding, Vector3 position, Quaternion rotation)
            => G.WorldEditorData.GotWorldObjectHeld(GetEnclosedObject(holding), position, rotation);

        [Command]
        private void CmdGotObjectHeld(List<Guid> holding, Vector3 position, Quaternion rotation)
            => G.WorldEditorData.GotWorldObjectHeld(holding, position, rotation);

        [Command]
        private void CmdManageAuthorityOf(GameObject networkGO, bool auth)
        {
            // Object lost -- maybe destroyed.
            if (!networkGO) return;

            networkGO.TryGetComponent(out IEnclosingObject o);
            o.ChangeAuthority(gameObject, auth);
        }

        private GameObject GetEnclosedObject(GameObject networkGO)
            => networkGO && networkGO.TryGetComponent(out IEnclosingObject o) ? o.EnclosedObject : networkGO;

        private GameObject GetEnclosingObject(GameObject coreGO)
            => coreGO.TryGetComponent(out IEnclosedObject o) ? o.EnclosingObject : coreGO;

        #endregion
        // ---------------------------------------------------------------
        #region Friend/Block state handling

        private string DisplayState(bool? state) => state switch
        {
            false => "disfavored",
            null => "neutral",
            true => "favored"
        };

        public void RelayFriendState(UserID target)
        {
            if (!isLocalPlayer) throw new InvalidOperationException("Internal error: not owner");

            bool? state = G.UserData.IsStated(target, true);
            // Debug.Log($"{(string)UserID} sends {(string)target} the state: {DisplayState(state)}");

            // Update 'blocking' state on the alien avatar: true if combined status is 'false', false otherwise.
            // Target User may be actually offline.
            IAvatarBrain targetUser = G.NetworkStatus.GetOnlineUser(target);
            if (targetUser == null) return;

            CmdRelayFriendState(targetUser.gameObject, state);
            targetUser.SetAppearanceStatusBit(Avatar.AppearanceStatus.Blocking, state == false);
        }

        [Command]
        private void CmdRelayFriendState(GameObject target, bool? state)
        {
            AvatarBrain targetUser = target.GetComponent<AvatarBrain>();

            // Target User is definitely offline, so stop here.
            if (target == null) return;

            // Debug.Log($"Relaying from {(string) UserID} to {(string) targetUser.UserID} the status: {DisplayState(state)}");

            NetworkIdentity netid = target.GetComponent<NetworkIdentity>();
            targetUser.ReceiveFriendState(netid.connectionToClient, gameObject, state);
        }

        private void ReceiveFriendState(NetworkConnectionToClient target, GameObject source, bool? state)
            => TargetReceiveFriendState(target, source, state);

        [TargetRpc]
        private void TargetReceiveFriendState(NetworkConnectionToClient target, GameObject source, bool? state)
        {
            _ = target; // For TargetRpc's client targeting

            IAvatarBrain sourceUser = source.GetComponent<IAvatarBrain>();

            UserID sourceID = sourceUser.UserID;

            // TargetRpc broadcasts to all copies of a user throughout all clients, both local and alien.
            if (!isLocalPlayer)
            {
                Debug.LogWarning($"{(string)UserID} got from {(string)sourceID} the status: {DisplayState(state)} - BOGUS");
                return;
            }

            // Debug.Log($"{(string)UserID} got from {(string)sourceID} the status: {DisplayState(state)}");

            bool changed = false;

            switch (state)
            {
                case false:
                    changed = G.UserData.ReceiveBlock(sourceID, true);
                    break;
                case null:
                    changed = G.UserData.ReceiveBlock(sourceID, false);
                    changed |= G.UserData.ReceiveFriend(sourceID, false);
                    break;
                case true:
                    changed = G.UserData.ReceiveFriend(sourceID, true);
                    break;
            }

            // Update 'being blocked by' state in the alien avatar: true if combined status is 'false', false otherwise.
            // We got it so far to have the source, then the target still being online, but the _source_ may have
            // (rage)quit. Well, no matter.
            sourceUser.SetAppearanceStatusBit(Avatar.AppearanceStatus.Blocked, state == false);

            // Announce the reactions from source's change of views.
            // Common example: "You block me?! Then you're not a friend anymore, Bwaaaah!"
            if (changed) RelayFriendState(sourceID);
        }

        #endregion
    }
}
