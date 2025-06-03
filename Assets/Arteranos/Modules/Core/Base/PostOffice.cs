/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arteranos.Core
{
    public class MessageEntryJSON
    {
        public DateTime Date = DateTime.MinValue;
        public string Nickname = null;
        public UserID UserID = null;
        public string Text = null;
    }

    public class MessageStashJSON
    {
        public List<MessageEntryJSON> incoming = new();
        public List<MessageEntryJSON> outgoing = new();
    }

    public static class PostOffice
    {
        public const string PATH_POST_OFFICE = "Messages.json";

        private static MessageStashJSON stash = null;
        private static bool dirty = false;

        public static void Save()
        {
            if(!dirty) return;

            try
            {
                string json = JsonConvert.SerializeObject(stash, Formatting.Indented);
                ConfigUtils.WriteTextConfig(PATH_POST_OFFICE, json);
                dirty = false;
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Failed to save the message stash: {e.Message}");
            }
        }

        public static void Load()
        {
            try
            {
                string json = ConfigUtils.ReadTextConfig(PATH_POST_OFFICE);
                stash = JsonConvert.DeserializeObject<MessageStashJSON>(json);
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Failed to load the message stash: {e.Message}");
                stash = new();
            }

            dirty = false;
        }

        public static void EnqueueIncoming(UserID sender, string senderNickname, string text)
        {
            Client cs = G.Client;

            // Look for your friend list if the sender is a friend - use the global UserID
            IEnumerable<KeyValuePair<UserID, UserSocialEntryJSON>> q = cs.GetSocialList(sender);

            // Either it's the scoped UserID, exactly matching, or the global UserID. 
            if(q.Count() > 0) sender = q.First().Key;

            stash.incoming.Add(new()
            {
                Date = DateTime.Now,
                Nickname = senderNickname,
                Text = text,
                UserID = sender
            });

            dirty = true;
        }

        public static int PeekIncoming(UserID sender, out MessageEntryJSON message)
        {
            message = null;

            IEnumerable<MessageEntryJSON> q = sender == null
                ? from entry in stash.incoming select entry
                : from entry in stash.incoming where entry.UserID == sender select entry;

            int n = q.Count();

            if(n == 0) return n;

            message = q.First();

            return n;
        }

        public static int DequeueIncoming(UserID sender, out MessageEntryJSON message)
        {
            int n = PeekIncoming(sender, out message);
            if(message != null)
            {
                stash.incoming.Remove(message);
                dirty = true;
            }

            return n;
        }

        public static void DiscardIncoming(UserID sender = null)
        {
            if(sender == null)
            {
                stash.incoming.Clear();
                dirty = true;
                return;
            }

            MessageEntryJSON[] q = 
                (from entry in stash.incoming where entry.UserID == sender select entry).ToArray();

            for(int i = 0; i < q.Length; i++)
                stash.incoming.Remove(q[i]);
        }
    }
}