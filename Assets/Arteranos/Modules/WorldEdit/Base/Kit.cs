/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Core;
using Arteranos.Core.Managed;
using Ipfs;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssetBundle = Arteranos.Core.Managed.AssetBundle;
using System.Linq;
using Newtonsoft.Json;
using Arteranos.Common;


namespace Arteranos.WorldEdit
{
    [ProtoContract]
    public struct KitEntryItem
    {
        [ProtoMember(1)]
        public string Name;

        [ProtoMember(2)]
        public Guid GUID;

        public KitEntryItem(string name, Guid guid)
        {
            Name = name;
            GUID = guid;
        }
    }

    [ProtoContract]
    public struct KitEntryList
    {
        [ProtoMember(1)]
        public List<KitEntryItem> Items;
    }

    public class KitMetaData
    {
        public string KitName = "Unnamed Kit";
        public string KitDescription = string.Empty;
        public UserID AuthorID = null;
        public DateTime Created = DateTime.MinValue;

        public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static KitMetaData Deserialize(string json) => JsonConvert.DeserializeObject<KitMetaData>(json);
    }

    public class Kit : IFavouriteable, IEquatable<Kit>, IDisposable
    {
        public Cid RootCid { get; private set; } = null;

        private Kit() { }

        public Kit(Cid rootCid)
        {
            RootCid = rootCid;

            KitContent = new(async () => await GetAssetBundle());
            KitInfo = new(async () => await GetKitInfo());
            ScreenshotPNG = new(async () => await GetScreenshot());
            ItemMap = new(async () => await GetMap());
        }

        public static implicit operator Kit(Cid rootCid) => new(rootCid);

        public static bool operator ==(Kit left, Kit right)
        {
            return EqualityComparer<Kit>.Default.Equals(left, right);
        }

        public static bool operator !=(Kit left, Kit right)
        {
            return !(left == right);
        }

        /// <summary>
        /// The kit content AssetBundle
        /// </summary>
        public readonly AsyncLazy<AssetBundle> KitContent;

        /// <summary>
        /// The kit's meta data
        /// </summary>
        public readonly AsyncLazy<KitMetaData> KitInfo;

        /// <summary>
        /// The kit's overall screenshot
        /// </summary>
        public readonly AsyncLazy<byte[]> ScreenshotPNG;

        /// <summary>
        /// List of kit items, together with its friendly names.
        /// </summary>
        public readonly AsyncLazy<Dictionary<Guid, string>> ItemMap;

        /// <summary>
        /// List of the item screenshots. Map preloaded, then the respective item.
        /// </summary>
        public Dictionary<Guid, AsyncLazy<byte[]>> ItemScreenshotPNGs => m_ItemScreenshotPNGs;

        private Dictionary<Guid, AsyncLazy<byte[]>> m_ItemScreenshotPNGs;


        private async Task<Dictionary<Guid, string>> GetMap()
        {
            using CancellationTokenSource cts = new(4000);
            using MemoryStream ms = await G.IPFSService.ReadIntoMS($"{RootCid}/map", cancel: cts.Token);
            KitEntryList list = Serializer.Deserialize<KitEntryList>(ms);

            Dictionary<Guid, string> dict = new();
            m_ItemScreenshotPNGs = new();

            foreach(KitEntryItem item in list.Items)
            {
                dict.Add(item.GUID, item.Name);
                m_ItemScreenshotPNGs.Add(item.GUID, new(async () => await GetItemScreenshot(item.GUID)));
            }

            return dict;
        }

        private async Task<byte[]> GetItemScreenshot(Guid gUID)
        {
            using CancellationTokenSource cts = new(4000);
            return await G.IPFSService.ReadBinary($"{RootCid}/KitScreenshots/{gUID}.png", cancel: cts.Token);
        }

        private async Task<byte[]> GetScreenshot()
        {
            using CancellationTokenSource cts = new(4000);
            return await G.IPFSService.ReadBinary($"{RootCid}/Screenshot.png", cancel: cts.Token);
        }

        private async Task<KitMetaData> GetKitInfo()
        {
            using CancellationTokenSource cts = new(4000);
            byte[] data = await G.IPFSService.ReadBinary($"{RootCid}/Metadata.json", cancel: cts.Token);
            string json = Encoding.UTF8.GetString(data);

            return KitMetaData.Deserialize(json);
        }

        private async Task<AssetBundle> GetAssetBundle()
        {
            // TODO ten minutes timeout? Configurable?
            using CancellationTokenSource cts = new(600000);
            return await Core.Utils.LoadAssetBundle(RootCid, cancel: cts.Token);
        }

        // ---------------------------------------------------------------

        public bool IsFavourited => throw new NotImplementedException();

        public DateTime LastSeen => throw new NotImplementedException();

        public void Favourite()
        {
            G.Client.WEAC.WorldObjectsKits.Add(new()
            {
                IPFSPath = RootCid,
                FriendlyName = ""
            });

            G.Client.Save();
        }

        public void Unfavourite()
        {
            throw new NotImplementedException();
        }

        public void UpdateLastSeen()
        {
            throw new NotImplementedException();
        }

        public static IEnumerable<Cid> ListFavourites()
        {
            return from entry in G.Client.WEAC.WorldObjectsKits
                   select (Cid) entry.IPFSPath;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Kit);
        }

        public bool Equals(Kit other)
        {
            return other is not null &&
                   EqualityComparer<Cid>.Default.Equals(RootCid, other.RootCid);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RootCid);
        }
        // ---------------------------------------------------------------

        bool disposed = false;

        ~Kit()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (KitContent.IsValueCreated) KitContent?.Result?.Dispose();
        }
    }
}
 