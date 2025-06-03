/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Avatar;
using Arteranos.Common;
using Arteranos.Core;
using Ipfs;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace Arteranos.Services
{
    public interface INetworkStatus : IMonoBehaviour
    {
        bool IsClientConnected { get; }
        bool IsClientConnecting { get; }
        Action<bool, string> OnClientConnectionResponse { get; set; }
        bool OpenPorts { get; set; }
        MultiHash RemotePeerId { get; set; }
        AsyncLazy<List<IPAddress>> IPAddresses { get; }

        event Action<ConnectivityLevel, OnlineLevel> OnNetworkStatusChanged;

        ConnectivityLevel GetConnectivityLevel();
        OnlineLevel GetOnlineLevel();
        IEnumerable<IAvatarBrain> GetOnlineUsers();
        IAvatarBrain GetOnlineUser(uint netId);
        IAvatarBrain GetOnlineUser(UserID userID);
        void StartClient(Uri connectionUri);
        Task StartHost(bool resetConnection = false);
        Task StartServer();
        Task StopHost(bool loadOfflineScene);
    }
}