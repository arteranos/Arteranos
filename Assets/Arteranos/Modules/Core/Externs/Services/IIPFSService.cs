/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Common.Cryptography;
using Ipfs;
using Ipfs.CoreApi;
using Ipfs.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Arteranos.Services
{
    public interface IIPFSService : IMonoBehaviour
    {
        Cid CurrentSDCid { get; }
        Cid IdentifyCid { get; }
        IpfsClientEx Ipfs { get; }
        Peer Self { get; }
        SignKey ServerKeyPair { get; }
        bool Ready { get; }

        Task<IFileSystemNode> AddDirectory(string path, bool recursive = true, AddFileOptions options = null, CancellationToken cancel = default);
        Task<IFileSystemNode> AddStream(Stream stream, string name = "", AddFileOptions options = null, CancellationToken cancel = default);
        Task FlipServerDescription();
        Task<Stream> Get(string path, CancellationToken cancel = default);
        Task<IFileSystemNode> ListFile(string path, CancellationToken cancel = default);
        Task<IEnumerable<Cid>> ListPinned(CancellationToken cancel = default);
        Task PinCid(Cid cid, bool pinned, CancellationToken token = default);
        Task<byte[]> ReadBinary(string path, Action<long> reportProgress = null, CancellationToken cancel = default);
        Task<Stream> ReadFile(string path, CancellationToken cancel = default);
        Task RemoveGarbage(CancellationToken cancel = default);
        Task<Cid> ResolveToCid(string path, CancellationToken cancel = default);
        Task<FileSystemNode> CreateDirectory(IEnumerable<IFileSystemLink> links, bool pin = true, CancellationToken cancel = default);
        Task<MemoryStream> ReadIntoMS(string path, Action<long> reportProgress = null, CancellationToken cancel = default);
        Task<T> GetConfigItem<T>(string key, CancellationToken cancel = default);
        void PostMessageTo(MultiHash peerID, byte[] message);
        void BumpServerOnlineData();
    }
}