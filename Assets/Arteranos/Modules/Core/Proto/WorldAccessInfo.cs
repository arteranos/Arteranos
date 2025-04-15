/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using ProtoBuf;
using System.Collections.Generic;
using System.IO;

namespace Arteranos.Core
{
    // Access levels imply capabilities of lower levels
    public enum WorldAccessInfoLevel
    {
        Nothing = 0,    // Nothing at all, server would even reject latecomers
        View,           // User can visit on an already loaded world
        Pin,            // User can favourite (=pin) would, and load it on his own
        Edit,           // User can edit the world's contents
        Admin           // All of the above, and can modify the access list
    }

    // Ref. #89 - World access control
    [ProtoContract]
    public partial class WorldAccessInfo
    {
        // Set on changing world on server
        // Cleared on propagating from server to visitors
        // NOT INCLUDED in signature, because it's only between the uploader and the server
        [ProtoMember(1)]
        public string Password; // Sent from author to the server, server asks the visitors
        
        [ProtoMember(2)]
        public HashSet<UserID> BannedUsers = new(); // Self explanatory. Overrides everthing.

        [ProtoMember(3)]
        public WorldAccessInfoLevel DefaultLevel; // Default access level for users not on the list

        [ProtoMember(6)]
        public Dictionary<UserID, WorldAccessInfoLevel> UserALs = new(); // Access levels for individual users

        [ProtoMember(10)]
        public UserID AccessAuthor;     // Creator of this data, the world creator or the delegates

        [ProtoMember(11)]
        public byte[] Signature;        // Against AccessAuthor's signing key

        [ProtoMember(12)]
        public UserID WorldAuthor;      // The world creator.

        public static WorldAccessInfo Create(UserID author)
        {
            return new WorldAccessInfo()
            {
                Password = null,
                DefaultLevel = WorldAccessInfoLevel.Pin,
                AccessAuthor = author,
                WorldAuthor = author
            };
        }

        // Sure, everyone may edit this structure, but we can be sure _who_ done it
        public void Serialize(Stream stream)
        {
            // At least to have some dumbf**k shield...
            if (!CanAdmin(G.Client.MeUserID))
                throw new InvalidDataException("ACL access denied");

            string tmpPassword = Password;
            AccessAuthor = G.Client.MeUserID;
            Signature = null;
            Password = null;

            using (MemoryStream ms = new())
            {
                Serializer.Serialize(ms, this);
                ms.Position = 0;
                Client.Sign(ms.ToArray(), out Signature);
            }

            Password = tmpPassword;

            Serializer.Serialize(stream, this);
            stream.Flush();
        }

        public static WorldAccessInfo Deserialize(byte[] data)
        {
            WorldAccessInfo wai = Serializer.Deserialize<WorldAccessInfo>(new MemoryStream(data));

            using (MemoryStream ms = new()) 
            {
                // Password and signature itself is NOT covered by the signature.
                byte[] signature = wai.Signature;
                string tmpPassword = wai.Password;
                wai.Signature = null;
                wai.Password = null;

                Serializer.Serialize(ms, wai);
                ms.Position = 0;

                // Throw if invalid signature
                wai.AccessAuthor.SignPublicKey.Verify(ms.ToArray(), signature);

                wai.Signature = signature;
                wai.Password = tmpPassword;
            }

            return wai;
        }
    }
}