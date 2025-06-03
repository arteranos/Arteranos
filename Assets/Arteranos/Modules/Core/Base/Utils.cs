/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using System;

using System.ComponentModel;
using System.Reflection;
using System.Text;
using Arteranos.Avatar;
using Arteranos.Social;
using UnityEngine.UI;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;
using AssetBundle = Arteranos.Core.Managed.AssetBundle;
using ICSharpCode.SharpZipLib.Tar;
using Arteranos.Common;

namespace Arteranos.Core
{
    public static class Utils
    {
        /// <summary>
        /// Allows to tack on a Description attribute to enum values, e.g. a display name.
        /// </summary>
        /// <param name="enumVal">The particular value of the enum set</param>
        /// <returns>The string in the value's description, null if there isn't</returns>
        public static string GetEnumDescription(Enum enumVal)
        {
            MemberInfo[] memInfo = enumVal.GetType().GetMember(enumVal.ToString());
            DescriptionAttribute attribute = CustomAttributeExtensions.GetCustomAttribute<DescriptionAttribute>(memInfo[0]);
            return attribute?.Description;
        }

        /// <summary>
        /// Generate a directory of a hashed URL suitable for a cache directory tree.
        /// </summary>
        /// <param name="url">The URL</param>
        /// <returns>the two directory levels, without a root path</returns>
        public static string GetURLHash(string url)
        {
            Hash128 hash = new();
            byte[] bytes = Encoding.UTF8.GetBytes(url);
            hash.Append(bytes);
            string hashstr = hash.ToString();

            string hashed = $"{hashstr[0..2]}/{hashstr[2..]}";
            return hashed;
        }

        /// <summary>
        /// The world cache directory. Safe to delete, and deleting force reloads.
        /// </summary>
        public static readonly string WorldCacheRootDir = $"{ConfigUtils.temporaryCachePath}/WorldCache";

        /// <summary>
        /// Simulate a RC circuit (a capacitor and resistor) to measure the capacitor's charge,
        /// used in for example a VU meter. 
        /// </summary>
        /// <param name="value">Current input voltage</param>
        /// <param name="charge">The resulting charge</param>
        /// <param name="kCharge">The charging factor</param>
        /// <param name="kDischarge">The discharging factor</param>
        public static void CalcVU(float value, ref float charge, float kCharge = 0.1f, float kDischarge = 0.05f)
        {
            value = Mathf.Abs(value);

            if(value > charge)
                charge = (charge * (1 - kCharge)) + (value * kCharge);
            else
                charge *= (1 - kDischarge);
        }

        /// <summary>
        /// Fout = 10^(Q/20) * Fin
        /// </summary>
        /// <param name="dBvalue"></param>
        /// <returns>Ife plain factor.</returns>
        public static float LoudnessToFactor(float dBvalue) => MathF.Pow(10.0f, dBvalue / 10.0f);

        /// <summary>
        /// Get the user ID fingerprint's encoding according to visibility's User ID settings
        /// </summary>
        /// <param name="fpmodeSet">The setting in question (your, or the nameplate user's setting)</param>
        /// <returns>The CryptoHelper's ToString() parameter</returns>
        public static string GetUIDFPEncoding(UIDRepresentation fpmodeSet)
        {
            return fpmodeSet switch
            {
                UIDRepresentation.base64_8 => CryptoHelpers.FP_Base64_8,
                UIDRepresentation.base64_15 => CryptoHelpers.FP_Base64_15,
                UIDRepresentation.Dice_4 => CryptoHelpers.FP_Dice_4,
                UIDRepresentation.Dice_5 => CryptoHelpers.FP_Dice_5,
                _ => CryptoHelpers.FP_Base64
            };
        }

        /// <summary>
        /// Advances the tweened value towards the target value with the given timespan
        /// </summary>
        /// <param name="current">The current value, normalized</param>
        /// <param name="target">The targeet value, normalized</param>
        /// <param name="duration">How many seconds to take it across the whole scale</param>
        public static void Tween(ref float current, float target, float duration)
        {
            if (duration == 0) // That would not be tweening, but rather setting the value.
            {
                current = target;
                return;
            }

            if (target > current) current += Time.deltaTime / duration;
            if (target < current) current -= Time.deltaTime / duration;
            current = Mathf.Clamp01(current);
        }

