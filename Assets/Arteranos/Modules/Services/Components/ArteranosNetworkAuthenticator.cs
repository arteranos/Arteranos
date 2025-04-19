/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Net;
using Mirror;

using Arteranos.Core;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Version = Arteranos.Core.Version;
using Random = UnityEngine.Random;
using System.Collections;
using System.Linq;
using Arteranos.UI;
using System.Collections.Concurrent;
using Ipfs.Cryptography.Proto;
using ProtoBuf;
using System.IO;
using Arteranos.Core.Cryptography;
using Ipfs;

/*
    Documentation: https://mirror-networking.gitbook.io/docs/components/network-authenticators
    API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkAuthenticator.html
*/

namespace Arteranos.Services
{
    public class ArteranosNetworkAuthenticator : NetworkAuthenticator
    {
        #region Messages

        internal struct AuthGreetingMessage : NetworkMessage
        {
            public Version ServerVersion;
            public string ServerName;
            public PublicKey ServerSignPublicKey;
            public PublicKey ServerAgreePublicKey;
            public int SequenceNumber;
        }

        [ProtoContract]
        internal struct AuthRequestPayload // Signed and encrypted
        {
            [ProtoMember(1)]
            public int SequenceNumber;

            [ProtoMember(2)]
            public string Nickname;

            [ProtoMember(3)]
            public string deviceUID;

            [ProtoMember(4)]
            public PublicKey ClientSignPublicKey; // It's self-signed, okay?!

            [ProtoMember(5)]
            public PublicKey ClientAgreePublicKey;

            [ProtoMember(6)]
            public string UserIconCid;
        }

        internal struct AuthRequestMessage : NetworkMessage
        {
            public Version ClientVersion;
            public PublicKey ClientAgreePublicKey;

            public CMSPacket Payload; // As AuthRequestPayload, signed with Client, encrypted with Client and Server's keys.
        }

        [ProtoContract]
        internal struct AuthResponsePayload
        {
            [ProtoMember(1)]
            public HttpStatusCode status;

            [ProtoMember(2)]
            public string message;

            [ProtoMember(3)]
            public ServerJSON ServerSettings;

            [ProtoMember(4)]
            public ulong UserState;

            [ProtoMember(5)]
            public PublicKey ServerAgreePublicKey;
        }

        internal struct AuthResponseMessage : NetworkMessage
        {
            public PublicKey ServerAgreePublicKey;
            public CMSPacket Payload; // As AuthResponsePayload, signed with Server
        }
        #endregion

        #region Utilities

        private readonly ConcurrentQueue<ResponseQueueEntry> ResponseQueue = new();

        private readonly Dictionary<int, ChallengeListEntry> ChallengeList = new();


        /// <summary>
        /// Get (not necessarily cryptographically strong) random bytes for the challenge
        /// </summary>
        /// <param name="numBytes">How many bytes to get</param>
        public byte[] GetChallengeBytes(int numBytes)
        {
            byte[] challengeBytes = new byte[numBytes];

            for(int i = 0; i < challengeBytes.Length; i++)
                challengeBytes[i] = (byte) Random.Range(0, 255);

            return challengeBytes;
        }

        private void Update()
        {
            if(!ResponseQueue.TryDequeue(out ResponseQueueEntry e)) return;

            EmitAuthResponse(e);
        }

        #endregion

        #region Start & Stop

        public override void OnStartServer()
        {
            // register a handler for the authentication request we expect from client
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);

            StartCoroutine(ScrubChallenges());
        }

        public override void OnStopServer()
        {
            // unregister the handler for the authentication request
            NetworkServer.UnregisterHandler<AuthRequestMessage>();

            StopAllCoroutines();
        }

