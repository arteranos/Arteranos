/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using System;
using Newtonsoft.Json;
using System.IO;
using Arteranos.Common;

namespace Arteranos.Core
{
    public class WorldMetaData
    {
        public const string PATH_METADATA_DEFAULTS = "MetadataDefaults.json";

        public string WorldName = "Unnamed World";
        public string WorldDescription = string.Empty;
        public UserID AuthorID = null;
        public ServerPermissions ContentRating = null;
        public DateTime Created = DateTime.MinValue;

        public void SaveDefaults()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText($"{Application.persistentDataPath}/{PATH_METADATA_DEFAULTS}", json);
            }
            catch(Exception ex)
            {
                Debug.LogWarning($"Failed to save the metadata defaults: {ex.Message}");
            }
        }

        public static WorldMetaData LoadDefaults()
        {
            WorldMetaData mdj;
            try
            {
                string json = File.ReadAllText($"{Application.persistentDataPath}/{PATH_METADATA_DEFAULTS}");
                mdj = JsonConvert.DeserializeObject<WorldMetaData>(json);
            }
            catch(Exception ex)
            {
                Debug.LogWarning($"Failed to load the metadata defaults: {ex.Message}");
                mdj = new();
            }

            return mdj;
        }

        public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static WorldMetaData Deserialize(string json) => JsonConvert.DeserializeObject<WorldMetaData>(json);
    }
}
