/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Ipfs;
using Ipfs.Unity;
using System.IO;
using Arteranos.Core;
using System.Threading;
using System;
using System.Threading.Tasks;
using Arteranos.Core.Cryptography;
using System.Linq;
using Ipfs.Cryptography.Proto;
using Ipfs.Http;
using System.Text;
using IPAddress = System.Net.IPAddress;
using Arteranos.Core.Operations;
using Ipfs.CoreApi;
using Debug = UnityEngine.Debug;
using System.Collections.Concurrent;


namespace Arteranos.Services
{
    public class IPFSService : MonoBehaviour, IIPFSService
    {
        public bool Ready { get; private set; } = false;
        public IpfsClientEx Ipfs { get => ipfs; }
        public Peer Self { get => self; }
        public SignKey ServerKeyPair { get => serverKeyPair; }
        public Cid IdentifyCid { get; protected set; }
        public Cid CurrentSDCid { get; protected set; } = null;

        public bool EnableUploadDefaultAvatars = true;

        public static string CachedPTOSNotice { get; private set; } = null;

        private const string ANNOUNCERTOPIC = "/X-Arteranos";

        private const string PATH_USER_PRIVACY_NOTICE = "Privacy_TOS_Notice.md";

        private bool ForceIPFSShutdown = true;

        private IpfsClientEx ipfs = null;
        private Peer self = null;
        private SignKey serverKeyPair = null;

        private string versionString = null;

        private CancellationTokenSource cts = null;

        // ---------------------------------------------------------------
        #region Start & Stop

        public void Awake()
        {
            G.IPFSService = this;
        }

