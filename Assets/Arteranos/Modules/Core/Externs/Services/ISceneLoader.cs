/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using UnityEngine;

namespace Arteranos.Services
{
    public interface ISceneLoader
    {
        IEnumerator LoadScene(string name);
        IEnumerator LoadScene(AssetBundle loadedAB, bool isFallback, bool doUnload);
    }
}