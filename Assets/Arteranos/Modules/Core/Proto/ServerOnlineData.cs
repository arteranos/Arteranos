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
using Arteranos.Services;
using ProtoBuf;

namespace Arteranos.Core
{
    [ProtoContract]
    public partial class ServerOnlineData : PeerMessage
    {
        [Obsolete("See #208")]
        [ProtoMember(1)]
        public string CurrentWorldCid;

        [Obsolete("See #208")]
        [ProtoMember(2)]
        public string CurrentWorldName;

        [ProtoMember(3)]
        public string ServerDescriptionCid; // Just in case if we don't have the SD at all.

        [ProtoMember(4)]
        public List<Fingerprint> UserFingerprints = new();

        [ProtoMember(5)]
        public OnlineLevel OnlineLevel;

        //[ProtoMember(6)]
        //public string WorldCid;

        [ProtoMember(7)]
        public List<string> IPAddresses = new();

        [ProtoMember(8)]
        public DateTime Timestamp; // Sender's idea of time. And to see that the packets are different ones, not just dupes.

        [ProtoMember(9)]
        public bool Firewalled;

        [ProtoMember(10)]
        public PublicWorldData PublicWorldData;

        public override void Serialize(Stream stream)
            => Serializer.Serialize(stream, this);
    }
}