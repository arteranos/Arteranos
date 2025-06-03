/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.IO;
using Arteranos.Common;
using ProtoBuf;

namespace Arteranos.Core
{
    [ProtoContract]
    public struct ServerUserState
    {
        [ProtoMember(1)]
        public UserID userID;

        [ProtoMember(2)]
        public ulong userState;

        [ProtoMember(3)]
        public string address;

        [ProtoMember(4)]
        public string deviceUID;

        [ProtoMember(5)]
        public string remarks;

        public readonly byte[] Serialize()
        {
            using MemoryStream ms = new();
            Serializer.Serialize(ms, this);
            ms.Position = 0;
            return ms.ToArray();
        }

        public static ServerUserState Deserialize(byte[] bytes)
        {
            using MemoryStream ms = new(bytes);
            return Serializer.Deserialize<ServerUserState>(ms);
        }
    }
}
