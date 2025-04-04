/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

using Debug = UnityEngine.Debug;
using Newtonsoft.Json;
using System;
using Unity.EditorCoroutines.Editor;
using System.Collections;
using System.Threading.Tasks;
using Arteranos.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System.Collections.Generic;
using System.Text;

namespace Arteranos.Editor
{

    public class BuildPlayers
    {
        // public static string appName = Application.productName;
        public static readonly string appName = "Arteranos";

        public static Core.Version Version { get; private set; } = null;

        public static void GetProjectGitVersion()
        {
            ProcessStartInfo psi = new()
            {
                FileName = "git",
                Arguments = "describe --tags --long",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(psi);
            using StreamReader reader = process.StandardOutput;

            string data = reader.ReadToEnd();

            Debug.Log($"Git says: {data}");


            string[] parts = data.Split('-');

            Version = Core.Version.Parse(parts[0][1..]);

            Version.Hash = parts[^1][1..^1];     // Remove the 'g' before the commit hash and the LF
            Version.B = parts[^2];
            Version.MMP = $"{Version.Major}.{Version.Minor}.{Version.Patch}";
            Version.MMPB = $"{Version.MMP}.{Version.B}";

            if(parts.Length > 3)
                Version.Tag = "-"+string.Join("-", parts[1..^2]);

            Version.Full = $"{Version.MMP}.{Version.B}{Version.Tag}-{Version.Hash}";

            string json = JsonConvert.SerializeObject(Version, Formatting.Indented);
            TextAsset textAsset = new(json);

            if(!Directory.Exists("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");

            if(!Directory.Exists("Assets/Generated/Resources"))
                AssetDatabase.CreateFolder("Assets/Generated", "Resources");

            AssetDatabase.CreateAsset(textAsset, "Assets/Generated/Resources/Version.asset");

            textAsset = new();

            string WiXFileText =
@"<?xml version='1.0' encoding='utf-8' ?>

<?define version = """+ Version.MMP + @""" ?>
<?define fullversion = """+ Version.Full + @""" ?>

<Include xmlns='http://schemas.microsoft.com/wix/2006/wi'>
  
</Include>
";
            if(!Directory.Exists("build"))
                Directory.CreateDirectory("build");

            File.WriteAllText("build/WiXVersion.wxi",WiXFileText);
        }

        public static void UpdateLicenseFiles()
        {
            File.Copy("LICENSE.md", "Assets\\Generated\\Resources\\LICENSE.md", true);
            File.Copy("Third Party Notices.md", "Assets\\Generated\\Resources\\Third Party Notices.md", true);

            AssetDatabase.Refresh();
        }

        public static void BumpForceReloadFile()
        {
            // Put a timestamp in the RFC3339 datetime format.
            // Contents doesn't matter, only when it's CHANGING after a platform switch!

            File.WriteAllText("Assets/Generated/dummy.cs", @"
// Automatically generated file -- EDITS WILL BE OVERWRITTEN

#pragma warning disable IDE1006
public static class _dummy
{
    public static string creationTime = """ + DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK") + @""";
}
"
// Intended to have direct code generation -- nothing like resources.... not yet.
#if false
+ @"
namespace Arteranos.Core
{
    public partial class VersionDefaults
    {
        public void Defaults(Version v)
        {
            v.MMP = """ + Version.MMP + @""";
            v.MMPB = """ + Version.MMPB + @""";
            v.B = """ + Version.B + @""";
            v.Hash = """ + Version.Hash + @""";
            v.Tag = """ + Version.Tag + @""";
            v.Full = """ + Version.Full + @""";
        }
    }
}
"
#endif
);
            AssetDatabase.Refresh();
        }

        [MenuItem("Arteranos/Build/Update version and platform", false, 120)]
        public static void SetVersion()
        {
            GetProjectGitVersion();

            UpdateLicenseFiles();

            BumpForceReloadFile();

            Core.Version v = Core.Version.Load();
            Debug.Log($"Version detected: Full={v.Full}, M.M.P={v.MMP}");
        }

        [MenuItem("Arteranos/Build/Build Windows64", false, 140)]
        public static void BuildWin64()
        {
            static IEnumerator SingleTask()
            {
                GetProjectGitVersion();
                yield return BuildWin64Coroutine();
            }

            EditorCoroutineUtility.StartCoroutineOwnerless(SingleTask());
        }

        [MenuItem("Arteranos/Build/Build Windows Dedicated Server", false, 150)]
        public static void BuildWin64DedServ()
        {
            static IEnumerator SingleTask()
            {
                GetProjectGitVersion();
                yield return BuildWin64DSCoroutine();
            }

            EditorCoroutineUtility.StartCoroutineOwnerless(SingleTask());
        }

        [MenuItem("Arteranos/Build/Build Linux64 Dedicated Server", false, 160)]
        public static void BuildLinux64DedServ()
        {
            static IEnumerator SingleTask()
            {
                GetProjectGitVersion();
                yield return BuildLinux64DSCoroutine();
            }

            EditorCoroutineUtility.StartCoroutineOwnerless(SingleTask());
        }

        [MenuItem("Arteranos/Run/Run Client's IPFS daemon", false, 20)]
        public static void RunClientIPFSDaemon()
        {
            string RepoDir = $"{Application.persistentDataPath}/.ipfs";
            string IPFSExePath = $"{Environment.GetEnvironmentVariable("ProgramData")}\\arteranos\\arteranos\\ipfs.exe";

            string argLine = $"--repo-dir={RepoDir} daemon --enable-pubsub-experiment";

            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = IPFSExePath,
                    Arguments = argLine,
                }
            };

            process.Start();
        }

