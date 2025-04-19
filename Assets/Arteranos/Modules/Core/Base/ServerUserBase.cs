/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.ComponentModel;

namespace Arteranos.Core
{
    public struct UserState
    {
        // Just an ordinary everyday user.
        public const ulong Normal = 0;

        // ---------------------------------------------------------------
        // 0  ... 15: User related

        // Trusted user, whatever it shall be... ¯\_(ツ)_/¯
        public const ulong Trusted = (ulong) 1 << 0;

        // ---------------------------------------------------------------
        // 16 ... 31: World related

        // Derived from the world administrator, can elevate or demote assistants with the same powers
        public const ulong World_admin_asstnt = (ulong) 1 << 30;

        // World administrators
        public const ulong World_admin = (ulong) 1 << 31;

        // ---------------------------------------------------------------
        // 32 ... 47: Server related

        // Derived from the world administrator, can elevate or demote assistants with the same powers
        public const ulong Srv_admin_asstnt = (ulong) 1 << 46;

        // Server administrators
        public const ulong Srv_admin = (ulong) 1 << 47;


        // ---------------------------------------------------------------
        // 48 ... 63: Repercussions, especially with the regative result (= banned bit set)

        // Below the user's legal or recommended age, e.g. below 13 b/c COPPA.
        public const ulong Underage = (ulong) 1 << 48;

        // Disruptive behavior
        public const ulong Trolling = (ulong) 1 << 49;

        // Discrimination/-phobic/hatemongering (see below)
        public const ulong Hating = (ulong) 1 << 50;

        // Incessantly loud (e.g. noisy mic) (Maybe automatically force-muted on login?)
        public const ulong Loud = (ulong) 1 << 51;

        // Mobbing/Bullying/Harassment
        public const ulong Bullying = (ulong) 1 << 52;

        // Undesired sexual advances or harassment
        public const ulong SxHarassment = (ulong) 1 << 53;

        // Exploit/security leak usage (maybe tripped in the network stack)
        public const ulong Exploiting = (ulong) 1 << 60;

        // Impersonation (to gain privileges by social engineering)
        public const ulong Impersonation = (ulong) 1 << 61;

        // Attempted ban evasion
        public const ulong BanEvading = (ulong) 1 << 62;

        // User has been banned
        public const ulong Banned = (ulong) 1 << 63;

        // All of the 'good' bits.
        public const ulong GOOD_MASK = ((ulong)1 << 48) - 1;

        public static bool IsBadGuy(ulong field) => Bit64field.IsAny(field, ~GOOD_MASK);

        public static bool IsBanned(ulong field) => Bit64field.IsAny(field, Banned);

        public static bool IsWAdmin(ulong field) => Bit64field.IsAny(field, World_admin | World_admin_asstnt);

        public static bool IsSAdmin(ulong field) => Bit64field.IsAny(field, Srv_admin | Srv_admin_asstnt);
    }

    public static class  BanHandling
    {
        internal struct BanReasonEntry
        {
            public string description;
            public ulong reasonBit;
            public string reasonText;

            public BanReasonEntry(string description, ulong reasonBit, string reasonText)
            {
                this.description = description;
                this.reasonBit = reasonBit;
                this.reasonText = reasonText;
            }
        }

        internal static readonly BanReasonEntry[] reasons = new BanReasonEntry[]
        {
            new("Pick a reason...",     0,                          "Please specify..."),
            new("Underage",             UserState.Underage,         "Below the legal or recommended age, e.g. 13 b/c COPPA"),
            new("Trolling",             UserState.Trolling,         "Disruptive behavior"),
            new("Hate Speech",          UserState.Hating,           "Discrimination/-phobic/hatemongering"),
            new("Loud/Disruptive",      UserState.Loud,             "Incessantly loud, even with repeated muting"),
            new("Bullying",             UserState.Bullying,         "Mobbing/Bullying/Harassment"),
            new("Sexual Harassment",    UserState.SxHarassment,     "Undesired sexual advances or harassment"),
            new("Exploit Use",          UserState.Exploiting,       "Exploit/security leak usage"),
            new("Impersonation",        UserState.Impersonation,    "Impersonation/Deliberately false representation"),
            new("Ban Evasion",          UserState.BanEvading,       "Attempted Ban evasion"),
            new("Other...",             0,                          "Other, please specify the detailed reason")
        };

        public static string FindBanReason(ulong userState)
        {
            if (!UserState.IsBanned(userState)) return null; // Not banned at all.

            IEnumerable<string> q = from entry in reasons
                                    where entry.reasonBit != 0 && Bit64field.IsAll(userState, entry.reasonBit)
                                    select entry.description;

            return q.Count() != 0 ? q.First() : null;
        }

        public static IEnumerable<string> ReasonList(bool includezeroes = true)
            => from reason in reasons
               where includezeroes || reason.reasonBit != 0 
               select reason.description;

