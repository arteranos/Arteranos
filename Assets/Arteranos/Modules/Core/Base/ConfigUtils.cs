/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Arteranos.Core
{
    public static class ConfigUtils
    {
        // Learned from work:
        // It's better to not to condition out the code, because it still always checks for errors
        // in compilation time, and those errors won't only pop up in the full release builds.
        //
        // Additionally you can switch the switch in runtime for debugging...
#if UNITY_SERVER
        public static bool Unity_Server = true;
#else
        public static bool Unity_Server = false;

#endif

        // These are _string constants_. Why aren't they callable from worker threads?!
        private static readonly string _persistentDataPath = Application.persistentDataPath;
        private static readonly string _temporaryCachePath = Application.temporaryCachePath;
        private static readonly string _productName = Application.productName;

#pragma warning disable IDE1006 // Benennungsstile
        // Arteranos.Common and SDK MAY be included into other projects (like, premade worlds) with different names.
        // So remove the project name from the path and replace it with Arteranos

        public static string GetPathWithArteranos(string providedPathName, bool? isServer)
        {
            string destName = isServer ?? Unity_Server
                ? "Arteranos_DedicatedServer"
                : "Arteranos";

            return $"{providedPathName[0..^_productName.Length]}{destName}";
        }
        public static string persistentDataPath => GetPathWithArteranos(_persistentDataPath, null);

        public static string temporaryCachePath => GetPathWithArteranos(_temporaryCachePath, null);

#pragma warning restore IDE1006 // Benennungsstile

        public static bool NeedsFallback(string path)
            => Unity_Server && !File.Exists($"{persistentDataPath}/{path}");

        public static byte[] ReadBytesConfig(string path) => ReadConfig(path, File.ReadAllBytes);

        public static string ReadTextConfig(string path) => ReadConfig(path, File.ReadAllText);

        public static void WriteBytesConfig(string path, byte[] data) => WriteConfig(path, File.WriteAllBytes, data);

        public static Task WriteBytesConfigAsync(string path, byte[] data)
        {
            return WriteConfigAsync(path, (path, data) => File.WriteAllBytesAsync(path, data), data);
        }

        public static void WriteTextConfig(string path, string data) => WriteConfig(path, File.WriteAllText, data);

        public static Task WriteTextConfigAsync(string path, string data)
        {
            return WriteConfigAsync(path, (path, data) => File.WriteAllTextAsync(path, data), data);
        }

        public static T ReadConfig<T>(string path, Func<string, T> reader)
        {
            string fullPath = $"{persistentDataPath}/{path}";

            if (Unity_Server)
            {
                if (File.Exists(fullPath)) return reader(fullPath);
                Debug.LogWarning($"{fullPath} doesn't exist - falling back to regular file.");
            }

            fullPath = $"{GetPathWithArteranos(_persistentDataPath, false)}/{path}";
            return reader(fullPath);
        }

        private static void WriteConfig<T>(string path, Action<string, T> writer, T data)
        {
            string fullPath = $"{persistentDataPath}/{path}";
            if (Unity_Server && !Directory.Exists(persistentDataPath))
                Directory.CreateDirectory(persistentDataPath);

            writer(fullPath, data);
        }

        private static Task WriteConfigAsync<T>(string path, Func<string, T, Task> writer, T data)
        {
            string fullPath = $"{persistentDataPath}/{path}";
            if (Unity_Server && !Directory.Exists(persistentDataPath))
                Directory.CreateDirectory(persistentDataPath);

            return writer(fullPath, data);
        }
    }
}