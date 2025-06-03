/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using UnityEngine;

using Mono.Nat;
using System;
using System.Threading.Tasks;
using Arteranos.Core;
using Mirror;

using System.Collections.Generic;
using Arteranos.Avatar;
using System.Linq;
using Ipfs;
using System.Net;
using System.Net.NetworkInformation;
using Arteranos.Core.Managed;
using System.Runtime.InteropServices;
using Arteranos.Common;

namespace Arteranos.Services
{
    public partial class NetworkStatus : MonoBehaviour, INetworkStatus
    {
        public AsyncLazy<List<IPAddress>> IPAddresses => m_IPAddresses;

        private NATDevice device = null;
        public MultiHash RemotePeerId { get; set; } = null;

        public event Action<ConnectivityLevel, OnlineLevel> OnNetworkStatusChanged;
        public Action<bool, string> OnClientConnectionResponse { get => m_OnClientConnectionResponse; set => m_OnClientConnectionResponse = value; }

        public bool OpenPorts
        {
            get => m_OpenPorts;
            set
            {
                bool oldvalue = m_OpenPorts;
                bool newvalue = value && (G.Server?.UseUPnP ?? false);
                if(oldvalue != newvalue)
                {
                    m_OpenPorts = newvalue;
                    if(m_OpenPorts) OpenPortsAsync();
                    else ClosePortsAsync();
                }
            }
        }

        private Action<bool, string> m_OnClientConnectionResponse = null;

        public bool ServerPortPublic = false;

        private ConnectivityLevel CurrentConnectivityLevel = ConnectivityLevel.Unconnected;
        private OnlineLevel CurrentOnlineLevel = OnlineLevel.Offline;
        private bool m_OpenPorts = false;

        private NetworkManager manager = null;
        private Transport transport = null;
        public void Awake()
        {
            manager = FindObjectOfType<NetworkManager>(true);
            transport = FindObjectOfType<Transport>(true);

            G.NetworkStatus = this;
        }

        // -------------------------------------------------------------------
        #region Running
        public ConnectivityLevel GetConnectivityLevel()
        {
            if(Application.internetReachability == NetworkReachability.NotReachable)
                return ConnectivityLevel.Unconnected;

            return (ServerPortPublic)
                ? ConnectivityLevel.Unrestricted
                : ConnectivityLevel.Restricted;
        }

        public OnlineLevel GetOnlineLevel()
        {
            bool clientConnected = NetworkClient.active && NetworkClient.isConnected;

            if (!clientConnected && !NetworkServer.active)
                return OnlineLevel.Offline;

            if(clientConnected && NetworkServer.active)
                return OnlineLevel.Host;

            return clientConnected
                ? OnlineLevel.Client
                : OnlineLevel.Server;
        }

        public bool IsClientConnecting => NetworkClient.isConnecting;

        public bool IsClientConnected => NetworkClient.isConnected;

        public void OnEnable()
        {
            Debug.Log("Setting up NAT gateway configuration");

            IEnumerator RefreshDiscovery()
            {
                RefreshIPAddresses();

                yield return IPAddresses.WaitFor();

                while(true)
                {
                    // No sense for router and IP detection if the computer's network cable is unplugged
                    // and in its airplane mode.
                    if (GetConnectivityLevel() == ConnectivityLevel.Unconnected)
                    {
                        yield return new WaitForSeconds(10);
                    }
                    else
                    {
                        // Needs to be refreshed anytime, because the router invalidates port forwarding
                        // if the connected device falls idle, or catatonic.
                        NatUtility.StartDiscovery();

                        // Wait for refresh...
                        yield return new WaitForSeconds(500);
                    }


                    NatUtility.StopDiscovery();
                }
            }

            NatUtility.DeviceFound += DeviceFound;

            StartCoroutine(RefreshDiscovery());
        }

        public void OnDisable()
        {
            Debug.Log("Shutting down NAT gateway configuration");

            NatUtility.DeviceFound -= DeviceFound;

            StopAllCoroutines();

            device?.Dispose();
            device = null;

            NatUtility.StopDiscovery();
        }

        public void Update()
        {
            ConnectivityLevel c1 = GetConnectivityLevel();
            OnlineLevel c2 = GetOnlineLevel();

            if(CurrentConnectivityLevel != c1 || CurrentOnlineLevel != c2)
                OnNetworkStatusChanged?.Invoke(c1, c2);

            if(CurrentOnlineLevel == OnlineLevel.Client && c2 == OnlineLevel.Offline)
                OnRemoteDisconnected();

            CurrentConnectivityLevel = c1;
            CurrentOnlineLevel = c2;
        }

        #endregion
        // -------------------------------------------------------------------
        #region Connectivity and UPnP

        private void DeviceFound(object sender, DeviceEventArgs e)
        {
            // For some reason, my Fritz!Box reponds twice, for two WAN ports,
            // all with the same external IP address?
            if(device != null) return;

            device = new(e.Device);

            // ExternalAddress_ = await device.GetExternalIPAsync();

            Debug.Log($"Device found : {device.NatProtocol}");
            Debug.Log($"  Type       : {device.GetType().Name}");
        }

