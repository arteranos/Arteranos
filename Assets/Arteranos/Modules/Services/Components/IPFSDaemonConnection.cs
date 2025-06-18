/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Common.Cryptography;
using Ipfs;
using Ipfs.Cryptography.Proto;
using Ipfs.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Arteranos.Common;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Arteranos.Services
{
    public static class IPFSDaemonConnection
    {
        // API ports to avoid
        public static readonly HashSet<int> bannedAPIPorts = new() 
        { 
            5001, // Default API
            4001, // Default IPFS
            8080, // Default Web Gateway 
        };

        public enum Status
        {
            OK = 0,
            NoDaemonExecutable,
            NoRepository,
            CommandFailed,
            DeadDaemon,
            PortSquatter
        }

        public static IpfsClientEx Ipfs { get; private set; } = null;
        public static Peer Self { get; private set; } = null;
        public static SignKey ServerKeyPair { get; private set; } = null;
        public static string RepoDir { get; private set; } = null;

        private static bool? _RepoExists = null;
        private static int? APIPort = null;

        public static Status CheckRepository()
        {
            if (_RepoExists ?? false) return Status.OK;

            Status res = Status.CommandFailed;

            RepoDir = $"{ConfigUtils.persistentDataPath}/.ipfs";

            try
            {
                _ = IpfsClientEx.ReadDaemonPrivateKey(RepoDir);

                _RepoExists = true;
                res = Status.OK;
            }
            catch
            {
                _RepoExists = false;
                res = Status.NoRepository;
            }

            Debug.Log($"Checking IPFS repository ({RepoDir}) ... {res}");
            return res;
        }

        public static Status StopDaemon()
        {
            Task.Run(async () =>
            {
                await Ipfs.ShutdownAsync();
            });
            return Status.OK;
        }

        public static int GetAPIPort()
        {
            if (APIPort != null) return APIPort.Value;

            if (CheckRepository() != Status.OK) return -1;
            int port = -1;

            try
            {
                MultiAddress apiAddr = IpfsClientEx.ReadDaemonAPIAddress(RepoDir);
                foreach (NetworkProtocol protocol in apiAddr.Protocols)
                    if (protocol.Code == 6)
                    {
                        port = int.Parse(protocol.Value);
                        break;
                    }
            }
            catch { }

            APIPort = port;
            Debug.Log($"Repository says API address to be: {APIPort}");
            return port;
        }

        public static async Task<Status> CheckAPIConnection(int attempts)
        {
            int port = GetAPIPort();

            if (port < 0) return Status.NoRepository;

            string host = $"http://localhost:{port}";

            Debug.Log($"Trying to connect to {host}...");

            IpfsClientEx ipfs = new(host);

            for(int i = 0;  i < attempts; i++)
            {
                try
                {
                    _ = await ipfs.Config.GetAsync();
                    break;
                }
                catch // (Exception es) 
                {
                    // Debug.LogException(es);
                }

                await Task.Delay(1000);
            }

            try
            {
                PrivateKey pk = IpfsClientEx.ReadDaemonPrivateKey(RepoDir);

                await ipfs.VerifyDaemonAsync(pk);

                ServerKeyPair = SignKey.ImportPrivateKey(pk);
                Ipfs = ipfs;

                Self = await ipfs.IdAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                Debug.Log("Node's daemon is unresponsive, or don't match with this repository.");
                return Status.PortSquatter;
            }

            Debug.Log("Daemon connect OK");
            return Status.OK;
        }
    }
}