        public static string GetReasonText(int index) => reasons[index].reasonText;

        public static ulong GetReasonBit(int index) => reasons[index].reasonBit;
    }


    public class UserPrivacy
    {
        public Visibility Visibility = Visibility.Online;
        public UserVisibility UIDVisibility;
        public UIDRepresentation UIDRepresentation;
        public UserVisibility TextReception;
    }

    public enum UserVisibility
    {
        [Description("everyone")]
        everyone = 0,
        [Description("friends only")]
        friends = 1,
        [Description("no one")]
        none = 2
    }

    public enum UIDRepresentation
    {
        [Description("8 characters")]
        base64_8,
        [Description("15 characters")]
        base64_15,
        [Description("4 words")]
        Dice_4,
        [Description("5 words")]
        Dice_5
    }

    public class ServerUserBase
    {
        public List<ServerUserState> Base = new();

        public const string PATH_SERVER_USERBASE = "ServerUserBase.json";

        public static bool MatchElement<T>(T entry, T query) 
            => query == null || query.Equals(entry);

        public static bool Match(ServerUserState entry, ServerUserState query)
        {
            return
                MatchElement(entry.userID, query.userID) &&
                MatchElement(entry.address, query.address) &&
                MatchElement(entry.deviceUID, query.deviceUID);
        }

        public static bool MatchOR(ServerUserState entry, ServerUserState query)
        {
            return
                MatchElement(entry.userID, query.userID) ||
                MatchElement(entry.address, query.address) ||
                MatchElement(entry.deviceUID, query.deviceUID);
        }

        public IEnumerable<ServerUserState> FindUsers(ServerUserState query)
        {
            return from entry in Base
                   where Match(entry, query)
                   select entry;
        }

        public IEnumerable<ServerUserState> FindUsersOR(ServerUserState query)
        {
            return from entry in Base
                   where MatchOR(entry, query)
                   select entry;
        }

        public void RemoveUsers(ServerUserState removals)
        {
            ServerUserState[] newBase = (from entry in Base
                          where !Match(entry, removals)
                          select entry).ToArray();

            Base.Clear();
            foreach(ServerUserState entry in newBase)
                Base.Add(entry);
        }

        public void AddUser(ServerUserState user)
        {
            if (user.userState == UserState.Normal) return;

            Base.Add(user);
        }

        private void AddRootSA(bool init)
        {
            Client cs = G.Client;
            if(cs == null)
            {
                Debug.LogWarning("Client Settings is not initialized - skipping");
                return;
            }

            UserID userID = cs.MeUserID;
            ServerUserState user = new()
            {
                userID = userID,
                userState = UserState.Srv_admin,
                address = null,
                deviceUID = null,
                remarks = $"Auto-generated root user, derived from owner's ID ({(string) userID})"
            };

            if(FindUsers(user).Any())
            {
                RemoveUsers(user);
                init = true; // Just removed, re-add with the new UserID.
            }

            if (init)
            {
                AddUser(user);
                Save();
            }
        }

        public static ServerUserBase Load()
        {
            ServerUserBase sub = null;
            bool needSave = false;

            if (G.CommandLineOptions.ClearServerUserBase)
            {
                // Clear the Server User Base
                sub = new();
                needSave = true;
            }
            else
            {
                try
                {
                    sub = JsonConvert.DeserializeObject<ServerUserBase>(ConfigUtils.ReadTextConfig(PATH_SERVER_USERBASE));
                }
                catch (Exception)
                {
                    sub = new();
                    // New file, add the default server administrator
                    sub.AddRootSA(true);
                }
            }

            // Refresh default server administrator, in case the nickname has been changed
            sub.AddRootSA(false);

            TypeConverter typeConverter = new UserIDConverter();
            foreach (string entry in G.CommandLineOptions.AddServerAdmins)
            {
                try
                {
                    UserID userID = (UserID)typeConverter.ConvertFrom(entry);
                    ServerUserState user = new()
                    {
                        userID = userID,
                        userState = UserState.Srv_admin,
                        address = null,
                        deviceUID = null,
                        remarks = $"Commandline added root user ({(string)userID})"
                    };

                    sub.AddUser(user);
                    needSave = true;
                }
                catch (Exception ex) 
                { 
                    Debug.LogException(ex);
                }
            }

            if(needSave) sub.Save();

            // Just configure the server root admin database and quit.
            if(G.CommandLineOptions.ClearServerUserBase || G.CommandLineOptions.AddServerAdmins.Count > 0)
            {
                Debug.Log("Server User Base modified by commandline options, remove the options and restart");
                SettingsManager.Quit();
            }

            return sub;
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                ConfigUtils.WriteTextConfig(PATH_SERVER_USERBASE, json);
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Failed to save server user base: {e.Message}");
            }
        }


    }
}
