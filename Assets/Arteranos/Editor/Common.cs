/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Core;
using Arteranos.Core.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Arteranos.Editor
{
    public static class Extensions
    {
        public static string Capitalize(this string str) => str[..1].ToUpper() + str[1..];

        public static string ToShortDTString(this DateTime dt) => dt.ToShortDateString() + ", " + dt.ToShortTimeString();
    }
}