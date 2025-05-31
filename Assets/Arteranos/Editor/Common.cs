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

    public class Common
    {
        private static string _resourceDirectory = null;

        public static string ResourceDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_resourceDirectory))
                {
                    UnityEditor.PackageManager.PackageInfo p = UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
                    if (p != null)
                        _resourceDirectory = $"Packages/{p.name}/Resources";
                }

                if (String.IsNullOrEmpty(_resourceDirectory))
                {
                    // Find ourselves first, then go from there to the Resources folder.
                    string[] g = AssetDatabase.FindAssets("t:Script UrpInstaller");
                    if (g.Length > 0)
                    {
                        _resourceDirectory = AssetDatabase.GUIDToAssetPath(g[0]);
                        _resourceDirectory = Path.GetDirectoryName(_resourceDirectory);
                        _resourceDirectory = Path.GetDirectoryName(_resourceDirectory);
                        _resourceDirectory = $"{_resourceDirectory}/Resources";
                    }
                }

                return _resourceDirectory;
            }
        }
        public static void DisplayStatus(string caption, string defaultText, string activeText, string goodText = null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(caption, GUILayout.Width(150.0f));

            GUIStyle style = new() { fontStyle = FontStyle.Bold };

            if (activeText == null)
            {
                style.normal.textColor = new Color(0.62f, 0, 0);
                EditorGUILayout.LabelField(defaultText, style);
            }
            else
            {
                if (goodText == null || activeText == goodText)
                    style.normal.textColor = new Color(0, 0.62f, 0);
                else
                    style.normal.textColor = new Color(0.2f, 0.2f, 0);

                EditorGUILayout.LabelField(activeText, style);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

        }

        /// <summary>
        /// Parses a string like "2021-01-06T08:52:02.432-08:00" to a UTC DateTime timestamp
        /// </summary>
        /// <param name="str">The string to parse</param>
        /// <returns>the timestamp in UTC</returns>
        public static DateTime ParseTimeString(string str)
        {
            int years = int.Parse(str[..4]);
            int months = int.Parse(str.Substring(5, 2));
            int days = int.Parse(str.Substring(8, 2));

            int hours = int.Parse(str.Substring(11, 2));
            int minutes = int.Parse(str.Substring(14, 2));
            int seconds = int.Parse(str.Substring(17, 2));

            int len = str.Length;

            int offs_hours = int.Parse(str.Substring(len - 6, 3));
            int offs_minutes = int.Parse(str.Substring(len - 2, 2));

            if (offs_hours < 0) offs_minutes = -offs_minutes;

            DateTime t = new(years, months, days, hours, minutes, seconds);
            // *remove* the timezone offset
            t = t.AddHours(-offs_hours);
            t = t.AddMinutes(-offs_minutes);

            return t;
        }


        /// <summary>
        /// Offers a combined text entry/file selection dialog for the editor GUI
        /// </summary>
        /// <param name="label">Label of item</param>
        /// <param name="folder">Select a folder, not a file</param>
        /// <param name="save">Select to save, rather than to open</param>
        /// <param name="path">Suggested path (absolute or relative to project root)</param>
        /// <param name="extension">when saving a file, the file extension</param>
        /// <returns>The path to the item. Unchanged if the dialog has been canceled.</returns>
        public static string FileSelectionField(GUIContent label, bool folder, bool save, string path, string extension = null)
        {
            EditorGUILayout.BeginHorizontal();

            path = EditorGUILayout.TextField(label, path);

            if (GUILayout.Button("...", GUILayout.Width(20)))
            {
                string newPath = OpenFileDialog(path, folder, save, extension);
                if (!string.IsNullOrEmpty(newPath))
                    path = newPath;
            }

            EditorGUILayout.EndHorizontal();
            return path;
        }

        // The file and directory dialog need some serious boilerplate to actually be useful.
        public static string OpenFileDialog(string path, bool folder, bool save, string extension)
        {
            string newPath = folder
                ? save
                    ? EditorUtility.SaveFolderPanel("Select directory to save to", path, "")
                    : EditorUtility.OpenFolderPanel("Select directory to open", path, "")
                : save
                    ? EditorUtility.SaveFilePanel("Select file to save to", path, "", extension)
                    : EditorUtility.OpenFilePanel("Select file to open", path, "");
            if (newPath.StartsWith(Application.dataPath))
                newPath = "Assets" + newPath[Application.dataPath.Length..];

            return newPath;
        }

        /// <summary>
        /// Replace interpunction characters in a filename with '_'
        /// </summary>
        /// <param name="filename">Filename or path element name</param>
        /// <returns>Sanitized path element name</returns>
        public static string SanitizeFileName(string filename)
        {
            char[] chars = new char[filename.Length];

            for (int i = 0; i < filename.Length; i++)
            {
                char c = filename[i];
                chars[i] = (!Char.IsLetterOrDigit(c) && c != '_' && c != '.') ? '_' : c;
            }

            return new string(chars);
        }

        /// <summary>
        /// Create a temporary directory with a unique name
        /// </summary>
        /// <returns>Directory path</returns>
        public static string CreateTempDirectory()
        {
            string tmpDirName;
            do
            {
                tmpDirName = $"{Path.GetTempPath()}/{Path.GetRandomFileName()}";
            } while (Directory.Exists(tmpDirName));

            Directory.CreateDirectory(tmpDirName);
            return tmpDirName;
        }

        /// <summary>
        /// Build a AltspaceVR compliant asset Bundle zip out of the given data
        /// </summary>
        /// <param name="assetFiles">Input files (Kit Prefabs or file with preformatted scene)</param>
        /// <param name="screenshotFiles">Kits: Screenshots to kit items</param>
        /// <param name="architectures">Architectures to build for</param>
        /// <param name="tgtRootName">Target root name (must match upload file name of zip file)</param>
        /// <param name="targetFileName">File name to locally save to (incl. .zip) or null to open dialog</param>
        /// <returns>The chosen filename</returns>
        public static string BuildAssetBundle(string[] assetFiles, List<BuildTarget> architectures, string tgtRootName, string metadataTxt = null, string screenshotFile = null, string targetFileName = null)
        {
            string tmpSaveLocation = CreateTempDirectory();

            tgtRootName = SanitizeFileName(tgtRootName).ToLower();

            targetFileName ??= OpenFileDialog($"{Application.dataPath}/{tgtRootName}.zip", false, true, "zip");

            if (string.IsNullOrEmpty(targetFileName))
            {
                Debug.Log("Build has been canceled.");
                return null;
            }

            if (!string.IsNullOrEmpty(screenshotFile))
                File.Copy(screenshotFile, $"{tmpSaveLocation}/Screenshot{Path.GetExtension(screenshotFile)}", true);

            if (!string.IsNullOrEmpty(metadataTxt))
                File.WriteAllText($"{tmpSaveLocation}/Metadata.json", metadataTxt);

            AssetBundleBuild[] abb =
            {
                new()
                {
                    assetBundleName = tgtRootName,
                    assetNames = assetFiles
                }
            };

            foreach (BuildTarget architecture in architectures)
            {
                string assetBundlesSave =
                    $"{tmpSaveLocation}/{GetArchitectureDirName(architecture)}";

                if (!Directory.Exists(assetBundlesSave))
                    Directory.CreateDirectory(assetBundlesSave);

                BuildPipeline.BuildAssetBundles(
                    assetBundlesSave,
                    abb,
                    BuildAssetBundleOptions.StrictMode,
                    architecture);
            }

            File.Delete(targetFileName);
            CreateZip(tmpSaveLocation, targetFileName);

            Directory.Delete(tmpSaveLocation, true);
            return targetFileName;
        }

        public static string GetArchitectureDirName(BuildTarget p)
        {
            RuntimePlatform r = p switch
            {
                BuildTarget.StandaloneOSX => RuntimePlatform.OSXPlayer,
                BuildTarget.Android => RuntimePlatform.Android,
                BuildTarget.StandaloneLinux64 => RuntimePlatform.LinuxPlayer,
                _ => RuntimePlatform.WindowsPlayer,
            };

            return Utils.GetArchitectureDirName(r);
        }

        public static Dictionary<BuildTarget, bool> supported_cache = new();

        /// <summary>
        /// Checks if build support for the given platform is loaded
        /// </summary>
        /// <param name="target">The target platform</param>
        /// <returns>true if build support is present</returns>
        public static bool IsBuildTargetSupported(BuildTarget target)
        {
            if (!supported_cache.TryGetValue(target, out bool res))
            {
                Type moduleManager = Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");

                MethodInfo getTargetStringFromBuildTarget = moduleManager.GetMethod(
                    "GetTargetStringFromBuildTarget",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo isPlatformSupportLoaded = moduleManager.GetMethod(
                    "IsPlatformSupportLoaded",
                    BindingFlags.Static | BindingFlags.NonPublic);

                string targetString = (string)getTargetStringFromBuildTarget.Invoke(null, new object[] { target });
                res = (bool)isPlatformSupportLoaded.Invoke(null, new object[] { targetString });

                supported_cache[target] = res;

                if (!res)
                    Debug.LogWarning("Build Support '" + targetString + "' is not installed, building for this platform will be disabled.");
            }

            return res;
        }

        public static void CreateZip(string sourceDirectory, string outputFile) => ZipFile.CreateFromDirectory(sourceDirectory, outputFile);
    }

    /// <summary>
    /// Creates a directory which will be deleted as soon as this object
    /// falls out of scope.
    /// </summary>
    public class TempDir : IDisposable
    {
        string dir = null;

        public TempDir(string directory)
        {
            this.dir = directory;
            Directory.CreateDirectory(dir);
        }

        public static implicit operator string(TempDir s) => s.dir;

        public override string ToString() => dir;

        public static implicit operator TempDir(string s) => new(s);

        /// <summary>
        /// Removes the scope binding, making the directory permanent
        /// </summary>
        public void Detach() => dir = null;

        public void Dispose()
        {
            if (dir == null) return;

            if (!Directory.Exists(dir)) return;

            Directory.Delete(dir, true);

#if UNITY_EDITOR
            string metaFile = $"{dir}.meta";
            if (File.Exists(metaFile))
            {
                File.Delete(metaFile);
                AssetDatabase.Refresh();
            }
#endif

            dir = null;
        }
    }
}