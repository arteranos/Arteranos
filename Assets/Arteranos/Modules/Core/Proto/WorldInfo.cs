/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using ProtoBuf;
using System;

namespace Arteranos.Core
{
    [ProtoContract]
    public class WorldInfo
    {
        // [ProtoMember(1)] - Unnecessary, Cid is available locally
        public string WorldCid;

        [ProtoMember(2)]
        public string WorldName;

        [ProtoMember(3)]
        public string WorldDescription;

        //[ProtoMember(4)]
        //public string AuthorNickname;

        //[ProtoMember(5)]
        //public byte[] AuthorPublicKey;

        [ProtoMember(6)]
        public ServerPermissions ContentRating;

        //[ProtoMember(7)]
        //public byte[] Signature;

        //[ProtoMember(8)]
        //[Obsolete("Retired, separate file")]
        //public byte[] ScreenshotPNG;

        [ProtoMember(9)]
        public DateTime Created;

        [ProtoMember(10)]
        public UserID Author;

        [ProtoMember(11)]
        public WorldAccessInfo AccessInfo;
    }
}
