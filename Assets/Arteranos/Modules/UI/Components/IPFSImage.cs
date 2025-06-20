/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */


using System.Collections;
using UnityEngine.UI;

using Arteranos.Core;
using UnityEngine;
using Ipfs.Unity;
using System.Threading;
using System.Threading.Tasks;

namespace Arteranos.UI
{
    [AddComponentMenu("UI/IPFS Image", 13)]
    public class IPFSImage : RawImage, IIPFSImage
    {
        public string Path
        {
            get => m_Path;
            set
            {
                if (m_Path == value) return;
                
                m_Path = value;
                RestartLoader();
            }
        }

        public byte[] ImageData
        {
            get => m_ImageData;
            set
            {
                if (m_ImageData == value) return;

                m_Path = null;
                m_ImageData = value;
                RestartLoader();
            }
        }

        private string m_Path = null;
        private byte[] m_ImageData = null;


        protected override void OnEnable()
        {
            base.OnEnable();

#if UNITY_EDITOR
            if (Application.isPlaying)
#endif
                RestartLoader();
        }

        private void RestartLoader()
        {
            IEnumerator LoaderCoroutine()
            {
                yield return null;

                if (m_Path != null)
                {
                    using CancellationTokenSource cts = new(4000);

                    yield return Asyncs.Async2Coroutine(
                        () => G.IPFSService.ReadBinary(m_Path, cancel: cts.Token),
                        _data => m_ImageData = _data);
                }

                bool result = false;
                Texture2D tex = new(2, 2);

                if (m_ImageData != null && m_ImageData.Length > 0)
                {
                    // No Async2Coroutine. AsyncImageLoader's initializer needs the main task.
                    Task<bool> resultTask = AsyncImageLoader.LoadImageAsync(tex, m_ImageData);
                    yield return new WaitUntil(() => resultTask.IsCompleted);
                    result = resultTask.Result;
                }

                if (result)
                    texture = tex;
                else if (texture == null)
                    texture = BP.I != null ? BP.I.Unknown_Icon : null;
            }

            StopAllCoroutines();

            if(isActiveAndEnabled)
                StartCoroutine(LoaderCoroutine());
        }
    }
}