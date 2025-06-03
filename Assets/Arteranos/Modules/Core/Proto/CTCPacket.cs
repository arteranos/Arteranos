/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.IO;
using Arteranos.Common;
using Arteranos.Common.Cryptography;
using Mirror;
using ProtoBuf;

namespace Arteranos.Core
{
    public struct CTCPacketEnvelope : NetworkMessage
    {
        public UserID receiver;
        public CMSPacket CTCPayload; // Encrypted for the receiver
    }

    [ProtoContract]
    [ProtoInclude(65537, typeof(CTCPUserState))]
    [ProtoInclude(65538, typeof(CTCPTextMessage))]
    public class CTCPacket
    {
        [ProtoMember(1)]
        public UserID sender;

        public virtual byte[] Serialize()
        {
            using MemoryStream ms = new();
            Serializer.Serialize(ms, this);
            return ms.ToArray();
        }

        public static CTCPacket Deserialize(byte[] data) 
            => Serializer.Deserialize<CTCPacket>(new MemoryStream(data));
    }

    [ProtoContract]
    public class CTCPUserState : CTCPacket
    {
        [ProtoMember(1)]
        public ulong state;
    }

    [ProtoContract]
    public class CTCPTextMessage : CTCPacket
    {
        [ProtoMember(1)]
        public string text;
    }
}