        private async Task<bool> OpenPortAsync(int port)
        {
            // UDP, and internal and external ports as the same.
            Mapping mapping = new(Protocol.Udp, port, port);

            try
            {
                await device.CreatePortMapAsync(mapping);
                return true;
            }
            catch(Exception ex)
            {
                Debug.Log($"Failed to create a port mapping for {port}: {ex.Message}");
                return false;
            }
        }

        private async void ClosePortAsync(int port)
        {
            // UDP, and internal and external ports as the same.
            Mapping mapping = new(Protocol.Udp, port, port);

            try
            {
                await device.DeletePortMapAsync(mapping);
            }
            catch
            {
                Debug.Log($"Failed to delete a port mapping for {port}... but that's okay.");
            }
        }

        public async void OpenPortsAsync()
        {
            // No point to open the ports if we're not supposed to.
            if(!OpenPorts) return;

            Debug.Log("Opening ports in the router");

            Server ss = G.Server;

            ServerPortPublic = await OpenPortAsync(ss.ServerPort);
        }

        public void ClosePortsAsync()
        {
            Debug.Log("Closing ports in the router.");

            Server ss = G.Server;

            if(ServerPortPublic)
                ClosePortAsync(ss.ServerPort);

            ServerPortPublic = false;
        }

        #endregion
        // -------------------------------------------------------------------
        #region Port allocation

        private static readonly HashSet<int> bannedPorts = new()
        {
            5001, // Default API
            4001, // Default IPFS
            8080, // Default Web Gateway 
        };

        public static int GetAvailablePort(int startPort, int endPort = 49152, HashSet<int> excluded = null, bool random = true)
        {
            static void AddCollection<T>(HashSet<T> set, IEnumerable<T> collection)
            {
                foreach(T t in collection) set.Add(t);
            }

            var portArray = new HashSet<int>();

            var properties = IPGlobalProperties.GetIPGlobalProperties();

            // Ignore preallocated ports
            AddCollection(portArray, excluded ?? bannedPorts);

            // Ignore active connections
            var connections = properties.GetActiveTcpConnections();
            AddCollection(portArray, from n in connections select n.LocalEndPoint.Port);

            // Ignore active tcp listners
            var endPoints = properties.GetActiveTcpListeners();
            AddCollection(portArray, from n in endPoints select n.Port);

            // Ignore active UDP listeners
            endPoints = properties.GetActiveUdpListeners();
            AddCollection(portArray, from n in endPoints select n.Port);

            if (!random) // Sequential
            {
                for (int i = startPort; i < endPort; i++)
                    if (!portArray.Contains(i))
                        return i;
                return 0;
            }
            else // Just pick one
            {
                if (portArray.Count > endPort - startPort - 20) return 0;

                System.Random rnd = new();
                while(true)
                {
                    int i = rnd.Next(startPort, endPort);
                    if (!portArray.Contains(i)) return i;
                }
            }

            // NOTREACHED
        }
        #endregion
        // -------------------------------------------------------------------
        #region IP Address determination

        private AsyncLazy<List<IPAddress>> m_IPAddresses = null;

        private void RefreshIPAddresses()
        {
            m_IPAddresses = new(async () => await GatherIPAddresses());
        }

        private async Task<List<IPAddress>> GatherIPAddresses()
        {
            List<IPAddress> ips = new();

            IPAddress externalIPAddress = null;
            for(int i = 0; i < 10; i++)
            {
                externalIPAddress = await GetExternalIPAdress();
                if (externalIPAddress != null) break;
            }

            if(externalIPAddress != null) ips.Add(externalIPAddress);

            ips.AddRange(GetLocalIPAddresses());

            foreach (IPAddress ip in ips)
                Debug.Log($" Found IP address: {ip}");

            return ips;
        }

        private static List<IPAddress> GetLocalIPAddresses()
        {
            List<IPAddress> addr = new();

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                UnicastIPAddressInformationCollection uniCast = adapterProperties.UnicastAddresses;
                if (uniCast.Count > 0)
                    foreach (UnicastIPAddressInformation uni in uniCast)
                    {
                        if (uni.Address.IsIPv6LinkLocal) continue;

                        // Really, now?! Seen NotImplementedException on Linux! >:(
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            if (uni.PrefixOrigin == PrefixOrigin.WellKnown) continue;

                        addr.Add(uni.Address);
                    }
            }

            return addr;
        }

        public static async Task<IPAddress> GetExternalIPAdress()
        {
            try
            {
                string ipstr = await G.IPFSService.GetConfigItem<string>("X-Arteranos.ExternalIPv4Address");
                return IPAddress.Parse(ipstr);
            }
            catch (Exception)
            {
                return null;
            }
        }

#endregion
        // -------------------------------------------------------------------
        #region Connections

        private bool transitionDisconnect = false;

        public void StartClient(Uri connectionUri)
        {

            Debug.Log($"Attempting to connect to {connectionUri}...");

            manager.StartClient(connectionUri);
        }

