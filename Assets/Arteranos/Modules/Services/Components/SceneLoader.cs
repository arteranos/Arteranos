/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System;

namespace Arteranos.Services
{
    public class SceneLoader : MonoBehaviour, ISceneLoader
    {
        public event Action OnFinishingSceneChange;

        private readonly List<string> TNWhitelist = new()
        {
            "UnityEngine.",
            "TMPro.",
            "Arteranos.User"
        };

        private readonly List<string> AssWhiteList = new()
        {
            "UnityEngine.AnimationModule",
            "UnityEngine.AudioModule",
            "UnityEngine.ClothModule",
            "UnityEngine.CoreModule",
            "UnityEngine.DirectorModule",
            "UnityEngine.GIModule",
            "UnityEngine.ParticleSystemModule",
            "UnityEngine.PhysicsModule",
            "UnityEngine.SpriteMaskModule",
            "UnityEngine.SpriteShapeModule",
            "UnityEngine.TerrainModule",
            "UnityEngine.TerrainPhysicsModule",
            "UnityEngine.UIModule",

            "UnityEngine.UI",
            "Unity.XR.Interaction.Toolkit",
            "Unity.RenderPipelines.Universal.Runtime",
            "Unity.TextMeshPro",

            "Arteranos.User"

        };

        private void Awake()
        {
            G.SceneLoader = this;
        }

        private bool MatchWith(string name, List<string> patterns)
        {
            foreach (string pattern in patterns)
                if (name.StartsWith(pattern)) return true;

            return false;
        }

        public bool CheckComponent(Component component)
        {
            Type type = component.GetType();
            if (type == null) return false;

            if (!MatchWith(type.FullName, TNWhitelist)) return false;

            Assembly asm = type.Assembly;

            if (!MatchWith(asm.GetName().Name, AssWhiteList)) return false;

            return true;
        }

        public void StripScripts(Transform transform)
        {
            Component[] components = transform.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                {
                    // Can't grasp it because it has missing code.
                    // Debug.LogWarning($"Detected defunct component in {transform.name}");
                }
                else if (!CheckComponent(component))
                {
                    Type type = component.GetType();
                    Assembly asm = type.Assembly;

                    Debug.LogWarning($"Removing {component.GetType().FullName} ({asm.GetName().Name}) in {transform.name}");
                    if (component as Behaviour != null)
                        (component as Behaviour).enabled = false;

                    // Really have to use it. Unwanted scripts have to move away as quick as possible.
                    DestroyImmediate(component);
                }
            }

            for (int i = 0, c = transform.childCount; i < c; ++i)
                StripScripts(transform.GetChild(i));
        }

        public void RouteAudio(Transform transform)
        {
            UnityEngine.Audio.AudioMixerGroup mixerGroupEnv = G.AudioManager.MixerGroupEnv;

            // Server may (=should) not have an audio output
            if (!mixerGroupEnv) return;

            // Maybe more groups, like Ambient, BGM, and streaming music/video?
            // Distinguish with Name/Tags?
            foreach (AudioSource source in transform.GetComponents<AudioSource>())
                source.outputAudioMixerGroup = mixerGroupEnv;

            for (int i = 0, c = transform.childCount; i < c; ++i)
                RouteAudio(transform.GetChild(i));
        }

        public void ListShaders(Transform transform, HashSet<Material> materials)
        {
            foreach (Renderer renderer in transform.GetComponents<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                    materials.Add(material);
            }

            for (int i = 0, c = transform.childCount; i < c; ++i)
                ListShaders(transform.GetChild(i), materials);
        }

        private static void FixupShader(Material material)
        {
            Shader shader = material?.shader;
            if (shader)
            {
                Shader replacement = Shader.Find(shader.name);
                if (!replacement)
                    Debug.LogWarning($"{shader.name} is unsupported and no stock shader present");
                else
                    material.shader = replacement;
            }
        }

        public IEnumerator LoadScene(string name)
        {
            yield return null;

            AssetBundle loadedAB = AssetBundle.LoadFromFile(name);

            yield return LoadScene(loadedAB, false, false);
        }

        public IEnumerator LoadScene(AssetBundle loadedAB, bool isFallback, bool doUnload)
        {
            if (loadedAB == null)
            {
                Debug.Log("Failed to load AssetBundle!");
                yield break;
            }

            Debug.Log("Done loading AssetBundle.");

            if (loadedAB.isStreamedSceneAssetBundle)
            {
                Debug.LogError("This is a streamed scene assetbundle, which we don't want to.");
                yield break;
            }

            Scene prev = SceneManager.GetActiveScene();

            Scene newScene = SceneManager.CreateScene($"Scene_{Path.GetRandomFileName()}");

            yield return null;

            SceneManager.SetActiveScene(newScene);

            AssetBundleRequest abrGO = loadedAB.LoadAssetAsync<GameObject>("Assets/Root/Environment.prefab");
            AssetBundleRequest abrLL = loadedAB.LoadAssetAsync<GameObject>("Assets/Root/LevelLightmapData.prefab");

            while (!abrGO.isDone) yield return null;

            GameObject environment = abrGO.asset as GameObject;
            if (environment == null)
            {
                // Must be horribly wrong. Or, someone tampered with the world data.
                Debug.LogError("Cannot load the Environment asset");
                SceneManager.UnloadSceneAsync(newScene);
                yield break;
            }

            environment.SetActive(false);

            Debug.Log("Populating scene...");

            GameObject go = Instantiate(environment);
            StripScripts(go.transform);

            RouteAudio(go.transform);

            Debug.Log("Adding lighting data...");

            while (!abrLL.isDone) yield return null;

            GameObject llGO = Instantiate(abrLL.asset as GameObject);

            LevelLightmapData lld = llGO.GetComponent<LevelLightmapData>();

            Debug.Log("Populating scene done, setting active...");
            go.SetActive(true);

            lld.allowLoadingLightingScenes = false;
            lld.LoadLightingScenarioData(0);

            // Asset Bundle is marked as Fallback, try the fixups...
            // - Shaders
            if (isFallback)
            {
                // Having Skybox set requires the Lighting Scenario Data to be set.
                // And the data to be set requires the scene to be completely reconstructed.
                Debug.Log("Fixup shaders...");

                HashSet<Material> materials = new();
                ListShaders(go.transform, materials);

                // Add the skybox material, too.
                materials.Add(RenderSettings.skybox);

                foreach (Material material in materials) FixupShader(material);

            }

            Debug.Log("Scene is live.");

            Debug.Log("Loader finished, cleaning up.");

            if (doUnload) loadedAB.Unload(false);

            // Give the chance to move the own avatar BEFORE to unload the old scene
            // to prevent to pull the rug away from under your feet.
            OnFinishingSceneChange?.Invoke();

            SceneManager.UnloadSceneAsync(prev);
        }
    }
}
