/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Ipfs;
using System.Collections.Generic;
using System;
using Arteranos.WorldEdit;
using Arteranos.Common;

namespace Arteranos.Core.Operations
{

    public class AssetDownloaderContext : Context, IAssetDownloaderContext
    {
        public string path { get; set; } = null;
        public bool isTarred { get; set; } = false;
        public string TargetFile { get; set; } = null;
        public long Size { get; set; } = -1;
    }

    internal class ServerSearcherContext : Context
    {
        public List<ServerInfo> serverInfos = null;
        public PublicWorldData wantedWorld = default;
        public MultiHash resultPeerID = null;
    }

    internal class AssetUploaderContext : Context
    {
        public bool asTarred = false;           // Asset (ZIP) archive will be unpacked and published as a directory
        public string AssetURL = null;          // Local file, file: URL, http(s): URL, resource: URL
        public string TempFile = null;
        public bool pin = false;
        public Cid Cid = null;                  // Contents as the file as-is, or the directory repacked as tar, or the root for <cid>/path/to/file.ext
    }
}