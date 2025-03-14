/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.WorldEdit;
using Ipfs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;


namespace Arteranos.Core
{

    public class Community
    {
        private readonly Dictionary<MultiHash, (HashSet<string>, DateTime) > UsersHosts = new();

        private readonly Dictionary<MultiHash, Cid> WorldHosts = new();

        public void UpdateServerUsers(MultiHash peerID, HashSet<string> userFPs, DateTime stamp)
            => UsersHosts[peerID] = (userFPs, stamp);

        public void UpdateServerWorld(MultiHash peerID, Cid worldID)
            => WorldHosts[peerID] = worldID;

        public void DownServer(MultiHash peerID)
        {
            UsersHosts.Remove(peerID);
            WorldHosts.Remove(peerID);
        }
        
        public IEnumerable<MultiHash> FindServersHostingWorld(Cid world)
        {
            return from entry in WorldHosts
                    where entry.Value == world
                    select entry.Key;
        }

        public MultiHash FindFriend(string friendFP)
        {
            // Lazy server still lists your friend who just switched servers
            IEnumerable<(MultiHash peer, DateTime time)> q = from entry in UsersHosts
                   where entry.Value.Item1.Contains(friendFP)
                   select (entry.Key, entry.Value.Item2);

            // Most recent online data would be the winner
            MultiHash found = null;
            DateTime foundTime = DateTime.MinValue;
            foreach ((MultiHash peer, DateTime time) in q)
                if(time > foundTime)
                {
                    found = peer;
                    foundTime = time;
                }

            return found;
        }

    }
}