        [MenuItem("Arteranos/Build/Build Installation Package (Linux)", false, 81)]
        public static void BuildLinuxInstallationPackage()
        {
            static IEnumerator SingleTask()
            {
                yield return BuildDebianPackageCoroutine();
            }

            EditorCoroutineUtility.StartCoroutineOwnerless(SingleTask());
        }

        public static string[] GetSceneNames()
        {
            return new[] {
                "Assets/Arteranos/Modules/Core/Other/_Startup.unity",
                "Assets/Arteranos/Modules/OfflineScene/OfflineScene.unity",
                "Assets/Arteranos/Modules/Core/Other/Transition.unity"
            };
        }

        private static IEnumerator BuildWin64Coroutine()
        {
            yield return CommenceBuild(new BuildPlayerOptions()
            {
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
            });
        }

        private static IEnumerator BuildWin64DSCoroutine()
        {

            yield return CommenceBuild(new BuildPlayerOptions()
            {
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
            });
        }

        private static IEnumerator BuildLinux64DSCoroutine()
        {

            yield return CommenceBuild(new BuildPlayerOptions()
            {
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
            });
        }

        private static IEnumerator Execute(string command, string argline, string cwd = "build")
        {
            ProcessStartInfo psi = new()
            {
                FileName = command,
                Arguments = argline,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = cwd,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(psi);
            using StreamReader reader = process.StandardOutput;

            Task t = Task.Run(() => process.WaitForExit());

            yield return new WaitUntil(() => t.IsCompleted);

            string data = reader.ReadToEnd();
            Debug.Log(data);

            yield return null;
        }

        public static IEnumerator BuildDebianPackageCoroutine()
        {
            int progressId = Progress.Start("Building...");

            try
            {
                GetProjectGitVersion();

                if (true)
                {
                    Progress.Report(progressId, 0.40f, "Build Linux Dedicated Server");

                    yield return BuildLinux64DSCoroutine();

                }

                Progress.Report(progressId, 0.40f, "Creating Debian Package");

                string systemroot = Environment.GetEnvironmentVariable("SystemRoot");

                yield return Execute($"{systemroot}\\system32\\cmd.exe", "/c build.bat" + $" {Version.Major} {Version.Minor} {Version.Patch}", "Setup-Linux");

                Debug.Log("Finished.");

            }
            finally
            {
                Progress.Remove(progressId);
            }

        }

        private static IEnumerator CommenceBuild(BuildPlayerOptions bpo)
        {
            yield return null;
            BuildSummary summary = default;

            bool isServer = bpo.subtarget == (int)StandaloneBuildSubtarget.Server;
            bool isLinux = bpo.target == BuildTarget.StandaloneLinux64;

            string buildTargetDir = $"{(isServer ? "server" : "desktop")}-{(isLinux ? "Linux" : "Win")}-amd64";
            string buildTargetName = $"{appName}{(isServer ? "-Server" : "")}{(isLinux ? "" : ".exe")}";

#if true
            bpo.locationPathName = $"build/{buildTargetDir}/{buildTargetName}";
            bpo.scenes = GetSceneNames();
            bpo.options = BuildOptions.CleanBuildCache;
            bpo.extraScriptingDefines = isServer ? new[] { "UNITY_SERVER" } : new string[0];

            string buildLocation = Path.GetDirectoryName(bpo.locationPathName);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, bpo.target);
            EditorUserBuildSettings.standaloneBuildSubtarget = (StandaloneBuildSubtarget)bpo.subtarget;
            EditorUserBuildSettings.SetBuildLocation(BuildTarget.StandaloneWindows64, $"{buildLocation}/");

            BuildReport report = BuildPipeline.BuildPlayer(bpo);
#endif
            summary = report.summary;

            if (summary.result == BuildResult.Unknown)
            {
                Debug.Log("No build run.");
                yield break;
            }
            else if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build unsuccesful: {summary.result}");
                yield break;
            }

            yield return CreateBuildOutputTar(buildTargetDir);

            yield return HashBuildOutput(buildTargetDir);
            
            Debug.Log($"Build succeeded: {summary.totalSize} bytes, {summary.totalTime} time.");
        }

