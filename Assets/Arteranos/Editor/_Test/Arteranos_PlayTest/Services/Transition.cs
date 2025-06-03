using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Arteranos.Services;
using System.IO;
using System.Threading.Tasks;
using Arteranos.Core;
using Ipfs;
using UnityEditor;
using Arteranos.Core.Operations;
using Arteranos.XR;
using Arteranos.Core.Managed;
using AssetBundle = Arteranos.Core.Managed.AssetBundle;
using System;
using Ipfs.Unity;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Arteranos.PlayTest.Services
{
    public class TransitionTest
    {
        private const string PlainFileAsset = "Assets/Arteranos/Editor/_Test/Sceelix_Abbey.zip";
        private string FileURLAsset => $"file:///{PlainFileAsset}";
        Cid WorldCid = null;


        IPFSService service = null;

        [UnitySetUp]
        public IEnumerator Setup0()
        {
            TestFixtures.IPFSServiceFixture(ref service);

            yield return TestFixtures.StartIPFSAndWait(service);
        }

        [UnityTearDown]
        public IEnumerator TearDown0()
        {
            G.XRVisualConfigurator.StartFading(0, 0);
            yield return null;
        }

        private IEnumerator UploadTestWorld()
        {
            (AsyncOperationExecutor<Context> ao, Context co) =
                AssetUploader.PrepareUploadToIPFS(FileURLAsset, true);

            yield return ao.ExecuteCoroutine(co);

            WorldCid = AssetUploader.GetUploadedCid(co);

            Assert.IsNotNull(WorldCid);
        }


        [UnityTest]
        public IEnumerator T001_InAndOut()
        {
            yield return new WaitForSeconds(1);

            yield return TransitionProgress.TransitionFrom();

            yield return new WaitForSeconds(2);

            yield return TransitionProgress.TransitionTo(null);

            yield return new WaitForSeconds(2);
        }

        [UnityTest]
        public IEnumerator T002_ProgressMonitoring()
        {
            yield return new WaitForSeconds(10);

            yield return TransitionProgress.TransitionFrom();

            for(int i = 0; i < 10; i++)
            {
                float progress = (float)i / (float)10;
                G.TransitionProgress.OnProgressChanged(progress, $"Progress {i}");
                yield return new WaitForSeconds(1f);
            }

            yield return TransitionProgress.TransitionTo(null);

            yield return new WaitForSeconds(2);
        }

        [UnityTest]
        public IEnumerator T003_ProgressMonitoringFromAsync()
        {
            yield return new WaitForSeconds(10);

            yield return TransitionProgress.TransitionFrom();

            // Same as before, but in a worker thread, not in a Coroutine
            Task t = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    float progress = (float)i / (float)10;
                    G.TransitionProgress.OnProgressChanged(progress, $"Progress {i}");

                    await Task.Delay(1000);
                }
            });

            // Wait for the task to be done.
            yield return new WaitUntil(() => t.IsCompleted);

            yield return TransitionProgress.TransitionTo(null);

            yield return new WaitForSeconds(2);
        }

        [UnityTest]
        public IEnumerator T004_ManagedAssetBundle()
        {
            static void ReportProgress(long bytes, long total)
            {
                Debug.Log($"{bytes} out of {total}");
            }

            yield return UploadTestWorld();

            AssetBundle ab_ab = null;
            yield return AssetBundle.LoadFromIPFS($"{WorldCid}/{Common.Utils.GetArchitectureDirName()}/{Common.Utils.GetArchitectureDirName()}", _result => ab_ab = _result);

            Assert.IsNotNull(ab_ab);
            Assert.IsNotNull((UnityEngine.AssetBundle)ab_ab);

            AssetBundleManifest manifest = ((UnityEngine.AssetBundle) ab_ab).LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            foreach(string abname in manifest.GetAllAssetBundles())
                Debug.Log(abname);

            string actualABName = manifest.GetAllAssetBundles()[0];

            AssetBundle actual_ab = null;
            yield return AssetBundle.LoadFromIPFS(
                $"{WorldCid}/{Common.Utils.GetArchitectureDirName()}/{actualABName}",
                _result => actual_ab = _result,
                ReportProgress);

            Assert.IsNotNull((UnityEngine.AssetBundle)actual_ab);

            ab_ab.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                UnityEngine.AssetBundle raw_ab = ab_ab;
            });
        }

        public async Task<AssetBundle> LoadAssetBundle(string path, Action<long, long> reportProgress = null, CancellationToken cancel = default)
        {
            AssetBundle resultAB = null;
            SemaphoreSlim waiter = new(0, 1);

            IEnumerator Cor()
            {
                AssetBundle manifestAB = null;
                yield return AssetBundle.LoadFromIPFS($"{path}/{Common.Utils.GetArchitectureDirName()}/{Common.Utils.GetArchitectureDirName()}", _result => manifestAB = _result, cancel: cancel);

                if(manifestAB != null)
                {
                    AssetBundleManifest manifest = ((UnityEngine.AssetBundle)manifestAB).LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    string actualABName = manifest.GetAllAssetBundles()[0];

                    yield return AssetBundle.LoadFromIPFS($"{path}/{Common.Utils.GetArchitectureDirName()}/{actualABName}", _result => resultAB = _result, reportProgress, cancel);

                    manifestAB.Dispose();
                }

                waiter.Release();
            }

            Core.TaskScheduler.ScheduleCoroutine(Cor);

            await waiter.WaitAsync();

            return resultAB;
        }

        [UnityTest]
        public IEnumerator T006_LazyAsyncAssetBundle()
        {
            static void ReportProgress(long bytes, long total)
            {
                Debug.Log($"{bytes} out of {total}");
            }

            yield return UploadTestWorld();

            Stopwatch sw = Stopwatch.StartNew();
            AsyncLazy<AssetBundle> laab = new(() => LoadAssetBundle(WorldCid, ReportProgress));
            Assert.IsTrue(sw.ElapsedMilliseconds < 2);

            sw.Restart();
            yield return laab.WaitFor();
            Debug.Log($"{sw.ElapsedMilliseconds} ms");
            Assert.IsTrue(sw.ElapsedMilliseconds > 100);

            sw.Restart();
            Assert.IsNotNull(laab);
            Assert.IsNotNull((AssetBundle)laab);
            Assert.IsTrue(sw.ElapsedMilliseconds < 2);

            ((AssetBundle) laab).Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                AssetBundle ab = laab;
                UnityEngine.AssetBundle native = ab;
            });
        }

        [UnityTest]
        public IEnumerator T007_FailNonExistent()
        {
            static void ReportProgress(long bytes, long total)
            {
                Debug.Log($"{bytes} out of {total}");
            }

            Stopwatch sw = Stopwatch.StartNew();
            AsyncLazy<AssetBundle> laab = new(() => LoadAssetBundle("foo", ReportProgress));
            Assert.IsTrue(sw.ElapsedMilliseconds < 2);

            yield return laab.WaitFor();
            Assert.IsNull((AssetBundle) laab);
        }

        [UnityTest]
        public IEnumerator T010_TransitionWorld()
        {
            static void ReportProgress(long bytes, long totalBytes)
            {
                float ratio = bytes / (totalBytes + float.Epsilon);

                string bytesMag = Core.Utils.Magnitude(bytes);
                string totalBytesMag = Core.Utils.Magnitude(totalBytes);

                string msg = totalBytes == 0
                    ? "Downloading..."
                    : $"Downloading {bytesMag} from {totalBytesMag}";

                G.TransitionProgress.OnProgressChanged(ratio, msg);
            }

            yield return new WaitForSeconds(10);

            yield return UploadTestWorld();

            yield return TransitionProgress.TransitionFrom();

            World world = WorldCid;

            world.OnReportingProgress += ReportProgress;

            yield return world.TemplateContent.WaitFor();
            yield return world.DecorationContent.WaitFor();

            world.OnReportingProgress -= ReportProgress;

            yield return new WaitForSeconds(2);

            yield return TransitionProgress.TransitionTo(world);

            yield return new WaitForSeconds(1);
        }
    }
}