        public void Start()
        {
            IEnumerator InitializeIPFSCoroutine()
            {
                IEnumerator EnsureIPFSStarted()
                {
                    IPFSDaemonConnection.Status res = IPFSDaemonConnection.Status.CommandFailed;

                    yield return Asyncs.Async2Coroutine(() => IPFSDaemonConnection.CheckAPIConnection(5), _res => res = _res);

                    if (res != IPFSDaemonConnection.Status.OK)
                    {
                        G.TransitionProgress?.OnProgressChanged(0.00f, "NO IPFS INSTANCE");
                        Debug.LogError(@"
**************************************************************************
* !!! Failed to check IPFS instance                                  !!! *
* Possible causes are....                                                *
*  * Corrupted user data                                                 *
*  * Corrupted program installation                                      *
**************************************************************************
");
                        yield return new WaitForSeconds(10);
                        SettingsManager.Quit();
                        yield break;
                    }
                }

                try
                {
                    versionString = Core.Version.Load().MMP;
                }
                catch (Exception ex)
                {
                    Debug.LogError("Internal error: Missing version information - use Arteranos->Build->Update version");
                    Debug.LogException(ex);
                }

                // Find and start the IPFS Server. If it's not already running.
                yield return EnsureIPFSStarted();

                // Keep the IPFS synced - it needs the IPFS node alive.
                yield return Asyncs.Async2Coroutine(InitializeIPFS);

                yield return new WaitForSeconds(1);

                // Default avatars
                yield return UploadDefaultAvatars();

                StartIPFSServices();

                // Ready to proceed, past the point we can manually shut down the IPFS backend
                ForceIPFSShutdown = false;
            }

            async Task InitializeIPFS()
            {
                self = IPFSDaemonConnection.Self;
                ipfs = IPFSDaemonConnection.Ipfs;

                // If it doesn't exist, write down the template in the config directory.
                if (!ConfigUtils.ReadConfig(PATH_USER_PRIVACY_NOTICE, File.Exists))
                {
                    ConfigUtils.WriteTextConfig(PATH_USER_PRIVACY_NOTICE, SettingsManager.DefaultTOStext);
                    Debug.LogWarning("Privacy notice and Terms Of Service template written down - Read (and modify) according to your use case!");
                }

                CachedPTOSNotice = ConfigUtils.ReadTextConfig(PATH_USER_PRIVACY_NOTICE);

                G.TransitionProgress?.OnProgressChanged(0.30f, "Announcing its service");

                StringBuilder sb = new();
                sb.Append("Arteranos Server, built by willneedit\n");
                sb.Append(Core.Version.VERSION_MIN);

                // Put up the identifier file
                if (G.Server.Public)
                {
                    IdentifyCid = (await ipfs.FileSystem.AddTextAsync(sb.ToString())).Id;
                }
                else
                {
                    // rm -f the identifier file to not to show the local node in other's FindProviders()
                    AddFileOptions ao = new() { OnlyHash = true };
                    IdentifyCid = (await ipfs.FileSystem.AddTextAsync(sb.ToString(), ao)).Id;

                    try
                    {
                        await ipfs.Pin.RemoveAsync(IdentifyCid).ConfigureAwait(false);
                        await ipfs.Block.RemoveAsync(IdentifyCid, true).ConfigureAwait(false);
                    }
                    catch { }
                }
                Debug.Log("---- IPFS Backend init complete ----\n" +
                    $"IPFS Node's ID\n" +
                    $"   {self.Id}\n"
                    + $"Discovery identifier file's CID\n" +
                    $"   {IdentifyCid}\n"
                    );

                // Reuse the IPFS peer key for the multiplayer server to ensure its association
                serverKeyPair = IPFSDaemonConnection.ServerKeyPair;
                G.Server.UpdateServerKey(serverKeyPair);

                G.TransitionProgress?.OnProgressChanged(0.40f, "Connected to IPFS");
            }

            ipfs = null;
            IdentifyCid = null;

            cts = new();

            StartCoroutine(InitializeIPFSCoroutine());

        }

        public void OnDisable()
        {
            // await FlipServerDescription();

            cts?.Cancel();

            // If we're started the backend on our own, shut it down, too.
            if (ForceIPFSShutdown)
            {
                Debug.Log("Shutting down the IPFS node, because the service didn't completely start.");
                if (ipfs != null) IPFSDaemonConnection.StopDaemon();
            }
            else if (G.Server.ShutdownIPFS)
            {
                Debug.Log("Shutting down the IPFS node.");
                if (ipfs != null) IPFSDaemonConnection.StopDaemon();
            }
            else
                Debug.Log("NOT Shutting down the IPFS node, because you want it to remain running.");

            cts?.Dispose();
        }

        private IEnumerator UploadDefaultAvatars()
        {
            G.TransitionProgress?.OnProgressChanged(0.50f, "Uploading default resources");

            Cid cid = null;
            IEnumerator UploadAvatar(string resourceMA)
            {
                (AsyncOperationExecutor<Context> ao, Context co) =
                    AssetUploader.PrepareUploadToIPFS(resourceMA, false); // Plsin GLB files

                yield return ao.ExecuteCoroutine(co);

                cid = AssetUploader.GetUploadedCid(co);

            }

            if(EnableUploadDefaultAvatars)
            {
                yield return UploadAvatar("resource:///Avatar/6394c1e69ef842b3a5112221.glb");
                G.DefaultAvatar.Male = cid;
                yield return UploadAvatar("resource:///Avatar/63c26702e5b9a435587fba51.glb");
                G.DefaultAvatar.Female = cid;
            }
        }

        #endregion
        // ---------------------------------------------------------------
        #region IPFS services
        private void StartIPFSServices()
        {
            // Server description emitter
            StartCoroutine(EmitServerDescriptionCoroutine());

            // Server online data emitter
            StartCoroutine(EmitServerOnlineDataCoroutine());

            // Subscriber loop
            StartCoroutine(SubscriberCoroutine());

            // IPNS publisher loop
            StartCoroutine(IPNSPublisherCoroutine());

            // Peer discovery loop
            StartCoroutine(PeerDiscoveryCoroutine());

            // Server description downloader scheduler
            StartCoroutine(SDDownloaderCoroutine());
        }

        private readonly ConcurrentQueue<string> ServerDescriptionQueue = new();
        private bool ServerOnlineDataLatch = false;
        private void ScheduleServerDescriptionDownload(string path)
        {
            if (!ServerDescriptionQueue.Contains(path))
                ServerDescriptionQueue.Enqueue(path);
        }
        public void BumpServerOnlineData() 
        {
            ServerOnlineDataLatch = true;
        }

        private IEnumerator IPNSPublisherCoroutine()
        {
            Cid PreviousSDCid = null;
            DateTime publishTime = DateTime.MinValue;

            while (true)
            {
                if (CurrentSDCid == null)
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                // Valid server description, and
                //  - differs from the last one or
                //  - older than one day
                if (PreviousSDCid != CurrentSDCid || publishTime < (DateTime.UtcNow - TimeSpan.FromHours(23)))
                {
                    using CancellationTokenSource cts_pub = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

                    Debug.Log("Publishing new Server Description to IPNS...");

                    Task t = ipfs.Name.PublishAsync(CurrentSDCid, cancel: cts_pub.Token);

                    yield return new WaitUntil(() => t.IsCompleted);

                    Debug.Log("... IPNS publish finished.");

                    PreviousSDCid = CurrentSDCid;
                    publishTime = DateTime.UtcNow;
                }

                yield return new WaitForSeconds(60);
            }
        }

        private IEnumerator SubscriberCoroutine()
        {
            Debug.Log("Subscribing Arteranos PubSub channel");

            while (true)
            {
                using CancellationTokenSource cts_sub = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

                // Truly async.
                Task t = ipfs.PubSub.SubscribeAsync(ANNOUNCERTOPIC, ParseArteranosMessage, cts_sub.Token);

                yield return new WaitForSeconds(5);

                if (t.IsCompleted)
                {
                    Ready = true;
                    yield return new WaitUntil(() => false); // NOTREACHED, until Service is destroyed
                }

                // Not completed within the five seconds?
                Debug.LogWarning("Subscription task seems to be stuck, retrying...");
                cts_sub.Cancel();
                yield return new WaitForSeconds(1);
            }
        }

        private IEnumerator PeerDiscoveryCoroutine()
        {
            HashSet<Peer> peers = new();

            void ProvidersFound(Peer peer)
            {
                // Debug.Log($"Peer found: {peer.Id}");

                // Already discovered, or myself.
                if (peers.Contains(peer) || peer.Id == self.Id) return;

                ScheduleServerDescriptionDownload($"/ipns/{peer.Id}");
                peers.Add(peer);
            }

            async Task<int> FindProviders(Cid idc, CancellationToken cancel = default)
            {
                // Roll up the results
                IEnumerable<Peer> peers = await ipfs.Routing.FindProvidersAsync(idc, 1000, ProvidersFound, cancel).ConfigureAwait(false);

                // Debug.Log($"... discovered peers: {peers.Count()}");
                return peers.Count();
            }

            while(true)
            {
                if (IdentifyCid == null)
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                using CancellationTokenSource cts_pd = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

                Task<int> t = FindProviders(IdentifyCid, cts_pd.Token);

                yield return new WaitUntil(() => t.IsCompleted);

                int timeout = 300;
                int result = t.Result;
                if (result < 2)
                {
                    Debug.Log($"No other peers detected, discovery timeout shortened");
                    timeout = 5;
                }

                yield return new WaitForSeconds(timeout); // 5 minutes before searching again
            }
        }

        private IEnumerator SDDownloaderCoroutine()
        {
            ConcurrentBag<string> knownServerDescriptions = new();
            int numDownloading = 0;

            async Task DownloadServerDescription(string origpath, string guessedPeerID)
            {
                Interlocked.Increment(ref numDownloading);

                string path = origpath;
                try
                {
                    using CancellationTokenSource timeoutToken = new(5000);
                    using CancellationTokenSource cts_dsd = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutToken.Token);

                    if(path.StartsWith("/ipns/"))
                    {
                        guessedPeerID = path[6..];
                        path = await ResolveToCid(path, cancel: cts_dsd.Token);
                    }

                    if (knownServerDescriptions.Contains(path)) return;

                    using MemoryStream ms = new();
                    using Stream s = await ipfs.FileSystem.ReadFileAsync(path + "/ServerDescription", cts_dsd.Token).ConfigureAwait(false);
                    await s.CopyToAsync(ms).ConfigureAwait(false);
                    ms.Position = 0;

                    // Add resolved path. This wouldn't go anywhere, even if it's failing.
                    knownServerDescriptions.Add(path);

                    PublicKey pk = guessedPeerID != null ? PublicKey.FromId(guessedPeerID) : null;
                    ServerDescription sd = ServerDescription.Deserialize(pk, ms);

                    guessedPeerID = sd.PeerID;
                    if (pk == null)
                    {
                        ms.Position = 0;
                        PublicKey.FromId(guessedPeerID);
                        sd = ServerDescription.Deserialize(pk, ms);
                    }

                    sd.LastSeen = DateTime.UtcNow;

                    if (sd)
                    {
                        sd.DBUpdate();
                        Debug.Log($"Successfully downloaded server description ({path}) from {guessedPeerID}");
                    }
                    else
                        // Invalid, most probably outdated server. But, it just gave a sign of life.
                        Debug.Log($"Rejecting server description ({path}) from {guessedPeerID}");
                }
                catch // (Exception ex)
                {
                    //Debug.LogWarning($"Failed to download server description {origpath}");
                    //Debug.LogException(ex);

                    // Try again, put it in the back end of the queue again.
                    ServerDescriptionQueue.Enqueue(origpath);
                }
                finally
                {
                    Interlocked.Decrement(ref numDownloading);
                }
            }

            while(true)
            {
                // Initial flood mitigation
                if(numDownloading >= 5) continue;
                
                if(ServerDescriptionQueue.TryDequeue(out string toDownload))
                {
                    _ = Task.Run(() => DownloadServerDescription(toDownload, null));
                    yield return null;
                }
                else 
                    yield return new WaitForSeconds(1);
            }
        }