        /// <summary>
        /// Determine if you are able to do the action in the question (to the targeted user)
        /// </summary>
        /// <param name="cap">The action you want to do</param>
        /// <param name="target">The targeted user you want to do for (or, against)</param>
        /// <returns>Self explanatory.</returns>
        public static bool IsAbleTo(UserCapabilities cap, IAvatarBrain target) 
            => IsAbleTo(G.Me, cap, target);

        public static bool IsAbleTo(IAvatarBrain source, UserCapabilities cap, IAvatarBrain target)
        {
            // You are offline, it has to be your own computer.
            if (source == null) return true;

            return source.IsAbleTo(cap, target);
        }

        public struct Paginated<T>
        {
            public int page;
            public int maxPage;
            public T[] payload;
        }

        /// <summary>
        /// Chops an array of T into smaller chunks
        /// </summary>
        /// <typeparam name="T">The payload data type</typeparam>
        /// <param name="whole">the data to cut into pages</param>
        /// <param name="page">Current page, from 1 up to including maxPage, or 0 to only get the number of pages</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>the data</returns>
        public static Paginated<T> Paginate<T>(T[] whole, int page, int pageSize = 25)
        {
            int maxPage = (whole.Length + pageSize - 1) / pageSize;
            int ceil = page * pageSize;
            if(whole.Length < ceil) ceil = whole.Length;

            return new Paginated<T>()
            {
                page = page,
                maxPage = maxPage,
                payload = page <= maxPage && page > 0 
                ? whole[((page - 1) * pageSize)..(ceil)] 
                : null
            };  
        }

        /// <summary>
        /// Advance one frame for exponential smoothing
        /// (ref. https://en.wikipedia.org/wiki/Exponential_smoothing )
        /// </summary>
        /// <param name="smoothed">The moving average throughout the data points</param>
        /// <param name="input">The current data point</param>
        /// <param name="tconst">Time in seconds where the average goes back</param>
        public static void AdvanceSmoothing(ref float smoothed, float input, float tconst)
        {
            float alpha = Time.deltaTime / tconst;
            smoothed =  alpha * input + (1  - alpha) * smoothed;
        }

        /// <summary>
        /// Returns a human-readable value with the appropiate number prefixes and the measure unit
        /// </summary>
        /// <param name="value">The value to describe</param>
        /// <returns>The human readable string</returns>
        public static string Magnitude(long value, string suffix = "B")
        {
            float val = value;
            string[] prefixes = { "", "k", "M", "G", "T", "E" };
            for(int i = 0; i < prefixes.Length - 1; i++)
            {
                if (val < 900) return (i > 0)
                        ? string.Format("{0:F1} {1}{2}", val, prefixes[i], suffix)
                        : string.Format("{0:F0} {1}", val, suffix);
                // SI numbers prefixes, sorry, no powers of two...
                val /= 1000;
            }
            return string.Format("{0:F1} {1}{2}", val, prefixes[^1], suffix);
        }

        public static void ShowImage(Texture2D icon, Image image)
        {
            image.sprite = Sprite.Create(icon,
                new Rect(0, 0, icon.width, icon.height),
                Vector2.zero);
        }

        public static void RateGameObject(GameObject go, IObjectStats warn, IObjectStats cutoff, IObjectStats counted)
        {
            static float Rate(int actual, int l1, int l2)
            {
                if (actual > l2) return -0.40f; // Beyond 'red' mark
                if (actual > l1) return -0.20f; // Beyond 'yellow' mark
                return 0.0f; // Green
            }

            counted.Count = 0;
            counted.Vertices = 0;
            counted.Triangles = 0;
            counted.Materials = 0;
            counted.Rating = 1.0f;

            CountGameObject(go.transform, counted);

            counted.Rating += Rate(counted.Count, warn.Count, cutoff.Count);
            counted.Rating += Rate(counted.Vertices, warn.Vertices, cutoff.Vertices);
            counted.Rating += Rate(counted.Triangles, warn.Triangles, cutoff.Triangles);
            counted.Rating += Rate(counted.Materials, warn.Materials, cutoff.Materials);

            counted.Rating = Mathf.Clamp01(counted.Rating);
        }

