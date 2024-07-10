/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Threading.Tasks;

using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using Arteranos.UI;
using Arteranos.Services;
using Ipfs;
using Arteranos.Web;

namespace Arteranos.Core.Operations
{

    internal class PreloadServerRequirementsOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.1f;
        public string Caption => "Preloading";
        public Action<float> ProgressChanged { get; set; }

        public Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            return Task.Run(() =>
            {
                ServerSearcherContext context = _context as ServerSearcherContext;

                WorldInfo wi = WorldInfo.DBLookup(context.desiredWorldCid);

                context.desiredWorldPermissions = wi.ContentRating;

                return context as Context;
            });
        }
    }

    internal class FetchServersOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 1.0f;
        public string Caption => "Fetching servers";
        public Action<float> ProgressChanged { get; set; }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            ServerSearcherContext context = _context as ServerSearcherContext;

            return await Task.Run(() => Execute(context, token));

            static Context Execute(ServerSearcherContext context, CancellationToken token)
            {
                context.serverInfos = new();
                context.resultPeerID = null;

                foreach (ServerInfo entry in ServerInfo.Dump(DateTime.MinValue))
                    context.serverInfos.Add(entry);

                return context;
            }
        }
    }

    internal class SortServersOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 1.0f;
        public string Caption => "Sorting";
        public Action<float> ProgressChanged { get; set; }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            ServerSearcherContext context = _context as ServerSearcherContext;

            return await Task.Run(() => Execute(context, token));

            int ScoreServer(ServerInfo x)
            {
                int xScore = x.MatchScore;
                if(x.UsesCustomTOS && !SettingsManager.Client.AllowCustomTOS)
                    xScore = -20000;
                else if (!x.IsOnline)
                    xScore = -20000;
                else if (context.desiredWorldPermissions != null && context.desiredWorldPermissions.IsInViolation(x.Permissions))
                    xScore = -10000;
                else if (context.desiredWorldCid != null && x.CurrentWorldCid != context.desiredWorldCid)
                    xScore = -10000;

                return xScore;
            }

            int CompareServers(ServerInfo x, ServerInfo y)
                => ScoreServer(y) - ScoreServer(x);

            Context Execute(ServerSearcherContext context, CancellationToken token)
            {
                context.serverInfos.Sort(CompareServers);

                ServerInfo leader = context.serverInfos.Count > 0 ? context.serverInfos[0] : null;

                int score = ScoreServer(leader);

                if (leader == null)
                    Debug.Log("Server search result: None at all.");
                else
                    Debug.Log($"Server search winner: {leader.Name}, Score: {score}");
                // Even the leader is disqualified, there's no winner.
                if (leader != null && score < 0) leader = null;

                // ... And the winner is... *drumroll*
                if (leader != null) context.resultPeerID = leader.PeerID;

                return context;
            }

        }

    }

    public static class ServerSearcher
    {
        public static (AsyncOperationExecutor<Context>, Context) PrepareSearchServers(string desiredWorld)
        {
            ServerSearcherContext context = new()
            {
                desiredWorldCid = desiredWorld
            };

            AsyncOperationExecutor<Context> executor = new(new IAsyncOperation<Context>[]
            {
                new PreloadServerRequirementsOp(),
                new FetchServersOp(),
                new SortServersOp()
            });

            return (executor, context);
        }

        public static void InitiateServerTransition(Cid WorldCid)
        {
            static void GotResult(Cid WorldCid, MultiHash ServerPeerID) 
                => _ = OnGotSearchResult(WorldCid, ServerPeerID);

            InitiateServerTransition(WorldCid, GotResult, null);
        }

        public static void InitiateServerTransition(Cid WorldCid, Action<Cid, MultiHash> OnSuccessCallback, Action OnFailureCallback)
        {
            IProgressUI pui = ProgressUIFactory.New();

            pui.SetupAsyncOperations(() => PrepareSearchServers(WorldCid));

            pui.Completed += context => OnSuccessCallback(WorldCid, (context as ServerSearcherContext).resultPeerID);
            pui.Faulted += (ex, context) => OnFailureCallback();
        }

        private static async Task OnGotSearchResult(Cid WorldCid, MultiHash ServerPeerID)
        {
            // No matching server, initiate Start Host with loading the world on entering
            if (!string.IsNullOrEmpty(WorldCid) && ServerPeerID == null)
            {
                // It's time to part ways...
                if(G.NetworkStatus.GetOnlineLevel() == OnlineLevel.Client)
                    await G.NetworkStatus.StopHost(true);

                SettingsManager.EnterWorld(WorldCid);

                // If we haven't a server (or, just left one), start up.
                if(G.NetworkStatus.GetOnlineLevel() == OnlineLevel.Offline)
                    await G.NetworkStatus.StartHost();
            }

            // No matching server, leave it be
            if (ServerPeerID == null) return;

            if(IPFSService.Self.Id == ServerPeerID)
            {
                Debug.Log("...It's us! :O");
                return;
            }

            // Matching server (with matching world, if needed), initiate remote connection
            // TODO Use callback to be notified about the connection success/failure,
            // then maybe walk down on the list if the leading servers went dark.
            TaskScheduler.ScheduleCoroutine(
                () => ConnectionManager.ConnectToServer(ServerPeerID, null));
        }
    }
}