        private IEnumerator EmitServerOnlineDataCoroutine()
        {
            DateTime earliestEmitting;
            DateTime latestEmitting;
            bool saidOnline = false;

            while (true)
            {
                OnlineLevel ol = G.NetworkStatus.GetOnlineLevel();

                if (!G.Server.Public)
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                if (ol != OnlineLevel.Server && ol != OnlineLevel.Host && !saidOnline)
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                List<IPAddress> addrs = G.NetworkStatus?.IPAddresses;
                if(addrs == null || addrs.Count == 0)
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                List<byte[]> UserFingerprints = (from user in G.NetworkStatus.GetOnlineUsers()
                                                 where user.UserPrivacy != null && user.UserPrivacy.Visibility != Visibility.Invisible
                                                 select user.UserID.Fingerprint).ToList();

                List<string> addrstrs = (from entry in addrs
                                        where entry != null
                                        select entry.ToString()).ToList();

                ServerOnlineData sod = new()
                {
                    CurrentWorldCid = ol != OnlineLevel.Offline ? G.World.Cid : null,
                    CurrentWorldName = ol != OnlineLevel.Offline ? G.World.Name : "Offline",
                    UserFingerprints = ol != OnlineLevel.Offline ? UserFingerprints : null,
                    ServerDescriptionCid = CurrentSDCid,
                    // LastOnline = last, // Not serialized - set on receive
                    OnlineLevel = ol,
                    IPAddresses = addrstrs,
                    Timestamp = DateTime.UtcNow,
                    Firewalled = G.NetworkStatus.GetConnectivityLevel() != ConnectivityLevel.Unrestricted
                };

                using MemoryStream ms = new();
                sod.Serialize(ms);

                // Announce the server online data, too - fire and forget.
                Task t = ipfs.PubSub.PublishAsync(ANNOUNCERTOPIC, ms.ToArray(), cts.Token);

                // If the just emitted online data means being online, ensure the next
                // emittance - even if it's being offline.
                saidOnline = ol == OnlineLevel.Server || ol == OnlineLevel.Host;

                earliestEmitting = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                latestEmitting = DateTime.UtcNow + TimeSpan.FromSeconds(G.Server.AnnounceRefreshTime);

                yield return new WaitUntil(() =>
                    t.IsCompleted &&
                        ((ServerOnlineDataLatch && DateTime.UtcNow > earliestEmitting) 
                        || DateTime.UtcNow > latestEmitting)
                );

                ServerOnlineDataLatch = false;
            }
        }

