/*
 * Copyright (c) 2024, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.WorldEdit;
using Ipfs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arteranos.Core.Managed
{

    public class World : IFavouriteable, IDisposable
    {
        public event Action<long, long> OnReportingProgress;

        public Cid RootCid { get; private set; } = null;

        private World() { }

        public World(Cid rootCid)
        {
            RootCid = rootCid;

            TemplateCid = new(async () => (await GetWorldLinks()).template);
            DecorationCid = new(async () => (await GetWorldLinks()).decoration);
            WorldInfo = new(async () => await GetWorldInfo());
            TemplateInfo = new(async () => await GetTemplateInfo());
            ScreenshotPNG = new(async () => await GetActiveScreenshot());

            TemplateContent = new(async () => await GetAssetBundle());
            DecorationContent = new(async () => await GetWorldDecoration());
        }

        public static implicit operator World(Cid rootCid) => rootCid != null ? new(rootCid) : null;

        /// <summary>
        /// The underlying world template
        /// </summary>
        public readonly AsyncLazy<Cid> TemplateCid;

        /// <summary>
        /// The decoration, null if there idn't one (aka blank world)
        /// </summary>
        public readonly AsyncLazy<Cid> DecorationCid;

        /// <summary>
        /// The active World Info, same as TemplateInfo if it's a blank world
        /// </summary>
        public readonly AsyncLazy<WorldInfo> WorldInfo;

        /// <summary>
        /// The template's info.
        /// </summary>
        public readonly AsyncLazy<WorldInfo> TemplateInfo;

        /// <summary>
        /// The screemshot PNG
        /// </summary>
        public readonly AsyncLazy<byte[]> ScreenshotPNG;

        /// <summary>
        /// The template's asset bundle
        /// </summary>
        public readonly AsyncLazy<AssetBundle> TemplateContent;

        /// <summary>
        /// The world decoration's content
        /// </summary>
        public readonly AsyncLazy<IWorldDecoration> DecorationContent;

        public async Task<bool> IsFullWorld() => await DecorationCid != null;


        private string m_TemplateCid = null;
        private string m_DecorationCid = null;

        private async Task<(string template,string decoration)> GetWorldLinks()
        {
            if (m_TemplateCid != null) return (m_TemplateCid, m_DecorationCid);

            Dictionary<string, IFileSystemLink> dir = new();

            IFileSystemNode fsn = await G.IPFSService.ListFile(RootCid);
            if (!fsn.IsDirectory)
                throw new InvalidDataException($"{RootCid} is not a directory");
            foreach (IFileSystemLink file in fsn.Links)
                dir.Add(file.Name, file);

            m_TemplateCid = dir.ContainsKey("Template")
                ? dir["Template"].Id
                : RootCid;

            m_DecorationCid = dir.ContainsKey("Decoration")
                ? dir["Decoration"].Id
                : null;

            return (m_TemplateCid, m_DecorationCid);
        }

        private async Task<byte[]> GetActiveScreenshot()
        {           
            using CancellationTokenSource cts = new(4000);
            return await G.IPFSService.ReadBinary($"{RootCid}/Screenshot.png", cancel: cts.Token);
        }

        private async Task<WorldInfo> GetTemplateInfo()
        {
            string targeted = await TemplateCid;

            using CancellationTokenSource cts = new(4000);
            byte[] data = await G.IPFSService.ReadBinary($"{targeted}/Metadata.json", cancel: cts.Token);
            string json = Encoding.UTF8.GetString( data );

            WorldMetaData metaData = WorldMetaData.Deserialize(json);

            WorldInfo win = new()
            {
                WorldCid = (Cid) TemplateCid,
                WorldName = metaData.WorldName,
                WorldDescription = metaData.WorldDescription,
                Author = metaData.AuthorID,
                ContentRating = metaData.ContentRating,
                Created = metaData.Created,
                AccessInfo = null, // Templates are considered Public Domain, to prevent identical copies, just for changing authors
            };
            return win;
        }

        private async Task<WorldInfo> GetWorldInfo()
        {
            WorldInfo win = await IsFullWorld() 
                ? (await GetWorldDecoration()).Info 
                : await GetTemplateInfo();

            // Add the self reference
            win.WorldCid = RootCid;
            return win;
        }

        private async Task<IWorldDecoration> GetWorldDecoration()
        {
            Cid path = await DecorationCid;

            if (path == null) return null;

            using CancellationTokenSource cts = new(4000);
            using MemoryStream ms = await G.IPFSService.ReadIntoMS(path, cancel: cts.Token);
            return G.WorldEditorData.DeserializeWD(ms);
        }

        private async Task<AssetBundle> GetAssetBundle()
        {
            // TODO ten minutes timeout? Configurable?
            using CancellationTokenSource cts = new(600000);
            return await Utils.LoadAssetBundle(await TemplateCid, (bytes, total) => OnReportingProgress?.Invoke(bytes, total), cts.Token);
        }

        // ---------------------------------------------------------------

        public DateTime LastSeen => DateTime.MinValue;

        public bool IsFavourited => G.Client.FavouritedWorlds.Contains(RootCid);

        public void Favourite()
        {
            Client cs = G.Client;
            if (!cs.FavouritedWorlds.Contains(RootCid))
                cs.FavouritedWorlds.Add(RootCid);

            cs.Save();
        }

        public void Unfavourite()
        {
            Client cs = G.Client;
            cs.FavouritedWorlds.Remove(RootCid);

            cs.Save();
        }

        public void UpdateLastSeen()
        {
            // TODO Implement
        }

        public static IEnumerable<Cid> ListFavourites()
        {
            return G.Client.FavouritedWorlds;
        }

        // ---------------------------------------------------------------

        bool disposed = false;

        ~World()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if(TemplateContent.IsValueCreated) TemplateContent?.Result?.Dispose();
        }
    }
}