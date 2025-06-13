/*
 * Copyright (c) 2025, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

#if UNITY_EDITOR
using UnityEditor;

// Just for updating the version information on startup -
// This task is too easily forgotten.
namespace Arteranos.Editor
{
    static class Welcome
    {
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            // Only once, SetVersion() triggers a domain reload!
            if (SessionState.GetBool("ARTERANOS_WELCOME", false)) return;
            SessionState.SetBool("ARTERANOS_WELCOME", true);

            BuildPlayers.GetProjectGitVersion();
        }
    }
}
#endif
