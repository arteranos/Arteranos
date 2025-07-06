/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Arteranos.Common;
using ProtoBuf;

namespace Arteranos.Core
{
    [ProtoContract]
    public partial class ServerDescription : IEquatable<ServerDescription>
    {
        [ProtoMember(1)]
        public string Name;

        [ProtoMember(2)]
        public int ServerPort;

        //[ProtoMember(3)]
        //public int MetadataPort;

        [ProtoMember(4)]
        public string Description;

        //[ProtoMember(5)]
        //public byte[] Icon;

        [ProtoMember(7)]
        public string Version;

        [ProtoMember(8)]
        public string MinVersion;

        [ProtoMember(9)]
        public PermissionsJSON Permissions;

        [ProtoMember(10)]
        public string PrivacyTOSNotice;

        [ProtoMember(12)]
        public string PeerID;

        [ProtoMember(13)]
        public DateTime LastModified;

        [ProtoMember(14)]
        public byte[] signature;

        //[ProtoMember(15)]
        //public string ServerDescriptionCid; // Only matches itself if it's null!

        [ProtoMember(16)]
        public string ServerIcon; // string, because CIDs are not proto-serializable

        [ProtoMember(17)]
        public DateTime LastSeen; // Local only: Time last seen online

        [ProtoMember(18)]
        public string LastUsedIPAddress; // Client only: IP address to connect to this server
        
        [ProtoMember(19)]
        public UserID[] AdminNames;

        public void Serialize(Stream stream)
            => Serializer.Serialize(stream, this);

        public static ServerDescription Deserialize(Stream stream)
            => Serializer.Deserialize<ServerDescription>(stream);

        public override bool Equals(object obj)
        {
            return Equals(obj as ServerDescription);
        }

        public bool Equals(ServerDescription other)
        {
            return other is not null &&
                   Name == other.Name &&
                   ServerPort == other.ServerPort &&
                   Description == other.Description &&
                   ServerIcon == other.ServerIcon &&
                   Version == other.Version &&
                   MinVersion == other.MinVersion &&
                   EqualityComparer<PermissionsJSON>.Default.Equals(Permissions, other.Permissions) &&
                   PrivacyTOSNotice == other.PrivacyTOSNotice &&
                   PeerID == other.PeerID &&
                   LastModified == other.LastModified &&
                   true; //AdminNames.SequenceEqual(other.AdminNames);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Name);
            hash.Add(ServerPort);
            hash.Add(Description);
            hash.Add(ServerIcon);
            hash.Add(Version);
            hash.Add(MinVersion);
            hash.Add(Permissions);
            hash.Add(PrivacyTOSNotice);
            // hash.Add(AdminNames);
            hash.Add(PeerID);
            hash.Add(LastModified);
            return hash.ToHashCode();
        }

        public static bool operator ==(ServerDescription left, ServerDescription right)
        {
            return EqualityComparer<ServerDescription>.Default.Equals(left, right);
        }

        public static bool operator !=(ServerDescription left, ServerDescription right)
        {
            return !(left == right);
        }
    }

    [ProtoContract]
    public partial class ServerDescriptionLink : PeerMessage
    {
        [ProtoMember(1)]
        public string ServerDescriptionCid;
    }
}