        #endregion
        // ---------------------------------------------------------------
        #region Server information publishing

        public IEnumerator EmitServerDescriptionCoroutine()
        {
            while (true)
            {
                yield return Asyncs.Async2Coroutine(FlipServerDescription);

                // One day
                yield return new WaitForSeconds(24 * 60 * 60);
            }
            // NOTREACHED
        }

        public async Task FlipServerDescription()
        {
            async Task<IFileSystemNode> CreateSDFile()
            {
                Server server = G.Server;
                IEnumerable<string> q = from entry in G.ServerUsers.Base
                                        where UserState.IsSAdmin(entry.userState)
                                        select ((string)entry.userID);

                ServerDescription ServerDescription = new()
                {
                    Name = server.Name,
                    ServerPort = server.ServerPort,
                    Description = server.Description,
                    ServerIcon = server.ServerIcon,
                    Version = versionString,
                    MinVersion = Core.Version.VERSION_MIN,
                    Permissions = server.Permissions,
                    PrivacyTOSNotice = CachedPTOSNotice,
                    AdminNames = q.ToArray(),
                    PeerID = self.Id.ToString(),
                    LastModified = server.ConfigLastChanged
                };

                using MemoryStream ms = new();
                ServerDescription.Serialize(serverKeyPair, ms);
                ms.Position = 0;
                return await ipfs.FileSystem.AddAsync(ms, "ServerDescription").ConfigureAwait(false);
            }

            async Task<IFileSystemNode> CreateTOSFile()
            {
                using MemoryStream ms = new(Encoding.UTF8.GetBytes(CachedPTOSNotice));
                ms.Position = 0;
                return await ipfs.FileSystem.AddAsync(ms, "Privacy_TOS_Notice.md").ConfigureAwait(!false);
            }

            async Task<IFileSystemNode> CreateLauncherHTMLFile()
            {
                string linkto = $"arteranos://{self.Id}/";
                string html
    = "<html>\n"
    + "<head>\n"
    + "<title>Launch Arteranos connection</title>\n"
    + $"<meta http-equiv=\"refresh\" content=\"0; url={linkto}\" />\n"
    + "</head>\n"
    + "<body>\n"
    + $"Trouble with redirection? <a href=\"{linkto}\">Click here.</a>\n"
    + "</body>\n"
    + "</html>\n";

                using MemoryStream ms = new(Encoding.UTF8.GetBytes(html));
                ms.Position = 0;
                return await ipfs.FileSystem.AddAsync(ms, "launcher.html").ConfigureAwait(!false);
            }

            async Task<IFileSystemNode> CreateServerDescription()
            {
                List<IFileSystemLink> list = new()
                {
                    (await CreateSDFile().ConfigureAwait(false)).ToLink(),
                    (await CreateTOSFile().ConfigureAwait(false)).ToLink(),
                    (await CreateLauncherHTMLFile().ConfigureAwait(false)).ToLink()
                };

                return await CreateDirectory(list).ConfigureAwait(false);
            }

            IFileSystemNode fsn = await CreateServerDescription().ConfigureAwait(false);
            CurrentSDCid = fsn.Id;

        }

#endregion
        // ---------------------------------------------------------------
        #region Peer communication and data exchange

