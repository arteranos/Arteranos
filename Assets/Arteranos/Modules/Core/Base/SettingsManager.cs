/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Avatar;
using Arteranos.Common;
using Arteranos.Services;
using Ipfs;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arteranos.Core
{
    public abstract class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance;

        public Blueprints Blueprints;
        public ActionItems ActionItems;

        private CommandLine Command;

        public static bool StartupTrigger { get; private set; } = false;

        public static ServerJSON CurrentServer { get; set; } = null;

        public static Transform Purgatory { get; private set; }

        public static string DefaultTOStext { get; private set; } = null;


        public static ServerJSON ActiveServerData =>
            G.NetworkStatus.GetOnlineLevel() == OnlineLevel.Client
            ? CurrentServer
            : G.Server;

        protected virtual void Awake()
        {
            BP.I = Blueprints;
            ActionRegistry.Register(ActionItems);

            SetupPurgatory();

            ParseSettingsAndCmdLine();

            DefaultTOStext = BP.I.PrivacyTOSNotice.text;
            G.DefaultAvatar.Male = null;
            G.DefaultAvatar.Female = null;

            SetupWorldObjectRoot();
        }

        protected abstract void OnDestroy();

        private void ParseSettingsAndCmdLine()
        {
            bool GetCmdArg(string key, out string val)
            {
                return CommandLine.Commands.TryGetValue(key, out val);
            }

            bool GetBoolArg(string key, bool def = false)
            {
                if(GetCmdArg(key, out _))
                    return true;

                if(GetCmdArg("-no" + key, out _))
                    return false;

                return def;
            }

            if (Client.Load() == null)
                throw new Exception("Internal error - cannot set up client data");

            if (Server.Load() == null)
                throw new Exception("Internal error - cannot set up server data");
                
            Command = ScriptableObject.CreateInstance<CommandLine>();

            Command.GetCommandlineArgs();

            // Only for fiddling with dedicated servers.
            if (ConfigUtils.Unity_Server)
            {
                G.CommandLineOptions.ClearServerUserBase = GetBoolArg("--clear-sub", false);
                if (GetCmdArg("--add-root-users", out string uidlist))
                {
                    string[] parts = uidlist.Split(":");
                    G.CommandLineOptions.AddServerAdmins = parts.ToList();
                }

                if(GetCmdArg("--set-server-name", out string newservername))
                    G.CommandLineOptions.NewServerName = newservername;
            }

            G.ServerUsers = ServerUserBase.Load();

            if(G.CommandLineOptions.NewServerName != null)
            {
                G.Server.Name = G.CommandLineOptions.NewServerName;
                G.Server.Save();
            }

            if (CommandLine.PlainArgs.Count > 0)
            {
                // arteranos://[<PeerID>]/[<WorldCid>]

                string[] parts = CommandLine.PlainArgs[0].Split('/');
                if(parts.Length >= 4 && parts[0] == "arteranos:")
                {
                    G.CommandLineOptions.DesiredPeerID = !string.IsNullOrEmpty(parts[2]) ? parts[2] : null;
                    G.CommandLineOptions.DesiredWorldCid = (!string.IsNullOrEmpty(parts[3]) ? parts[3] : null);

                    if(G.CommandLineOptions.DesiredPeerID != null || G.CommandLineOptions.DesiredWorldCid != null) 
                        StartupTrigger = true;
                }
            }

            G.Client.VRMode = GetBoolArg("-vr", G.Client.VRMode);
        }

        private void SetupPurgatory()
        {
            Purgatory = new GameObject("_Purgatory").transform;
            Purgatory.position = new Vector3(0, -9000, 0);
            DontDestroyOnLoad(Purgatory.gameObject);
        }

        public static GameObject SetupWorldObjectRoot(bool onlyExisting = false)
        {
            // If the world object root doesn't exist yet, create one now.
            GameObject gameObject = GameObject.FindGameObjectWithTag("WorldObjectsRoot");
            if (!gameObject && !onlyExisting)
            {
                // Debug.Log("Setting up World Object Root");
                gameObject = Instantiate(BP.I.WorldEdit.WorldObjectRoot);

                //// Needed to persist even the scene rebuild, only on entering transition
                //DontDestroyOnLoad(gameObject);
            }

            return gameObject;
        }

        /// <summary>
        /// Returns the connection data of the remote server (in client mode) or the
        /// local server in the server and host mode. Nulls if offline.
        /// </summary>
        /// <returns>Peer ID, the own one or the remote</returns>
        /// <exception cref="NotImplementedException"></exception>
        public static MultiHash GetServerConnectionData()
        {
            OnlineLevel ol = G.NetworkStatus.GetOnlineLevel();

            return ol switch
            {
                OnlineLevel.Offline => null,
                OnlineLevel.Client => G.NetworkStatus.RemotePeerId,
                OnlineLevel.Server => G.IPFSService.Self.Id,
                OnlineLevel.Host => G.IPFSService.Self.Id,
                _ => throw new NotImplementedException()
            };
        }

        protected abstract void EmitToClientCTSPacket_(CTSPacket packet, IAvatarBrain to = null);
        protected abstract void EmitToServerCTSPacket_(CTSPacket packet);

        protected abstract event Action<UserID, ServerUserState> OnClientReceivedServerUserStateAnswer_;
        protected abstract event Action<ServerJSON> OnClientReceivedServerConfigAnswer_;


        public static void EmitToServerCTSPacket(CTSPacket packet)
        {
            if (Instance) Instance.EmitToServerCTSPacket_(packet);
        }

        public static void EnterWorld(Cid WorldCid)
        {
            EmitToServerCTSPacket(new CTSPWorldChangeAnnouncement()
            {
                WorldRootCid = WorldCid,
            });
        }

        public static event Action<UserID, ServerUserState> OnClientReceivedServerUserStateAnswer
        {
            add => Instance.OnClientReceivedServerUserStateAnswer_ += value;
            remove { if (Instance != null) Instance.OnClientReceivedServerUserStateAnswer_ -= value; }
        }

        public static event Action<ServerJSON> OnClientReceivedServerConfigAnswer
        {
            add => Instance.OnClientReceivedServerConfigAnswer_ += value;
            remove {  if (Instance != null) Instance.OnClientReceivedServerConfigAnswer_ -= value; }
        }

        // Schedule the shutdown on the first suitable moment
        public static void Quit()
        {
            G.ToQuit = true;
            if (Instance) Instance.enabled = true;
        }
    }
}