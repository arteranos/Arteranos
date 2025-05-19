/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using Newtonsoft.Json;
using System;

namespace Arteranos.Core
{
    public class Version
    {
        public const string VERSION_MIN = "3.0.1";

        public static readonly Version MinVersion = Parse(VERSION_MIN);

        public string MMP = "0.0.0";    // Major, minor, patch
        public string MMPB = "0.0.0.0"; // Major, minor, patch, build
        public string B = "0";          // build
        public string Hash = "0000000"; // Abbreviated commit hash
        public string Tag = "";         // Optional tag
        public string Full = "unknown"; // Full version string

        [JsonIgnore]
        public int Major { get; private set; } = 0;
        [JsonIgnore]
        public int Minor { get; private set; } = 0;
        [JsonIgnore]
        public int Patch { get; private set; } = 0;
        [JsonIgnore]
        public int Build { get; private set; } = 0;

        public static Version Load()
        {
            TextAsset ta = Resources.Load<TextAsset>("Version");
            if (ta == null) return null;

            Version newVer = JsonConvert.DeserializeObject<Version>(ta.text);

            newVer.ParseInPlace(newVer.MMPB);

            return newVer;
        }

        private void ParseInPlace(string text)
        {
            string[] parts = text.Split('.');

            Major = int.Parse(parts[0]);
            if (parts.Length > 1) Minor = int.Parse(parts[1]);
            if (parts.Length > 2) Patch = int.Parse(parts[2]);
            if (parts.Length > 3) Build = int.Parse(parts[3]);
        }

        public static Version Parse(string text)
        {
            Version newVer = new();
            newVer.ParseInPlace(text);
            return newVer;
        }

        private static bool CompareVersion(Version lhs, Version rhs, Func<int, int, bool> comparer)
        {
            // Indeterminate when they're equal - both (>=, <=) or neither (>, <) being true.
            static bool? TripleComparer(int left, int right, Func<int, int, bool> comparer) 
            {
                bool resultTrue = comparer(left, right);
                bool resultFalse = comparer(right, left);

                if (!resultTrue ^ resultFalse) return null;
                return resultTrue;
            }

            // Waterfall through the most impotant down to the less important values.
            bool? result = TripleComparer(lhs.Major, rhs.Major, comparer);
            result ??= TripleComparer(lhs.Minor, rhs.Minor, comparer);
            result ??= TripleComparer(lhs.Patch, rhs.Patch, comparer);
            result ??= TripleComparer(lhs.Build, rhs.Build, comparer);

            // If it's still indeterminate, ask the comparer what it says about proven equal values.
            return result ?? comparer(0, 0);
        }

        public static bool operator <(Version lhs, Version rhs)
            => CompareVersion(lhs, rhs, (l, r) => l < r);

        public static bool operator >(Version lhs, Version rhs)
            => CompareVersion(lhs, rhs, (l, r) => l > r);

        public static bool operator <=(Version lhs, Version rhs)
            => CompareVersion(lhs, rhs, (l, r) => l <= r);

        public static bool operator >=(Version lhs, Version rhs)
            => CompareVersion(lhs, rhs, (l, r) => l >= r);
    }
}