        public void PostMessageTo(MultiHash peerID, byte[] message)
        {
            _ = ipfs.PubSub.PublishAsync(ANNOUNCERTOPIC, message);
        }

        private void ParseArteranosMessage(IPublishedMessage message)
        {
            MultiHash SenderPeerID = message.Sender.Id;

            // Pubsub MAY loop back the messages, but no need.
            if (SenderPeerID == self.Id) return;

            try
            {
                using MemoryStream ms = new(message.DataBytes);
                PeerMessage pm = PeerMessage.Deserialize(ms);

                if(pm is IDirectedPeerMessage dm)
                {
                    if (dm.ToPeerID != self.Id.ToString())
                    {
                        // Debug.Log("Discarding a message directed to another peer");
                        return;
                    }
                    // else Debug.Log($"Directed message accepoted: {dm.ToPeerID}");
                }

                if (pm is ServerOnlineData sod) // As-is.
                {
                    // Debug.Log($"New server online data from {SenderPeerID}");
                    ScheduleServerDescriptionDownload(sod.ServerDescriptionCid);
                    sod.LastOnline = DateTime.UtcNow; // Not serialized
                    sod.DBInsert(SenderPeerID.ToString());

                    if(sod.CurrentWorldCid == null && sod.UserFingerprints == null)
                    {
                        G.Community.DownServer(SenderPeerID);
                    }
                    else
                    {
                        HashSet<string> usersFP = new();
                        foreach (byte[] entry in sod.UserFingerprints)
                            usersFP.Add(HexString.Encode(entry));
                        G.Community.UpdateServerWorld(SenderPeerID, sod.CurrentWorldCid);
                        G.Community.UpdateServerUsers(SenderPeerID, usersFP, sod.Timestamp);
                    }
                }
                else if (pm is NatPunchRequestData nprd)
                {
                    InitiateNatPunch(nprd);
                }
                else
                    Debug.LogWarning($"Discarding unknown message from {SenderPeerID}");
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }

        private struct SDEntry
        {
            public string path;         // CID of server description
            public DateTime lastSeen;   // Last seen on the network
        }

        private void InitiateNatPunch(NatPunchRequestData nprd)
        {
            Debug.Log($"Contacting peer wants us to initiate Nat punch, relay={nprd.relayIP}:{nprd.relayPort}, token={nprd.token}");
            G.ConnectionManager.Peer_InitateNatPunch(nprd);
        }

        #endregion
        // ---------------------------------------------------------------
        #region IPFS Lowlevel interface
        public Task PinCid(Cid cid, bool pinned, CancellationToken token = default)
        {
            if (pinned)
                return ipfs.Pin.AddAsync(cid, cancel: token);
            else
                return ipfs.Pin.RemoveAsync(cid, cancel: token);
        }

        public async Task<MemoryStream> ReadIntoMS(string path, Action<long> reportProgress = null, CancellationToken cancel = default)
        {
            MemoryStream ms = new();

            using Stream instr = await ipfs.FileSystem.ReadFileAsync(path, cancel).ConfigureAwait(false);
            byte[] buffer = new byte[128 * 1024];

            // No cancel. We have to do it until the end, else the RPC client would choke.
            long totalBytes = 0;
            long lastreported = 0;
            while (true)
            {
                int n = instr.Read(buffer, 0, buffer.Length);
                if (n <= 0) break;
                totalBytes += n;
                try
                {
                    // Report rate throttling
                    if(lastreported < totalBytes - buffer.Length)
                    {
                        reportProgress?.Invoke(totalBytes);
                        lastreported = totalBytes;
                    }
                }
                catch { } // Whatever may come, the show must go on.
                ms.Write(buffer, 0, n);
            }

            instr.Close();

            // One last report.
            reportProgress?.Invoke(totalBytes);

            ms.Position = 0;
            return ms;
        }

        public async Task<byte[]> ReadBinary(string path, Action<long> reportProgress = null, CancellationToken cancel = default)
        {
            using MemoryStream ms = await ReadIntoMS(path, reportProgress, cancel).ConfigureAwait(false);

            return ms.ToArray();
        }

        public async Task<IEnumerable<Cid>> ListPinned(CancellationToken cancel = default)
            => await Ipfs.Pin.ListAsync(cancel).ConfigureAwait(false);
        public async Task<Stream> ReadFile(string path, CancellationToken cancel = default)
            => await Ipfs.FileSystem.ReadFileAsync(path, cancel).ConfigureAwait(false);

        public async Task<Stream> Get(string path, CancellationToken cancel = default)
            => await Ipfs.FileSystem.GetAsync(path, cancel: cancel).ConfigureAwait(false);
        public async Task<IFileSystemNode> AddStream(Stream stream, string name = "", AddFileOptions options = null, CancellationToken cancel = default)
            => await Ipfs.FileSystem.AddAsync(stream, name, options, cancel).ConfigureAwait(false);
        public async Task<IFileSystemNode> ListFile(string path, CancellationToken cancel = default)
            => await Ipfs.FileSystem.ListAsync(path, cancel).ConfigureAwait(false);
        public async Task<IFileSystemNode> AddDirectory(string path, bool recursive = true, AddFileOptions options = null, CancellationToken cancel = default)
            => await Ipfs.FileSystem.AddDirectoryAsync(path, recursive, options, cancel).ConfigureAwait(false);
        public async Task RemoveGarbage(CancellationToken cancel = default)
            => await Ipfs.BlockRepository.RemoveGarbageAsync(cancel).ConfigureAwait(false);

        public async Task<Cid> ResolveToCid(string path, CancellationToken cancel = default)
        {
            string resolved = await Ipfs.ResolveAsync(path, cancel: cancel).ConfigureAwait(false);
            if (resolved == null || resolved.Length < 6 || resolved[0..6] != "/ipfs/") return null;
            return resolved[6..];
        }

        public async Task<FileSystemNode> CreateDirectory(IEnumerable<IFileSystemLink> links, bool pin = true, CancellationToken cancel = default)
            => await Ipfs.FileSystemEx.CreateDirectoryAsync(links, pin, cancel).ConfigureAwait(false);

        #endregion
    }
}
