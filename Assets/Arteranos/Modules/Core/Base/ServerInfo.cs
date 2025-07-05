/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Ipfs;
using Arteranos.Common.Cryptography;
using System.Net;
using Arteranos.Common;

namespace Arteranos.Core
{
    public class ServerInfo
    {
        private ServerOnlineData OnlineData = null;
        private ServerDescription DescriptionStruct = null;

        public MultiHash PeerID { get; private set; } = null;

        public string CurrentWorldName => OnlineData?.PublicWorldData.Name;
        public PublicWorldData? PublicWorldData => OnlineData?.PublicWorldData;

        private ServerInfo()
        {

        }

        public ServerInfo(MultiHash PeerID)
        {
            this.PeerID = PeerID;

            OnlineData = ServerOnlineData.DBLookup(PeerID.ToString());
            DescriptionStruct = ServerDescription.DBLookup(PeerID.ToString());
        }

        public void Delete()
        {
            ServerOnlineData.DBDelete(PeerID.ToString());
            ServerDescription.DBDelete(PeerID.ToString());
        }
        public static IEnumerable<ServerInfo> Dump()
        {
            foreach(ServerDescription sd in ServerDescription.DBList())
            {
                yield return new ServerInfo()
                {
                    OnlineData = ServerOnlineData.DBLookup(sd.PeerID),
                    DescriptionStruct = sd,
                    PeerID = sd.PeerID,
                };
            }
        }

        public bool IsValid => DescriptionStruct;
        public bool SeenOnline => OnlineData != null;
        public bool IsOnline => OnlineData != null && OnlineData.LastOnline > (DateTime.UtcNow - TimeSpan.FromMinutes(5)) && OnlineData.OnlineLevel != Services.OnlineLevel.Offline;
        public bool IsFirewalled => OnlineData?.Firewalled ?? true;
        public string Name => DescriptionStruct?.Name;
        public string Description => DescriptionStruct?.Description ?? string.Empty;
        public string PrivacyTOSNotice => DescriptionStruct?.PrivacyTOSNotice;
        public Cid ServerIcon => DescriptionStruct?.ServerIcon;
        public string[] AdminNames => DescriptionStruct?.AdminNames ?? new string[0];
        public IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                return OnlineData?.IPAddresses == null 
                    ? null 
                    : from entry in OnlineData.IPAddresses select IPAddress.Parse(entry);
            }
        }

        public int ServerPort => DescriptionStruct?.ServerPort ?? 0;
        public string LastUsedIPAddress => DescriptionStruct?.LastUsedIPAddress;
        public void UpdateLastUsedIPAddress(IPAddress addr)
        {
            DescriptionStruct.LastUsedIPAddress = addr.ToString();
            DescriptionStruct._DBInsert(DescriptionStruct.PeerID);
        }

        public string SPKDBKey => DescriptionStruct.PeerID;
        public PermissionsJSON Permissions => DescriptionStruct?.Permissions ?? new(true);
        public DateTime LastUpdated => DescriptionStruct?.LastModified ?? DateTime.MinValue;
        public DateTime LastOnline
        {
            get
            {
                DateTime online = OnlineData?.LastOnline ?? DateTime.MinValue;
                DateTime data = DescriptionStruct?.LastSeen ?? DateTime.MinValue;

                return online > data ? online : data;
            }
        }

        public int UserCount => OnlineData?.UserFingerprints?.Count ?? 0;
        public IEnumerable<UserID> Friends => G.Community.FindFriends(PeerID);
        public int FriendCount => Friends.Count();

        public int MatchScore
        {
            get
            {
                (int ms, int _) = Permissions.MatchRatio(G.UserContPerms);
                return ms + FriendCount * 3;
            }
        }

        public byte[] PrivacyTOSNoticeHash
        {
            get
            {
                if (PrivacyTOSNotice == null) return null;

                return Hashes.SHA256(PrivacyTOSNotice);
            }
        }

        public bool UsesCustomTOS
        {
            get 
            {
                if (PrivacyTOSNotice == null) return false; 
                return !PrivacyTOSNoticeHash.SequenceEqual(Hashes.SHA256(SettingsManager.DefaultTOStext));
            }
        }
    }
}
