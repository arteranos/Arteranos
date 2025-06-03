/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Common;
using ProtoBuf;
using System.IO;

namespace Arteranos.Core
{
    [ProtoContract]
    public struct KickPacket
    {
        [ProtoMember(1)]
        public UserID UserID;

        [ProtoMember(2)]
        public ServerUserState State;

        public readonly byte[] Serialize()
        {
            using MemoryStream ms = new();
            Serializer.Serialize(ms, this);
            ms.Position = 0;
            return ms.ToArray();
        }

        public static KickPacket Deserialize(byte[] bytes)
        {
            using MemoryStream ms = new(bytes);
            return Serializer.Deserialize<KickPacket>(ms);
        }

    }
}