        public override void OnStartClient()
        {
            // register a handler for the server's initial greeting message
            NetworkClient.RegisterHandler<AuthGreetingMessage>(OnAuthGreetingMessage, false);

            // register a handler for the authentication response we expect from server
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        public override void OnStopClient()
        {
            // unregister a handler for the server's initial greeting message
            NetworkClient.UnregisterHandler<AuthGreetingMessage>();

            // unregister the handler for the authentication response
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        #endregion

        #region Server

        internal struct ChallengeListEntry
        {
            public DateTime time;
        }

        internal struct ResponseQueueEntry
        {
            public NetworkConnectionToClient conn;
            public AuthRequestPayload request;
            public AuthResponsePayload response;
        }

        // Every two seconds, look for the outdated login attempts to remove them.
        private IEnumerator ScrubChallenges()
        {
            while(true)
            {
                yield return new WaitForSeconds(2);

                DateTime now = DateTime.Now;

                int[] numbers = (from entry in ChallengeList
                                 where entry.Value.time < now
                                 select entry.Key).ToArray();

                foreach(int number in numbers)
                    ChallengeList.Remove(number);
            }
        }

        /// <summary>
        /// Called on server from OnServerConnectInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public override void OnServerAuthenticate(NetworkConnectionToClient conn) 
        {
            // Flooded. Too many login attempts at the same time.
            if(ChallengeList.Count > 12000)
            {
                Debug.LogWarning($"Login attempt flood: Discarding connection from {conn.address}");
                // conn.Disconnect();
                return;
            }
                
            int sequence = Random.Range(0, 1000000);
            while(ChallengeList.TryGetValue(sequence, out _))
                sequence = Random.Range(0, 1000000);

            // Allow sixty seconds between the greeting message and the client's reaction.
            ChallengeListEntry entry = new()
            {
                time = DateTime.Now + TimeSpan.FromSeconds(60),
            };

            // Take the first step to authenticate.
            Server ss = G.Server;

            AuthGreetingMessage authGreetingMessage = new()
            {
                ServerVersion = Version.Load(),
                ServerName = ss.Name,
                ServerSignPublicKey = ss.ServerSignPublicKey,
                ServerAgreePublicKey = ss.ServerAgrPublicKey,
                SequenceNumber = sequence
            };

            conn.Send(authGreetingMessage);

            ChallengeList.Add(sequence, entry);
        }

        /// <summary>
        /// Called on server when the client's AuthRequestMessage arrives
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        /// <param name="msg">The message payload</param>
        private void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
            => Task.Run(() =>
            {
                try
                {
                    DecideAuthenthicity(conn, msg);
                }
                catch(Exception e)
                {
                    Debug.LogException(e);
                    ServerReject(conn);
                }
            });

        private void DecideAuthenthicity(NetworkConnectionToClient conn, AuthRequestMessage encryptedMsg)
        {
            // DEBUG
            // System.Threading.Thread.Sleep(3000);

            // if(connectionsPendingDisconnect.Contains(conn)) return;

            AuthResponsePayload response = default;
            AuthRequestPayload request = default;
            try
            {
                Server.ReceiveMessage(encryptedMsg.Payload, out byte[] payloadBlob, out PublicKey signerPublicKey);
                
                using MemoryStream ms = new(payloadBlob);
                request = Serializer.Deserialize<AuthRequestPayload>(ms);

                // Wait, what?!
                if (signerPublicKey != request.ClientSignPublicKey)
                    throw new Exception("Wrong signature key");

                if (encryptedMsg.ClientAgreePublicKey != request.ClientAgreePublicKey)
                    throw new Exception("Wrong agreement key");

                response.status = 0;
            }
            catch (Exception)
            {
                response = new()
                {
                    status = HttpStatusCode.Unauthorized,
                    message = $"Signature check failed.",
                };
            }

            Version clientVersion = Version.Parse(encryptedMsg.ClientVersion.MMPB);

            if (clientVersion < Version.MinVersion)
            {
                // Insufficient version
                response = new()
                {
                    status = HttpStatusCode.NotAcceptable,
                    message = $"Your client is outdated.\nVersion {Version.VERSION_MIN} required,\nPlease update.",
                };
            }
            else if (!ChallengeList.TryGetValue(request.SequenceNumber, out _))
            {
                // Authentication attempt either out-of-band or expired. Or it's a replay attack.
                response = new()
                {
                    status = HttpStatusCode.RequestTimeout,
                    message = $"Request timed out",
                };
            }
            else if(response.status == 0)
            {
                // All checks pan out.
                response = new()
                {
                    status = HttpStatusCode.OK,
                    message = "OK",
                    // Add the server settings and the signature of the client's public key.
                    // IMPORTANT: Need type ServerJSON, because inherited types make
                    // ProtoBuf choke on them.
                    ServerSettings = new ServerJSON(G.Server)
                };

                ServerUserState query = new()
                {
                    userID = new UserID(request.ClientSignPublicKey, request.Nickname, request.UserIconCid),
                    address = conn.address,
                    deviceUID = request.deviceUID
                };

                IEnumerable<ServerUserState> q = G.ServerUsers.FindUsersOR(query);

                ulong aggregated = 0;
                foreach (ServerUserState user in q) { aggregated |= user.userState; }

                // Conflicting server user base's entries. Maybe a user's deep fall or a
                // ban evasion.
                // But, for example, one user could be a world administrator due to the authoring of a world,
                // and a server administrator as his origin as the server.... perfectly valid.
                //
                // But, banned means revoking all of the privileges, of course.
                if(UserState.IsBanned(aggregated))
                {
                    aggregated &= ~UserState.GOOD_MASK;

                    if (q.Count() > 1)
                        aggregated |= UserState.BanEvading;

                    response.status = HttpStatusCode.Forbidden;
                    response.message = "You are banned from this server.";
                }

                response.UserState = aggregated;
            }

            // Anyway, expire the thallenge.
            ChallengeList.Remove(request.SequenceNumber);

            // Hand the authentication response to send to the main thread
            ResponseQueue.Enqueue(new()
            {
                conn = conn,
                request = request,
                response = response,
            });
        }

        private void EmitAuthResponse(ResponseQueueEntry e)
        {
            Server ss = G.Server;

            using MemoryStream ms = new();
            Serializer.Serialize(ms, e.response);
            Server.TransmitMessage(ms.ToArray(), e.request.ClientAgreePublicKey, out CMSPacket encryptedPayload);

            e.conn.Send(new AuthResponseMessage()
            {
                Payload = encryptedPayload,
                ServerAgreePublicKey = ss.ServerAgrPublicKey
            });

            bool accepted = e.response.status == HttpStatusCode.OK;

            Debug.Log($"{(accepted ? "Accept" : "Reject")}ed connection from {e.conn.address}");
            if(!accepted)
                Debug.Log($"  Reason: {e.response.status}, Message:\n{e.response.message}");

            if(accepted)
            {
                ArteranosNetworkManager.AuthSequence seq = new()
                {
                    request = e.request,
                    response = e.response
                };

                // Transfer the AuthResponse for the Manager to initialize the avatar.
                ArteranosNetworkManager.Instance.AddResponseMessage(e.conn.connectionId, seq);
                ServerAccept(e.conn);
            }
            else StartCoroutine(DelayedServerReject(e.conn));
        }

        private IEnumerator DelayedServerReject(NetworkConnectionToClient conn)
        {
            yield return new WaitForSeconds(2);

            ServerReject(conn);
        }

        #endregion

        #region Client

        /// <summary>
        /// Called on client from OnClientConnectInternal when a client needs to authenticate
        /// </summary>
        public override void OnClientAuthenticate()
        {
            // Do nothing. Until we got the server's greeting message, we cennot do anything.
        }

        /// <summary>
        /// Called on client when the server's initial greeting message arrives
        /// </summary>
        /// <param name="msg">Self explanatory</param>
        private void OnAuthGreetingMessage(AuthGreetingMessage msg)
        {
            MultiHash RemotePeerId = SettingsManager.GetServerConnectionData();

            string key = RemotePeerId.ToString();

            Debug.Log($"[Client] Server PeerID : {key}");
            Debug.Log($"[Client] Server name   : {msg.ServerName}");
            Debug.Log($"[Client] Server version: {msg.ServerVersion.Full}");

            Client cs = G.Client;

            byte[] pubKeyData = msg.ServerSignPublicKey.Serialize();

            if(!cs.ServerPasses.TryGetValue(key, out ServerPass sp))
            {
                // New server encountered. Yay!
                cs.ServerPasses.Add(key, new()
                {
                    PrivacyTOSHash = null,
                    ServerPublicKey = pubKeyData
                });
                SubmitAuthRequest(msg);
                cs.Save();
                return;
            }
            else if(sp.ServerPublicKey == null)
            {
                // Somewhat known, because we dealt with the custom TOS, and never connected to it yet.
                sp.ServerPublicKey = pubKeyData;
                cs.ServerPasses[key] = sp;
                SubmitAuthRequest(msg);
                cs.Save();
                return;
            }
            else if(pubKeyData.SequenceEqual(sp.ServerPublicKey))
            {
                // Known server, everything is okay.
                SubmitAuthRequest(msg);
                return;
            }
            else
            {
                // Something is very, very wrong....!
                IDialogUI dialog = Factory.NewDialog();

                dialog.Text =
                    "!!! SERVER KEY CHANGED !!!\n\n" +
                    "It's possible that someone is doing\n" +
                    "something very nasty!\n\n" +
                    "Or, the server's adminitrator being\n" +
                    "careless about his machine.\n\n"+
                    "Only accept it if you're really,\n" +
                    "absolutely positive, sure about it!\n\n";

                dialog.Buttons = new[]
                {
                    "Go offline",
                    "Accept"
                };

                dialog.OnDialogDone += (rc) => OnHostKeyChangedResponse(rc, msg);
            }
        }

        private void OnHostKeyChangedResponse(int rc, AuthGreetingMessage msg)
        {
            if(rc == 0)
            {
                ClientReject();
                return;
            }

            Client cs = G.Client;
            MultiHash RemotePeerId = SettingsManager.GetServerConnectionData();

            string key = $"{RemotePeerId}";

            cs.ServerPasses.TryGetValue(key, out ServerPass sp);

            sp.ServerPublicKey = msg.ServerSignPublicKey.Serialize();

            cs.ServerPasses[key] = sp;
            cs.Save();

            SubmitAuthRequest(msg);
        }

        private static void SubmitAuthRequest(AuthGreetingMessage msg)
        {
            Client cs = G.Client;

            AuthRequestPayload authRequestPayload = new()
            {
                SequenceNumber = msg.SequenceNumber,

                Nickname = cs.Me.Nickname,
                deviceUID = SystemInfo.deviceUniqueIdentifier,
                ClientSignPublicKey = cs.UserSignPublicKey,
                ClientAgreePublicKey = cs.UserAgrPublicKey,
                UserIconCid = cs.Me.UserIconCid
            };

            using MemoryStream ms = new();
            Serializer.Serialize(ms, authRequestPayload);
            Client.TransmitMessage(ms.ToArray(), msg.ServerAgreePublicKey, out CMSPacket encryptedPayload);

            NetworkClient.Send(new AuthRequestMessage()
            {
                ClientVersion = Version.Load(),
                ClientAgreePublicKey = cs.UserAgrPublicKey,
                Payload = encryptedPayload
            });
        }

        /// <summary>
        /// Called on client when the server's AuthResponseMessage arrives
        /// </summary>
        /// <param name="msg">The message payload</param>
        private void OnAuthResponseMessage(AuthResponseMessage encyptedMsg)
        {
            Client.ReceiveMessage(encyptedMsg.Payload, out byte[] payloadBlob, out _);

            using MemoryStream ms = new(payloadBlob);
            AuthResponsePayload msg = Serializer.Deserialize<AuthResponsePayload>(ms);

            if (msg.status != HttpStatusCode.OK)
            {
                G.ConnectionManager.DeliverDisconnectReason(msg.message);

                // Just only some formality, the server will disconnect anyway.
                ClientReject();
            }
            else
            {
                SettingsManager.CurrentServer = msg.ServerSettings;

                // Update the sserver's restrictions, like flying.
                G.Client.PingXRControllersChanged();

                // Authentication has been accepted
                ClientAccept();
            }
        }

        #endregion
    }
}
