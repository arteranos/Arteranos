/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using Arteranos.Avatar;
using Arteranos.Common;
using Arteranos.Core;
using Arteranos.Core.Managed;
using Arteranos.Services;
using Arteranos.UI;
using Arteranos.WorldEdit;
using Arteranos.XR;
using Ipfs;
using System.Collections.Generic;

namespace Arteranos
{
    public static class G
    {
        public class World_
        {
            public World World { get; set; }
            public Cid Cid => World?.RootCid;
            public string Name => World?.WorldInfo.Result.WorldName ?? "Somewhere";
            public bool ChangeInProgress { get; set; } = false;
        }

        public class DefaultAvatar_
        {
            public Cid Male { get; set; } = null;
            public Cid Female { get; set; } = null;

        }

        public class CommandLineOptions_
        {
            public bool ClearServerUserBase { get; set; } = false;
            public List<string> AddServerAdmins { get; set; } = new();
            public string NewServerName { get; set; } = null;
            public MultiHash DesiredPeerID { get; set; } = null;
            public Cid DesiredWorldCid { get; set; } = null;
        }

        public static IWorldEditorData WorldEditorData { get; set; }
        public static ISceneLoader SceneLoader { get; set; }
        public static IXRControl XRControl { get; set; }
        public static IXRVisualConfigurator XRVisualConfigurator { get; set; }
        public static IAvatarBrain Me {  get; set; }
        public static INetworkStatus NetworkStatus { get; set; }
        public static IAudioManager AudioManager { get; set; }
        public static IConnectionManager ConnectionManager { get; set; }
        public static ISysMenu SysMenu { get; set; }
        public static IIPFSService IPFSService { get; set; }
        public static ITransitionProgress TransitionProgress { get; set; }
        public static IAvatarDownloader AvatarDownloader { get; set; }
        public static IArteranosNetworkManager ArteranosNetworkManager { get; set; }
        // ---------------------------------------------------------------
        public static World_ World { get; } = new();
        // ---------------------------------------------------------------
        public static Client Client { get; set; }
        public static Server Server { get; set; }
        public static ServerUserBase ServerUsers { get; set; }
        public static Community Community { get; set; } = new();
        public static UserData UserData { get; set; }
        public static PermissionsJSON UserContPerms { get; set; }
        // ---------------------------------------------------------------
        public static DefaultAvatar_ DefaultAvatar { get; } = new();
        // ---------------------------------------------------------------
        public static CommandLineOptions_ CommandLineOptions { get; set; } = new();
        public static bool ToQuit { get; set; } = false;
    }
}