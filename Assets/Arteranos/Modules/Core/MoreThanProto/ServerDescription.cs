/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Arteranos.Common.Cryptography;
using Ipfs.Cryptography.Proto;
using ProtoBuf;

namespace Arteranos.Core
{
    public partial class ServerDescription : FlatFileDB<ServerDescription>
    {
        public ServerDescription() 
        {
            _KnownPeersRoot = $"{ConfigUtils.persistentDataPath}/KnownPeers";
            _GetFileName = id => $"{ConfigUtils.persistentDataPath}/KnownPeers/{Utils.GetURLHash(id)}.description";
            _SearchPattern = "*.description";
            _Deserialize = Deserialize;
            _Serialize = Serialize;
        }

        public bool DBUpdate() 
            => _DBUpdate(PeerID, old => old.LastModified <= LastModified);

        public static ServerDescription DBLookup(string id) 
            => new ServerDescription()._DBLookup(id);

        public static void DBDelete(string id) 
            => new ServerDescription()._DBDelete(id);

        public static IEnumerable<ServerDescription> DBList()
        {
            // Filter out outdated entries, too.
            List<string> toDelete = new();
            foreach (ServerDescription entry in new ServerDescription()._DBList())
            {
                if (entry) yield return entry;
                else
                {
                    UnityEngine.Debug.Log($"Discarding outdated peer {entry.PeerID}");
                    toDelete.Add(entry.PeerID);
                }
            }

            // And, really delete obsolete entries.
            ServerDescription _DB = new();
            foreach(string id in toDelete)
                _DB._DBDelete(id);
        }

        public void Serialize(SignKey serverPrivateKey, Stream stream)
        {
            // Sign the structure with the empty signature field
            using (MemoryStream ms = new())
            {
                signature = null;
                LastUsedIPAddress = null;

                Serializer.Serialize(ms, this);
                ms.Position = 0;
                serverPrivateKey.Sign(ms.ToArray(), out signature);
            }

            Serializer.Serialize(stream, this);
            stream.Flush();
        }

        public static ServerDescription Deserialize(PublicKey serverPublicKey, Stream stream)
        {
            ServerDescription d = Serializer.Deserialize<ServerDescription>(stream);
            if(serverPublicKey != null)
            {
                byte[] signature = d.signature;
                string tmp = d.LastUsedIPAddress;

                using MemoryStream ms = new();

                d.LastUsedIPAddress = null;
                d.signature = null;

                Serializer.Serialize(ms, d);
                ms.Position = 0;
                serverPublicKey.Verify(ms.ToArray(), signature);

                // Restore the signature and IP address,
                // to re-serialize without PeerID's private key.
                d.signature = signature;
                d.LastUsedIPAddress = tmp;
            }

            return d;
        }

        public static implicit operator bool(ServerDescription sd)
        {
            if (sd == null) return false;

            // Seen longer than 30 days ago
            if (sd.LastSeen < DateTime.UtcNow - TimeSpan.FromDays(30)) return false;

            // Outdated version
            Version serverVersion = Core.Version.Parse(sd.Version);
            if (serverVersion < Core.Version.MinVersion) return false;

            // Everything is OK.
            return true;
        }

    }
}