        private static IEnumerator CreateBuildOutputTar(string buildTargetDir)
        {
            IEnumerator CreateTarGZ(string tgzFilename, string sourceDirectory)
            {
                Stream outStream = File.Create(tgzFilename);
                Stream gzoStream = new GZipOutputStream(outStream);
                TarArchive tarArchive = TarArchive.CreateOutputTarArchive(gzoStream);

                // Note that the RootPath is currently case sensitive and must be forward slashes e.g. "c:/temp"
                // and must not end with a slash, otherwise cuts off first char of filename
                // This is scheduled for fix in next release
                tarArchive.RootPath = sourceDirectory.Replace('\\', '/');
                if (tarArchive.RootPath.EndsWith("/"))
                    tarArchive.RootPath = tarArchive.RootPath.Remove(tarArchive.RootPath.Length - 1);

                // Omit the (empty) root directory entry
                yield return AddDirectoryFilesToTar(tarArchive, sourceDirectory, true, false);

                tarArchive.Close();
            }

            IEnumerator AddDirectoryFilesToTar(TarArchive tarArchive, string sourceDirectory, bool recurse, bool self = true)
            {
                TarEntry tarEntry = null;
                if (self)
                {
                    // Optionally, write an entry for the directory itself.
                    // Specify false for recursion here if we will add the directory's files individually.
                    tarEntry = TarEntry.CreateEntryFromFile(sourceDirectory);

                    tarEntry.TarHeader.Mode = 493;  // drwxr-xr-x
                    tarArchive.WriteEntry(tarEntry, false);
                }

                // Write each file to the tar.
                string[] filenames = Directory.GetFiles(sourceDirectory);
                foreach (string filename in filenames)
                {
                    yield return null;

                    tarEntry = TarEntry.CreateEntryFromFile(filename);

                    tarEntry.GroupId = 1000;
                    tarEntry.GroupName = "arteranos";
                    tarEntry.UserId = 1000;
                    tarEntry.UserName = "arteranos";
                    tarEntry.TarHeader.Mode = 33261;    // -rwxr-xr-x (100755, with regular file)
                    tarArchive.WriteEntry(tarEntry, true);
                }

                if (recurse)
                {
                    string[] directories = Directory.GetDirectories(sourceDirectory);
                    foreach (string directory in directories)
                    {
                        // Obvious enough.
                        if (directory.EndsWith("DoNotShip")) continue;

                        yield return AddDirectoryFilesToTar(tarArchive, directory, recurse);
                    }
                }
            }

            Debug.Log("Creating build tarball");

            yield return CreateTarGZ($"build/{buildTargetDir}.tar.gz", $"build/{buildTargetDir}");
        }

        private class FileEntry
        {
            public string Cid;
            public string Path;
            public long Size;
        }

        private static IEnumerator HashBuildOutput(string buildTargetDir)
        {
            List<FileEntry> newList = new();

            IEnumerator HashEntriesChunk(StringBuilder fileArgs, string rootPath)
            {
                string IPFSExePath = $"{Environment.GetEnvironmentVariable("ProgramData")}\\arteranos\\arteranos\\ipfs.exe";

                ProcessStartInfo psi = new()
                {
                    FileName = IPFSExePath,
                    Arguments = $"add --only-hash {fileArgs}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };


                using Process process = Process.Start(psi);
                using StreamReader reader = process.StandardOutput;
                string data = reader.ReadToEnd();

                string[] lines = data.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] columns = line.Split(' ');
                    if (columns.Length < 3 || (columns.Length > 0 && columns[0] != "added"))
                    {
                        Debug.LogWarning($"Unrecognized response: {line}");
                        continue;
                    }

                    string cid = columns[1];
                    string file = string.Join(' ', columns[2..]);

                    string path = $"{rootPath}{file}";

                    FileInfo fileInfo = new($"build/{buildTargetDir}/{path}");

                    newList.Add(new()
                    {
                        Cid = cid,
                        Path = path,
                        Size = fileInfo.Length
                    });
                }

                yield return null;
            }

            IEnumerator HashEntries(string dir, string rootPath)
            {
                foreach (string entry in Directory.GetDirectories(dir))
                {
                    // Obvious enough.
                    if (entry.EndsWith("DoNotShip")) continue;

                    yield return HashEntries(entry, $"{rootPath}{Path.GetFileName(entry)}/");
                }

                StringBuilder fileArgs = new();

                foreach(string entry in Directory.GetFiles(dir))
                {
                    if(fileArgs.Length > 0) fileArgs.Append(" ");
                    fileArgs.Append("\"");
                    fileArgs.Append(entry);
                    fileArgs.Append("\"");

                    if(fileArgs.Length > 512)
                    {
                        yield return HashEntriesChunk(fileArgs, rootPath);
                        fileArgs = new();
                    }
                }

                if(fileArgs.Length > 0)
                    yield return HashEntriesChunk(fileArgs, rootPath);
            }

            Debug.Log("Creating file hashes");

            yield return HashEntries($"build/{buildTargetDir}", "");

            string json = JsonConvert.SerializeObject(newList, Formatting.Indented);
            File.WriteAllText($"build/{buildTargetDir}-FileList.json", json);
        }
    }
}
