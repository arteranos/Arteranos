/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;

using Arteranos.Core;
using System.Collections.Generic;
using System.Threading;
using System.Collections;
using Ipfs;
using Arteranos.Core.Operations;
using System.Collections.Concurrent;
using Arteranos.Core.Managed;
using Arteranos.Common.Cryptography;

namespace Arteranos.UI
{
    internal class Collection
    {
        public Cid worldCid;
        public int serversCount;
        public int usersCount;
        public int friendsMax;
        public bool favourited;
        public bool current;
    }

    public class WorldPanelUI : ActionPage
    {
        public ObjectChooser Chooser;
        public FileBrowser FileBrowser;

        private readonly ConcurrentDictionary<Fingerprint, Collection> worldlist = new();
        private readonly List<Fingerprint> sortedWorldList = new();
        private Mutex DictMutex = null;

        protected override void Awake()
        {
            base.Awake();

            DictMutex = new();

            Chooser.OnShowingPage += PreparePage;
            Chooser.OnPopulateTile += PopulateTile;
            Chooser.OnAddingItem += _ => ActionRegistry.Call(
                "embedded.fileBrowser", 
                new FileBrowserData() { Pattern = @".*\.(tar|zip)" },
                FileBrowser, GotFileBrowserSelection);
        }

        public override void OnEnterLeaveAction(bool onEnter)
        {
            Chooser.gameObject.SetActive(onEnter);
        }

        private void GotFileBrowserSelection(object result)
        {
            if (result is not string obj)
            {
                Chooser.FinishAdding();
                return;
            }

            Debug.Log($"Chosen to add: {obj}");
            RequestToAdd(obj);
        }

        private void PreparePage(int obj)
        {
            Chooser.UpdateItemCount(sortedWorldList.Count);
        }

        private void RequestToAdd(string obj)
        {
            CancellationTokenSource cts = new();


            IProgressUI pui = Factory.NewProgress();

            pui.SetupAsyncOperations(() => AssetUploader.PrepareUploadToIPFS(obj, true, pin: true)); // World Zip file

            pui.Completed += context =>
            {
                Cid cid = AssetUploader.GetUploadedCid(context);
                ParseWorld(cid);
                Chooser.FinishAdding();
            };

            pui.Faulted += (ex, context) =>
            {
                Chooser.FinishAdding();
                ShowPage(0);
            };
        }

        private void PopulateTile(int i, GameObject @object)
        {
            if (!@object.TryGetComponent(out WorldPaneltem wli)) return;

            Fingerprint WorldCid = sortedWorldList[i];
            if (worldlist.TryGetValue(WorldCid, out Collection list))
            {
                wli.ServersCount = list.serversCount;
                wli.UsersCount = list.usersCount;
                wli.FriendsMax = list.friendsMax;
                wli.World = list.worldCid;
            }
        }

        protected override void OnDestroy()
        {
            Chooser.OnPopulateTile -= PopulateTile;
            Chooser.OnAddingItem -= RequestToAdd;
            Chooser.OnShowingPage -= PreparePage;

            DictMutex.Close();
            DictMutex = null;
            base.OnDestroy();
        }

        protected override void Start()
        {
            IEnumerator BuildSortedList()
            {
                worldlist.Clear();

                if(G.World.Cid != null)
                    yield return AddManualWorldCoroutine(G.World.Cid, false, true);

                yield return GatherServeredWorlds();

                yield return GatherFavouritedWorlds();

                CreateSortedWorldList();

                Chooser.ShowPage(0);
            }

            base.Start();

            StartCoroutine(BuildSortedList());
        }

        private IEnumerator GatherServeredWorlds()
        {
            foreach(ServerInfo si in ServerInfo.Dump())
            {
                if(si.CurrentWorldFP == (Fingerprint) null) continue;

                // Looking for the server where you meet up with most of your friends;
                // sorry about for outliers.
                int friendCount = si.FriendCount;
                worldlist.AddOrUpdate(si.CurrentWorldFP, new Collection()
                {
                    favourited = false,
                    current = false,
                    friendsMax = friendCount,
                    serversCount = 1,
                    usersCount = si.UserCount
                },
                (cid, coll) =>
                {
                    coll.friendsMax = friendCount > coll.friendsMax ? friendCount : coll.friendsMax;
                    coll.serversCount++;
                    coll.usersCount += si.UserCount;
                    return coll;
                });

                yield return new WaitForEndOfFrame();
            }
        }

        private IEnumerator GatherFavouritedWorlds()
        {
            IEnumerable<Cid> pinned = World.ListFavourites();

            foreach(Cid cid in pinned)
            {
                yield return AddManualWorldCoroutine(cid, true, null);

                yield return new WaitForEndOfFrame();
            }
        }

        private IEnumerator AddManualWorldCoroutine(Cid WorldCid, bool? favourited, bool? current)
        {
            worldlist.AddOrUpdate(new Fingerprint(WorldCid), new Collection()
            {
                favourited = favourited ?? true,
                current = current ?? false,
                friendsMax = 0,
                serversCount = 0,
                usersCount = 0,
                worldCid = WorldCid
            },
            (cid, coll) =>
            {
                coll.favourited = favourited ?? coll.favourited;
                coll.current = current ?? coll.current;
                coll.worldCid = WorldCid; // We may have got the world by its fingerprint, and we got its CID now.
                return coll;
            });

            yield return new WaitForEndOfFrame();
        }

        private void CreateSortedWorldList()
        {
            sortedWorldList.Clear();
            foreach(KeyValuePair<Fingerprint, Collection> item in worldlist)
            {
                // It's nowhere hosted and unfavourited, so leave out the dross
                if (ScoreWorld(item.Key) <= 0) continue;

                sortedWorldList.Add(item.Key);
            }

            sortedWorldList.Sort((x, y) => ScoreWorld(y) - ScoreWorld(x));
        }

        private int ScoreWorld(Fingerprint cid) 
        {
            if(!worldlist.TryGetValue(cid, out Collection list)) return -10000;

            int score = 0;

            score += list.serversCount; // Servers get one point.

            score += list.usersCount * 5; // Users get five points.

            score += list.friendsMax * 20; // Available friends bunched up in a single server get twenty points.

            score += list.favourited ? 100000 : 0; // A class for its own.

            score += list.current ? 100000 : 0; // Always first.

            return score;
        }

        private void ShowPage(int currentPage) => Chooser.ShowPage(currentPage);


        private void ParseWorld(Cid cid)
        {
            IEnumerator ParseWorldCoroutine()
            {
                yield return null;

                yield return AddManualWorldCoroutine(cid, true, null);

                CreateSortedWorldList();

                Chooser.FinishAdding();
                ShowPage(0);
            }

            TaskScheduler.ScheduleCoroutine(ParseWorldCoroutine);
        }
    }
}