        public async Task StopHost(bool loadOfflineScene)
        {
            manager.StopHost();
            OpenPorts = false;

            RemotePeerId = null;

            if (loadOfflineScene)
                Core.TaskScheduler.ScheduleCoroutine(() => TransitionProgress.TransitionTo(null));

            // And, wait for the network to really be shut down.
            while (manager.isNetworkActive) await Task.Delay(8);
        }

        public async Task StartHost(bool resetConnection = false)
        {
            if (resetConnection)
                await SmoothServerTransition();

            OpenPorts = true;
            G.ConnectionManager.ExpectConnectionResponse();

            SetPort();
            manager.StartHost();

            // And, wait for the network to really be started up.
            while (!manager.isNetworkActive) await Task.Delay(8);
        }

        private void SetPort()
        {
            // Set the port number on supported transports, as long as they'd
            // require it.
            int serverPort = G.Server.ServerPort;
            if (serverPort <= 0
                || serverPort >= 49152
                || GetAvailablePort(serverPort, serverPort + 1, null, false) == 0)
            {
                Debug.LogWarning("Preferred port unavailable");
                serverPort = GetAvailablePort(8000, 49152);
                Debug.LogWarning($"Port temporarily changed to {serverPort}");
                G.Server.ServerPort = serverPort;
            }

            if (transport is TelepathyTransport tt)
                tt.port = (ushort)serverPort;
            else if (transport is LiteNetLibTransport lnlt)
                lnlt.port = (ushort)serverPort;
        }

        public async Task StartServer()
        {
            await SmoothServerTransition();

            OpenPorts = true;

            SetPort();
            manager.StartServer();

            // And, wait for the network to really be started up.
            while (!manager.isNetworkActive) await Task.Delay(8);
        }

        private async Task SmoothServerTransition()
        {
            if (manager.isNetworkActive)
            {
                transitionDisconnect = true;
                await StopHost(false);
            }
        }

        private void OnRemoteDisconnected()
        {
            // Client to offline. It it's planned to switch servers,
            // skip the offline scene.
            if(!transitionDisconnect) _ = StopHost(true);
            transitionDisconnect = false;
        }
        #endregion
        // -------------------------------------------------------------------
        #region User Data queries
        public IAvatarBrain GetOnlineUser(UserID userID)
        {
            IEnumerable<IAvatarBrain> q =
                from entry in GameObject.FindGameObjectsWithTag("Player")
                where entry.GetComponent<IAvatarBrain>().UserID == userID
                select entry.GetComponent<IAvatarBrain>();

            return q.Count() > 0 ? q.First() : null;
        }

        public IAvatarBrain GetOnlineUser(uint netId)
        {
            IEnumerable<IAvatarBrain> q =
                from entry in GameObject.FindGameObjectsWithTag("Player")
                where entry.GetComponent<IAvatarBrain>().NetID == netId
                select entry.GetComponent<IAvatarBrain>();

            return q.Count() > 0 ? q.First() : null;
        }

        public IEnumerable<IAvatarBrain> GetOnlineUsers()
        {
            return from entry in GameObject.FindGameObjectsWithTag("Player")
                   select entry.GetComponent<IAvatarBrain>();

        }

        #endregion
        // -------------------------------------------------------------------
    }

    // Extension of INatDevice to treat port mappings as a managed resource
    internal class NATDevice : ManagedHandle<INatDevice>, INatDevice
    {
        public NATDevice(INatDevice newDevice) : base(
            () => newDevice,
            r => { }
            )
        { }

        private readonly List<Mapping> mappings = new();
        public IPEndPoint DeviceEndpoint => resource.DeviceEndpoint;
        public DateTime LastSeen => resource.LastSeen;
        public NatProtocol NatProtocol => resource.NatProtocol;
        public async Task<Mapping> CreatePortMapAsync(Mapping mapping)
        {
            Mapping map = await resource.CreatePortMapAsync(mapping);
            mappings.Add(map);
            return map;
        }

        public async Task<Mapping> DeletePortMapAsync(Mapping mapping)
        {
            Mapping map = await resource.DeletePortMapAsync(mapping);
            for (int i = mappings.Count - 1; i >= 0; i--)
                if (mappings[i].Equals(map)) mappings.RemoveAt(i);

            return map;
        }

        public Task<Mapping[]> GetAllMappingsAsync() => resource.GetAllMappingsAsync();
        public Task<IPAddress> GetExternalIPAsync() => resource.GetExternalIPAsync();
        public Task<Mapping> GetSpecificMappingAsync(Protocol protocol, int publicPort) => resource.GetSpecificMappingAsync(protocol, publicPort);

        private static void DeleteAllPortMappings(INatDevice device, List<Mapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                try
                {
                    Debug.Log($"Disposing {mapping.PrivatePort}/{mapping.Protocol} --> {mapping.PublicPort}");
                    device.DeletePortMap(mapping);
                }
                catch { }
            }
        }

        public void DeleteAllPortMappings() => DeleteAllPortMappings(resource, mappings);
        // -------------------------------------------------------------------
        protected override void DisposeOuter()
        {
            DeleteAllPortMappings();

            base.DisposeOuter();
        }
    }
}
