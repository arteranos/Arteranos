/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

using Arteranos.Core.Managed;
using AssetBundle = Arteranos.Core.Managed.AssetBundle;
using Arteranos.Core;
using System;
using Arteranos.UI;

namespace Arteranos.Services
{
    public static class TransitionProgress
    {
        public static event Action OnWorldFinished = null;

        public static IDialogUI changeWorldDialog = null;

        public static void InitPreWorldTransition()
        {
            // Only pure clients may be unsuspecting; offline or hosts are the instigators
            // themselves....
            if(G.NetworkStatus.GetOnlineLevel() == OnlineLevel.Client)
            {
                changeWorldDialog = Factory.NewDialog();
                changeWorldDialog.Text = "Changing world...";
                changeWorldDialog.Buttons = new string[] { "OK" };
            }

            G.World.ChangeInProgress = true;
        }

        public static IEnumerator TransitionFrom()
        {
            G.XRVisualConfigurator.StartFading(1.0f);
            yield return new WaitForSeconds(0.5f);

            AsyncOperation ao = SceneManager.LoadSceneAsync("Transition");
            yield return new WaitUntil(() => ao.isDone);


            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            G.XRControl.MoveRig();

            G.XRVisualConfigurator.StartFading(0.0f);

            yield return new WaitUntil(() => (G.TransitionProgress != null));

            // Explicitly dispose the old world before attempting to load the new one.
            G.World.World?.Dispose();
            G.World.World = null;

            G.SysMenu.EnableHUD(false);
        }

        public static IEnumerator TransitionTo(World World)
        {
            IEnumerator MoveToPreloadedWorld()
            {
                G.World.World = World;

                if (World == null)
                {
                    AsyncOperation ao = SceneManager.LoadSceneAsync("OfflineScene");
                    while (!ao.isDone) yield return null;

                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();

                    G.XRControl.MoveRig();
                }
                else
                {
                    yield return EnterDownloadedWorld(World);
                    yield return World.WorldInfo.WaitFor();
                }

                // Reset UI flow
                ActionRegistry.Drop();
            }

            // However it is, we've arrived.
            if(changeWorldDialog != null)
            {
                changeWorldDialog.Close();
                changeWorldDialog = null;
            }

            G.World.ChangeInProgress = false;

            G.XRVisualConfigurator.StartFading(1.0f);
            yield return new WaitForSeconds(0.5f);

            yield return MoveToPreloadedWorld();

            G.XRVisualConfigurator.StartFading(0.0f);

            G.SysMenu.EnableHUD(true);
        }

        public static IEnumerator EnterDownloadedWorld(World world)
        {
            // Last chance...
            yield return world.TemplateContent.WaitFor();
            yield return world.DecorationContent.WaitFor();

            Debug.Log($"Download complete");

            // Once the world is placed in global, the old value will be discarded,
            // leading the AssetBundle will be unloaded.
            AssetBundle templateContent = (AssetBundle)world.TemplateContent;
            yield return G.SceneLoader.LoadScene(templateContent, templateContent.IsFallback, false);

            // In any case, set up a World Editor Data, for remote world editing on a bare template.
            SettingsManager.SetupWorldObjectRoot();

            // If we found decorations, act on them.
            if (world.DecorationContent.Result != null)
                yield return G.WorldEditorData.BuildWorld(world.DecorationContent.Result);

            // Tell everyone that the world is done, even with the world editor objects, like...
            // - populating the spawned objects which needs blueprints for the spawnees
            // - start up live content
            OnWorldFinished?.Invoke();

            G.XRControl.MoveRig();
        }
    }
}