        public static void CountGameObject(Transform t, IObjectStats counted)
        {
            Mesh m = null;
            SkinnedMeshRenderer smr = t.gameObject.GetComponent<SkinnedMeshRenderer>();
            if (smr)
            {
                m = smr.sharedMesh;
                counted.Materials += smr.materials.Length;
            }
            MeshFilter mf = t.gameObject.GetComponent<MeshFilter>();
            if (mf) 
            { 
                m = mf.sharedMesh;
            }
            MeshRenderer mr = t.gameObject.GetComponent<MeshRenderer>();
            if(mr)
            {
                counted.Materials += mr.materials.Length;
            }

            if (m)
            {
                counted.Count++;
                counted.Triangles += m.triangles.Length;
                counted.Vertices += m.vertices.Length;
            }

            foreach(Transform transform in t)
                CountGameObject(transform, counted);
        }

        // TODO - Move into common space to make it accessible to kit loading
        public static async Task<AssetBundle> LoadAssetBundle(string path, Action<long, long> reportProgress = null, CancellationToken cancel = default)
        {
            AssetBundle resultAB = null;
            SemaphoreSlim waiter = new(0, 1);
            bool fallback = false;

            IEnumerator Cor()
            {
                AssetBundle manifestAB = null;
                string adName = Common.Utils.GetArchitectureDirName();
                string ipfsPath = $"{path}/{adName}/{adName}";
                yield return AssetBundle.LoadFromIPFS(ipfsPath, _result => manifestAB = _result, cancel: cancel);

                if (manifestAB == null)
                {
                    Debug.LogWarning($"{ipfsPath} platform specific AssetBundle doesn't exist, falling back to the Windows one and attempting to fixup.");
                    adName = Common.Utils.GetArchitectureDirName(RuntimePlatform.WindowsPlayer);
                    yield return AssetBundle.LoadFromIPFS($"{path}/{adName}/{adName}", _result => manifestAB = _result, cancel: cancel);

                    fallback = true;
                }

                if (manifestAB == null)
                {
                    Debug.LogWarning($"...Not even the 'Windows' AssetBundle exists, trying 'AssetBundles'...");

                    adName = "AssetBundles";
                    yield return AssetBundle.LoadFromIPFS($"{path}/{adName}/{adName}", _result => manifestAB = _result, cancel: cancel);

                    fallback = true;
                }

                if (manifestAB != null)
                {
                    AssetBundleManifest manifest = ((UnityEngine.AssetBundle)manifestAB).LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    string actualABName = manifest.GetAllAssetBundles()[0];

                    yield return AssetBundle.LoadFromIPFS($"{path}/{adName}/{actualABName}", _result => resultAB = _result, reportProgress, cancel);

                    manifestAB.Dispose();
                }
                else
                    Debug.LogError("No suitable AssetBundle found, giving up.");

                if(resultAB != null) resultAB.IsFallback = fallback;
                waiter.Release();
            }

            TaskScheduler.ScheduleCoroutine(Cor);

            await waiter.WaitAsync();

            return resultAB;
        }

        public static async Task UnTarToDirectoryAsync(Stream fs, string path, CancellationToken token = default)
        {
            string common = null;

            using (TarInputStream tar = new(fs, Encoding.UTF8))
            {
                tar.IsStreamOwner = false;
                for (TarEntry entry = tar.GetNextEntry(); entry != null; entry = tar.GetNextEntry())
                {
                    if (entry.IsDirectory) continue;

                    common ??= entry.Name;

                    int i = common.CommonStart(entry.Name);
                    common = entry.Name[0..i];
                }
            }

            fs.Seek(0, SeekOrigin.Begin);
            int cutoff = common.Length;

            using (TarInputStream tar = new(fs, Encoding.UTF8))
            {
                for (TarEntry entry = tar.GetNextEntry(); entry != null; entry = tar.GetNextEntry())
                {
                    if (entry.IsDirectory) continue;

                    string filePath = $"{path}/{entry.Name[cutoff..]}";
                    string dirName = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

                    using Stream stream = File.Create(filePath);
                    await tar.CopyEntryContentsAsync(stream, token);
                }
            }
        }
    }
}
