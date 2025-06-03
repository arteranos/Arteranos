/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.IO;
using System.Threading.Tasks;

using System.Threading;
using Ipfs;
using System.Net.Http;
using UnityEngine;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;

namespace Arteranos.Core.Operations
{
    internal class UploadDirectoryToIPFS : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 1.0f;
        public string Caption { get => GetProgressText(); }
        public Action<float> ProgressChanged { get; set; }

        private string GetProgressText()
        {
            return "Uploading...";
        }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AssetUploaderContext context = _context as AssetUploaderContext;

            string path = $"{context.TempFile}.dir";

            try
            {
                IFileSystemNode fsn = await G.IPFSService.AddDirectory(path, cancel: token);
                context.Cid = fsn.Id;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }

            Debug.Log($"Resulting CID: {context.Cid}");
            return context;
        }
    }

    internal class UnZipFileOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 2.0f;
        public string Caption { get; set; } = "Uncompressing file";
        public Action<float> ProgressChanged { get; set; }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AssetUploaderContext context = _context as AssetUploaderContext;

            return await Task.Run(() =>
            {
                try
                {
                    string path = $"{context.TempFile}.dir";

                    if (Directory.Exists(path)) Directory.Delete(path, true);

                    ZipFile.ExtractToDirectory(context.TempFile, path);

                    return context;
                }
                finally
                {
                    if (File.Exists(context.TempFile)) File.Delete(context.TempFile);
                }
            });
        }
    }

    internal class UnTarFileOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 2.0f;
        public string Caption { get; set; } = "Uncompressing file";
        public Action<float> ProgressChanged { get; set; }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AssetUploaderContext context = _context as AssetUploaderContext;

            try
            {
                string path = $"{context.TempFile}.dir";

                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                using Stream fs = File.OpenRead(context.TempFile);

                await Utils.UnTarToDirectoryAsync(fs, path, token);

                return context;
            }
            finally
            {
                if (File.Exists(context.TempFile)) File.Delete(context.TempFile);
            }
        }
    }

    internal class UploadFileToIPFS : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 1.0f;
        public string Caption { get => GetProgressText(); }
        public Action<float> ProgressChanged { get; set; }

        private long actualBytes = 0;
        private long totalBytes = -1;
        private string totalBytesMag = null;

        private string GetProgressText()
        {
            if (totalBytesMag == null || totalBytes <= 0) return "Uploading...";

            return $"Uploading ({Utils.Magnitude(actualBytes)} of {totalBytesMag})...";
        }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            using CancellationTokenSource cts = new();

            AssetUploaderContext context = _context as AssetUploaderContext;

            try
            {
                FileInfo fileInfo = new(context.TempFile);
                totalBytes = fileInfo.Length;
                totalBytesMag = Utils.Magnitude(totalBytes);

                using Stream stream = File.OpenRead(context.TempFile);

                _ = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (actualBytes != stream.Position)
                        {
                            actualBytes = stream.Position;
                            ProgressChanged((float)actualBytes / totalBytes);
                        }
                        await Task.Delay(10);
                    }
                });

                IFileSystemNode fsn = await G.IPFSService.AddStream(stream, "", cancel: token);
                stream.Close();

                context.Cid = fsn.Id;
            }
            finally
            {
                cts.Cancel();
                if (File.Exists(context.TempFile)) File.Delete(context.TempFile);
            }

            return context;
        }
    }

    internal class DownloadFromWeb : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 1.0f;
        public string Caption { get => GetProgressText(); }
        public Action<float> ProgressChanged { get; set; }

        private long actualBytes = 0;
        private long totalBytes = -1;
        private string totalBytesMag = null;

        private string GetProgressText()
        {
            if (totalBytesMag == null || totalBytes <= 0) return "Downloading...";

            return $"Downloading ({Utils.Magnitude(actualBytes)} of {totalBytesMag})...";
        }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {

            byte[] LBA(string assetURL)
            {
                IEnumerator LBACoroutine(string assetURL, Action<byte[]> callback)
                {
                    TextAsset ta = Resources.Load<TextAsset>(assetURL);
                    yield return null;
                    callback(ta.bytes);
                }

                byte[] ta = null;
                TaskScheduler.ScheduleCoroutine(() => LBACoroutine(assetURL, _ta => ta = _ta));

                while (ta == null) Thread.Yield();
                return ta;
            }

            Stream inStream = null;

            AssetUploaderContext context = _context as AssetUploaderContext;

            string assetURL = context.AssetURL;

            // Strip quotes
            if (assetURL.StartsWith("\"") && assetURL.EndsWith("\""))
            {
                assetURL = assetURL[1..^1];
                context.AssetURL = assetURL;
            }

            // Strip 'file:///' prefix
            if (assetURL.StartsWith("file:///"))
                assetURL = assetURL[8..];

            if (assetURL.StartsWith("http://") || assetURL.StartsWith("https://"))
            {
                // Deal with web resources
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromSeconds(Timeout);
                using HttpResponseMessage response = await client.GetAsync(assetURL);
                response.EnsureSuccessStatusCode();
                totalBytes = response.Content.Headers.ContentLength ?? -1;
                inStream = await response.Content.ReadAsStreamAsync();
            }
            else if (assetURL.StartsWith("resource:///"))
            {
                assetURL = assetURL[12..];

                byte[] ta = LBA(assetURL);
                inStream = new MemoryStream(ta);
            }
            else
            {
                // Deal with local (file) resources
                FileInfo fileInfo = new(assetURL);
                totalBytes = fileInfo.Length;
                inStream = File.OpenRead(assetURL);
            }
            totalBytesMag = Utils.Magnitude(totalBytes);


            using FileStream outStream = File.Create(context.TempFile);

            await Common.Utils.CopyWithProgress(inStream, outStream,
                bytes =>
                {
                    actualBytes = bytes;
                    ProgressChanged((float)bytes / totalBytes);
                }, token);

            return context;
        }
    }

    public static class AssetUploader
    {
        public static (AsyncOperationExecutor<Context>, Context) PrepareUploadToIPFS(string assetURL, bool asTarred, int timeout = 600, bool pin = true)
        {
            AssetUploaderContext context = new()
            {
                AssetURL = assetURL,
                asTarred = asTarred,
                TempFile = Path.GetTempFileName(),
                pin = pin
            };

            AsyncOperationExecutor<Context> executor;

            if (asTarred)
            {
                bool tarFormat = assetURL.EndsWith(".tar") || assetURL.EndsWith(".tar\"");

                List<IAsyncOperation<Context>> asyncOperations = new() { new DownloadFromWeb() };

                if (!tarFormat) asyncOperations.Add(new UnZipFileOp());
                else asyncOperations.Add(new UnTarFileOp());

                asyncOperations.Add(new UploadDirectoryToIPFS());

                executor = new(asyncOperations.ToArray())
                {
                    Timeout = timeout
                };
            }
            else
            {
                executor = new(new IAsyncOperation<Context>[]
                {
                    new DownloadFromWeb(),
                    new UploadFileToIPFS()
                })
                {
                    Timeout = timeout
                };
            }

            return (executor, context);
        }

        public static Cid GetUploadedCid(Context _context)
            => (_context as AssetUploaderContext).Cid;

        public static string GetUploadedFilename(Context _context)
            => Path.GetFileName((_context as AssetUploaderContext).AssetURL);
    }
}
