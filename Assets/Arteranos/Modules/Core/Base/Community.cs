/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Ipfs;
using System;
using System.Collections.Generic;
using System.Linq;
using Arteranos.Common;
using Arteranos.Common.Cryptography;


namespace Arteranos.Core
{

    public class Community
    {
        private readonly Dictionary<MultiHash, (HashSet<Fingerprint>, DateTime) > UsersHosts = new();

        private readonly Dictionary<MultiHash, Fingerprint> WorldHosts = new();

        public void UpdateServerUsers(MultiHash peerID, HashSet<Fingerprint> userFPs, DateTime stamp)
            => UsersHosts[peerID] = (userFPs, stamp);

        public void UpdateServerWorld(MultiHash peerID, Fingerprint worldFP)
            => WorldHosts[peerID] = worldFP;

        public void DownServer(MultiHash peerID)
        {
            UsersHosts.Remove(peerID);
            WorldHosts.Remove(peerID);
        }
        
        public IEnumerable<MultiHash> FindServersHostingWorld(Fingerprint world)
        {
            return from entry in WorldHosts
                    where entry.Value == world
                    select entry.Key;
        }

        public (MultiHash server, Fingerprint worldFP) FindFriend(UserID friend)
            => FindFriend(new Fingerprint(friend));

        public (MultiHash server, Fingerprint worldFP) FindFriend(Fingerprint friendFP)
        {
            // Lazy server still lists your friend who just switched servers
            IEnumerable<(MultiHash peer, DateTime time)> q = from entry in UsersHosts
                                                             where entry.Value.Item1.Contains(friendFP)
                                                             select (entry.Key, entry.Value.Item2);

            // Most recent online data would be the winner
            MultiHash found = null;
            DateTime foundTime = DateTime.MinValue;
            foreach ((MultiHash peer, DateTime time) in q)
                if (time > foundTime)
                {
                    found = peer;
                    foundTime = time;
                }

            return found != null
                ? (found, WorldHosts.ContainsKey(found) ? WorldHosts[found] : null)
                : (null, null);
        }

        public IEnumerable<UserID> FindFriends(MultiHash peerID)
        {
            // None at all. Server is offline.
            if (!UsersHosts.ContainsKey(peerID)) yield break;

            (HashSet<Fingerprint> fps, DateTime dt) = UsersHosts[peerID];

            foreach ((UserID target, bool friendOffered, bool friendReceived, bool blockImposed) in G.UserData.GetAllStates())
            {
                // Yikes!
                if (blockImposed) continue;

                // Not a true friend
                if (!friendOffered || !friendReceived) continue;

                // Not In this server
                if (!fps.Contains(new Fingerprint(target))) continue;

                // Intersect server's user list with the own friend list
                // TODO #89, #208 -- exclude (not so much) friends hiding from you in restricted worlds? 
                yield return target;
            }
        }
    }
}
