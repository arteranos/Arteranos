/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using Arteranos.Core;

namespace Arteranos.UI
{
    public class CameraDroneUI : MonoBehaviour
    {
        [SerializeField] private Camera DroneCamera;

        public void TakePhoto()
        {

            IEnumerator Cor()
            {
                string name = $"Arteranos-Photo-{DateTime.Now:yyyyMMddHHmmss}.png";
                // FIXME Windows only?
                string picpath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string path = Path.Combine(picpath, name);

                using Stream fs = File.Create(path);

                yield return Common.Utils.TakePhoto(DroneCamera, fs);

                Debug.Log($"Written screenshot to {path}");
            }

            StartCoroutine(Cor());
        }
    }
}
