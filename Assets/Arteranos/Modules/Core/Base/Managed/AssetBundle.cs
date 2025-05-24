/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Ipfs;
using Ipfs.Unity;
using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;


namespace Arteranos.Core.Managed
{
    /// <summary>
    /// Managed version of AssetBundle, to let it only live as long as the
    /// reference is held.
    /// </summary>
    public class AssetBundle : ManagedHandle<UnityEngine.AssetBundle>
    {
        private UnityEngine.AssetBundle InternalAssetBundle
        {
            get => resource;
            set => Attach(value);
        }

        public AssetBundle() : base(
            () => null, 
            r => { Disposer(r); }
            )
        { }

        public static implicit operator UnityEngine.AssetBundle(AssetBundle assetBundle) 
            => assetBundle?.InternalAssetBundle;

        public static implicit operator AssetBundle(UnityEngine.AssetBundle assetBundle)
            => new() { InternalAssetBundle = assetBundle };

        public bool IsFallback { get; set; } = false;

        public static IEnumerator LoadFromIPFS(string path, Action<AssetBundle> result, Action<long, long> reportProgress = null, CancellationToken cancel = default)
        {
            Cid cid = null;
            yield return Asyncs.Async2Coroutine(() => G.IPFSService.ResolveToCid(path, cancel), _result => cid = _result, ex => { });
            if (cid == null) yield break;

            IFileSystemNode fsn = null;
            yield return Asyncs.Async2Coroutine(() => G.IPFSService.ListFile(cid, cancel), _result => fsn = _result, ex => { });

            // No 'stat' implementation, just add the block sizes
            long totalBytes = 0;
            foreach (IFileSystemLink link in fsn.Links)
                totalBytes += link.Size;

            Action<long> rp = reportProgress != null ? (b) => reportProgress(b, totalBytes) : null;

            MemoryStream ms = null;
            yield return Asyncs.Async2Coroutine(() => G.IPFSService.ReadIntoMS(path, rp, cancel), _result => ms = _result, ex => { });

            AssetBundleCreateRequest abc = UnityEngine.AssetBundle.LoadFromStreamAsync(ms);

            yield return new WaitUntil(() => abc.isDone);

            AssetBundle ab = abc.assetBundle;

            result.Invoke(ab);
        }

        private static void Disposer(UnityEngine.AssetBundle b)
        {
            static IEnumerator Cor(UnityEngine.AssetBundle b)
            {
                if(b) b.Unload(true);
                yield return null;
            }

            TaskScheduler.ScheduleCoroutine(() => Cor(b));
        }
    }
}