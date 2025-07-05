/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections.Generic;
using System.IO;
using Arteranos.Common;
using Arteranos.Common.Cryptography;
using Arteranos.Core.Managed;
using ProtoBuf;
using System.Linq;
using Ipfs;

namespace Arteranos.Core
{
    [ProtoContract]
    public struct PublicWorldData
    {
        [ProtoMember(1)]
        public string Name;

        [ProtoMember(2)]
        public UserID Creator;

        [ProtoMember(3)]
        public string ScreenshotCid;

        [ProtoMember(4)]
        public Fingerprint WorldFP;

        [ProtoMember(5)]
        public PermissionsJSON Permissions;

        [ProtoMember(6)]
        public WorldAccessInfoLevel DefaultAccess;

        [ProtoMember(7)]
        public List<Fingerprint> DeniedUsers;

        [ProtoMember(8)]
        public List<Fingerprint> VisitorUsers;

        [ProtoMember(9)]
        public List<Fingerprint> PinningUsers;

        [ProtoMember(10)]
        public string Description;

        private WorldAccessInfoLevel GetACL(UserID user)
        {
            Fingerprint fp = new Fingerprint(user);
            if (PinningUsers?.Contains(fp) ?? false) return WorldAccessInfoLevel.Pin;

            if (VisitorUsers?.Contains(fp) ?? false) return WorldAccessInfoLevel.View;

            if (DeniedUsers?.Contains(fp) ?? false) return WorldAccessInfoLevel.Nothing;

            return DefaultAccess;
        }

        public bool CanView(UserID user) => GetACL(user) >= WorldAccessInfoLevel.View;

        public bool CanPin(UserID user) => GetACL(user) >= WorldAccessInfoLevel.Pin;

        public static PublicWorldData OfflineWorld()
        {
            return new()
            {
                Name = "Somewhere",
                Creator = null,
                ScreenshotCid = null,
                WorldFP = null,
                Permissions = null,
                DefaultAccess = WorldAccessInfoLevel.Nothing,
                DeniedUsers = new(),
                VisitorUsers = new(), // TODO Server admins
                PinningUsers = new(),
                Description = "The server is just started..."
            };
        }
    }

    public static partial class Extensions
    {
        /// <summary>
        /// Extract the public data from the full meta data
        /// <para><b>Important:</b> Ensure that WorldInfo and ScreenshotCid is loaded!</para>
        /// </summary>
        /// <param name="world"></param>
        /// <returns></returns>
        public static PublicWorldData PublicData(this World world)
        {
            WorldInfo wi = (WorldInfo)world.WorldInfo;
            WorldAccessInfo wai = wi.AccessInfo;

            WorldAccessInfoLevel defaultLevel;
            IEnumerable<Fingerprint> deniedUsers;
            IEnumerable<Fingerprint> visitorUsers;
            IEnumerable<Fingerprint> pinningUsers;

            if (wai != null)
            {
                // For brevity, leave out the users which have the same access level 
                defaultLevel = wai.DefaultLevel;
                if (defaultLevel == WorldAccessInfoLevel.Nothing)
                    deniedUsers = Enumerable.Empty<Fingerprint>();
                else
                    deniedUsers = from entry in wai.UserALs
                                  where entry.Value == WorldAccessInfoLevel.Nothing
                                  select new Fingerprint(entry.Key);

                if (defaultLevel == WorldAccessInfoLevel.View)
                    visitorUsers = Enumerable.Empty<Fingerprint>();
                else
                    visitorUsers = from entry in wai.UserALs
                                   where entry.Value == WorldAccessInfoLevel.View
                                   select new Fingerprint(entry.Key);

                if (defaultLevel == WorldAccessInfoLevel.Pin)
                    pinningUsers = Enumerable.Empty<Fingerprint>();
                else
                    pinningUsers = from entry in wai.UserALs
                                   where entry.Value >= WorldAccessInfoLevel.Pin
                                   select new Fingerprint(entry.Key);
            }
            else
            {
                defaultLevel = WorldAccessInfoLevel.Pin;
                deniedUsers = Enumerable.Empty<Fingerprint>();
                visitorUsers = Enumerable.Empty<Fingerprint>();
                pinningUsers = Enumerable.Empty<Fingerprint>();
            }

            return new()
            {
                Name = wi.WorldName,
                Creator = wi.Author,
                ScreenshotCid = (Cid)world.ScreenshotCid,
                WorldFP = new Fingerprint(world.RootCid),
                Permissions = wi.ContentRating,
                DefaultAccess = defaultLevel,
                DeniedUsers = deniedUsers.ToList(),
                VisitorUsers = visitorUsers.ToList(),
                PinningUsers = pinningUsers.ToList(),
                Description = wi.WorldDescription
            };
        }
    }
}