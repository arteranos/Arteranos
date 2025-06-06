/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;
using Ipfs;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DitzelGames.FastIK;
using Newtonsoft.Json;
using Arteranos.Core;
using Arteranos.Core.Managed;
using Arteranos.Common;

namespace Arteranos.Avatar
{
    internal class ObjectStats : IObjectStats
    {
        public int Count { get; set; }
        public int Vertices { get; set; }
        public int Triangles { get; set; }
        public int Materials { get; set; }
        public float Rating { get; set; }
    }

    internal class BoneTranslations
    {
        public Dictionary<string, string> translationTable;
    }

    internal class InstallAnimController : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.01f;
        public string Caption { get; set; } = "Installing animation controller";
        public Action<float> ProgressChanged { get; set; }

        public Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AvatarDownloaderContext context = _context as AvatarDownloaderContext;

            if (context.InstallAnimController)
            {
                UnityEngine.Avatar avatar;
                // Preconditions:
                //  - Avatar must be in T-Pose
                //  - Skeleton must contain the HumanTrait.RequiredBone's

                List<HumanBone> bones = new();
                TextAsset ta = BP.I.RPMBoneTranslation;
                BoneTranslations table = JsonConvert.DeserializeObject<BoneTranslations>(ta.text);

                foreach (KeyValuePair<string, string> bone in table.translationTable)
                {
                    bones.Add(new()
                    {
                        boneName = bone.Key,    // Skeleton's bone names
                        humanName = bone.Value, // Mecanim's bone names
                        limit = new()
                        {
                            // Refer to the skeleton's T pose as the base of the animations.
                            useDefaultValues = true
                        }
                    });
                }

                // Build the avatar with the minimal dataset
                HumanDescription hd = new()
                {
                    human = bones.ToArray(),
                };

                avatar = AvatarBuilder.BuildHumanAvatar(context.Avatar, hd);
                avatar.name = "Generated Human Avatar";

                if (!avatar.isValid)
                    throw new ArgumentException("Avatar is considered invalid.");

                if (!avatar.isHuman)
                    throw new ArgumentException("Avatar is considered nonhuman.");
                Animator animator = context.Avatar.AddComponent<Animator>();
                animator.runtimeAnimatorController = BP.I.AvatarAnimController;
                animator.avatar = avatar;
            }

            return Task.FromResult<Context>(context);
        }
    }

    internal class InstallIKHandlers : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.01f;
        public string Caption { get; set; } = "Installing IK handlers";
        public Action<float> ProgressChanged { get; set; }

        public Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AvatarDownloaderContext context = _context as AvatarDownloaderContext;

            Transform avatarTransform = context.Avatar.transform;

            context.JointNames = new();

            if (context.InstallFootIK)
            {
                Transform footHandle;
                FootIKCollider footIK;

                foreach (FootIKData foot in context.Feet)
                {
                    footHandle = RigIK(foot.FootTransform, avatarTransform, new Vector3(0, 0, 2));
                    if (context.InstallFootIKCollider)
                    {
                        footIK = footHandle.gameObject.AddComponent<FootIKCollider>();
                        footIK.Elevation = foot.Elevation;
                        footIK.rootTransform = avatarTransform;
                        footIK.guidedTransform = foot.FootTransform;
                    }
                }
            }

            if (context.ReadFootJoints)
            {
                foreach (FootIKData foot in context.Feet)
                    ReadJointNames(foot.FootTransform, context.JointNames);
            }

            if (context.InstallHandIK)
            {
                Transform handHandle;
                HandIKController handIK;
                handHandle = RigIK(context.LeftHand, avatarTransform);
                if (context.InstallHandIKController)
                {
                    handIK = handHandle.gameObject.AddComponent<HandIKController>();
                    handIK.RightSide = false;
                    handIK.AvatarMeasures = context;
                }

                handHandle = RigIK(context.RightHand, avatarTransform);
                if (context.InstallHandIKController)
                {
                    handIK = handHandle.gameObject.AddComponent<HandIKController>();
                    handIK.RightSide = true;
                    handIK.AvatarMeasures = context;
                }
            }

            if (context.ReadHandJoints)
            {
                ReadJointNames(context.LeftHand, context.JointNames);
                ReadJointNames(context.RightHand, context.JointNames);
            }

            return Task.FromResult<Context>(context);
        }

        private Transform RigIK(Transform limb, Transform at, Vector3? poleOffset = null, int bones = 2)
        {
            if (!limb) return null;

            Transform pole = null;

            if (poleOffset != null)
            {
                pole = new GameObject($"Pole_{limb.name}").transform;
                pole.SetPositionAndRotation(
                    limb.position + at.rotation * poleOffset.Value,
                    limb.rotation);
                pole.SetParent(at);
            }

            Transform handle = new GameObject($"Handle_{limb.name}").transform;
            handle.SetPositionAndRotation(limb.position, limb.rotation);
            handle.SetParent(at);

            FastIKFabric limbIK = limb.gameObject.AddComponent<FastIKFabric>();

            limbIK.ChainLength = bones;
            limbIK.Target = handle;
            limbIK.Pole = pole;

            return handle;
        }

        private static void ReadJointNames(Transform limb, List<string> jointNames, int bones = 2)
        {
            // For Network IK, note down all of the affected joints
            Transform bone = limb;
            for (int i = 0; i <= bones; i++)
            {
                jointNames.Add(bone.name);
                bone = bone.parent;
            }
        }
    }

    internal class InstallAnimationOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.01f;
        public string Caption { get; set; } = "Installing the animation handlers";
        public Action<float> ProgressChanged { get; set; }

        public Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AvatarDownloaderContext context = _context as AvatarDownloaderContext;

            if (context.InstallEyeAnimation)
            {
                AvatarEyeAnimator a = context.Avatar.AddComponent<AvatarEyeAnimator>();
                a.AvatarMeasures = context;
            }

            if (context.InstallMouthAnimation)
            {
                AvatarMouthAnimator a = context.Avatar.AddComponent<AvatarMouthAnimator>();
                a.AvatarMeasures = context;
            }

            return Task.FromResult<Context>(context);
        }
    }

    internal class FindBlendShapesOp : IAsyncOperation<Context>
    {
        private const string MOUTH_OPEN_BLEND_SHAPE_NAME = "mouthOpen";
        private const string EYE_BLINK_LEFT_BLEND_SHAPE_NAME = "eyeBlinkLeft";
        private const string EYE_BLINK_RIGHT_BLEND_SHAPE_NAME = "eyeBlinkRight";


        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.01f;
        public string Caption { get; set; } = "Getting Blend Shapes";

        public Action<float> ProgressChanged { get; set; }

        public Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AvatarDownloaderContext context = _context as AvatarDownloaderContext;

            Transform avatarTransform = context.Avatar.transform;

            context.MouthOpen = new();
            FindBlendShapedMeshes(avatarTransform, MOUTH_OPEN_BLEND_SHAPE_NAME, context.MouthOpen);

            context.EyeBlinkLeft = new();
            FindBlendShapedMeshes(avatarTransform, EYE_BLINK_LEFT_BLEND_SHAPE_NAME, context.EyeBlinkLeft);

            context.EyeBlinkRight = new();
            FindBlendShapedMeshes(avatarTransform, EYE_BLINK_RIGHT_BLEND_SHAPE_NAME, context.EyeBlinkRight);

            return Task.FromResult<Context>(context);
        }

        public void FindBlendShapedMeshes(Transform t, string whichBlendShape, List<MeshBlendShapeIndex> collected)
        {
            SkinnedMeshRenderer[] meshes = t.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer mesh in meshes)
            {
                int index = mesh.sharedMesh.GetBlendShapeIndex(whichBlendShape);
                if (index >= 0)
                    collected.Add(new()
                    {
                        Index = index,
                        Renderer = mesh,
                    });
            }
        }
    }

    internal class MeasureSkeletonOp : IAsyncOperation<Context>
    {
        private const string BONE_ARMATURE = "Armature";

        private readonly ObjectStats warningLevels = new()
        {
            Count = 6,
            Vertices = 12000,
            Triangles = 60000,
            Materials = 6,
        };

        private readonly ObjectStats errorLevels = new()
        {
            Count = 10,
            Vertices = 16000,
            Triangles = 90000,
            Materials = 10,
        };

        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.01f;
        public string Caption { get; set; } = "Fixup avatar skeleton";

        public Action<float> ProgressChanged { get; set; }

        // Synced task -- needs to be in the main thread !
        public Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AvatarDownloaderContext context = _context as AvatarDownloaderContext;

            Transform avatarTransform = context.Avatar.transform;
            Transform armature = avatarTransform.Find(BONE_ARMATURE);

            // Maybe I had to look for the way to get the bounding box.
            context.UnscaledHeight = FoldTransformHierarchy(avatarTransform, 0.0f,
                (t, f) => t.position.y > f ? t.position.y : f);

            if (context.DesiredHeight != 0 && context.UnscaledHeight > 0)
            {
                // Scale up (or down) the avatar to the desired height before
                // to do the measuring.
                float scale = context.DesiredHeight / context.UnscaledHeight;
                avatarTransform.localScale = new Vector3(scale, scale, scale);
                context.FullHeight = FoldTransformHierarchy(avatarTransform, 0.0f,
                    (t, f) => t.position.y > f ? t.position.y : f);
            }
            else
                context.FullHeight = context.UnscaledHeight;

            // Pull up the other children besides the bones in the hierarchy root
            PullupMeshes(avatarTransform);

            // Try to find the IK relevant limbs
            context.LeftHand = AvatarDownloader.TrySidedLimb(context, "Hand", false);
            context.RightHand = AvatarDownloader.TrySidedLimb(context, "Hand", true);

            context.Head = armature.FindRecursive("Head");

            context.Feet = new();
            List<Transform> feetTransforms = new();
            FindLimbsPattern(armature, "(.*Foot|Foot.*)", feetTransforms);
            foreach (Transform transform in feetTransforms)
                context.Feet.Add(new()
                {
                    FootTransform = transform,
                    Elevation = transform.position.y
                });

            // Find the eyes (usually two ... :)
            context.Eyes = new();
            FindLimbsPattern(armature, "(.*Eye|Eye.*)", context.Eyes);

            // The Avatar Point Of View. Between the eyes, but not if the avatar breakdancing...
            Vector3 centerEyePos = Vector3.zero;
            foreach (Transform t in context.Eyes) centerEyePos += t.position;
            GameObject cEyeGO = new("Avatar_POV");
            if (context.Eyes.Count > 0)
                centerEyePos /= context.Eyes.Count;
            cEyeGO.transform.parent = avatarTransform;
            cEyeGO.transform.position = centerEyePos;
            context.CenterEye = cEyeGO.transform;
            context.EyeHeight = context.CenterEye.position.y;

            // Get the overall rating about the avatar
            Core.Utils.RateGameObject(context.Avatar, warningLevels, errorLevels, context);

            return Task.FromResult<Context>(context);
        }

        private static void PullupMeshes(Transform avatarTransform)
        {
            Transform armature = avatarTransform.Find(BONE_ARMATURE);
            if (!armature) return;

            for (int i = 0; i < armature.childCount; i++)
            {
                Transform transform = armature.GetChild(i);
                if (transform.gameObject.GetComponent<Renderer>())
                {
                    transform.parent = avatarTransform;
                    i--;
                }
            }
        }

        public static void FindLimbsPattern(Transform t, string pattern, List<Transform> collected)
        {
            Match m = Regex.Match(t.name, pattern, RegexOptions.IgnoreCase);
            if (m.Success) collected.Add(t);

            foreach (Transform transform in t)
                FindLimbsPattern(transform, pattern, collected);
        }

        public static T FoldTransformHierarchy<T>(Transform t, T start, Func<Transform, T, T> progress)
        {
            start = progress(t, start);
            foreach (Transform tc in t)
                start = FoldTransformHierarchy(tc, start, progress);

            return start;
        }
    }

    internal class LoadAvatarObjOp : IAsyncOperation<Context>
    {
        public int Timeout { get; set; }
        public float Weight { get; set; } = 0.5f;
        public string Caption { get; set; } = "Setting up game object";
        public Action<float> ProgressChanged { get; set; }

        public async Task<Context> ExecuteAsync(Context _context, CancellationToken token)
        {
            AvatarDownloaderContext context = _context as AvatarDownloaderContext;

            IPFSGLTFObject obj = new(context.path, token)
            {
                InitActive = false
            };

            context.Avatar = await obj.GameObject;

            if (context.Avatar)
            {
                // As an afterthought, pin the avatar in the local IPFS node.
                try
                {
                    _ = G.IPFSService.PinCid(context.path, true);
                }
                catch { }

            }

            // Debug.Log($"AvatarDownloader ({context.path}): Exiting load avatar");

            return context;
        }
    }

    public class AvatarDownloader : MonoBehaviour, IAvatarDownloader
    {
        private void Awake()
        {
            G.AvatarDownloader = this;
        }

        public (AsyncOperationExecutor<Context>, Context) PrepareDownloadAvatar(Cid cid, IAvatarDownloaderOptions options = null, int timeout = 600)
        {
            AvatarDownloaderContext context = new()
            {
                path = cid,
                TargetFile = GetAvatarCacheFile(cid),

                InstallAnimController = options?.InstallAnimController ?? false,
                InstallEyeAnimation = options?.InstallEyeAnimation ?? false,
                InstallMouthAnimation = options?.InstallMouthAnimation ?? false,
                InstallFootIK = options?.InstallFootIK ?? false,
                InstallFootIKCollider = options?.InstallFootIKCollider ?? false,
                InstallHandIKController = options?.InstallHandIKController ?? false,
                InstallHandIK = options?.InstallHandIK ?? false,
                ReadFootJoints = options?.ReadFootJoints ?? false,
                ReadHandJoints = options?.ReadHandJoints ?? false,
                DesiredHeight = options?.DesiredHeight ?? 0,
            };

            AsyncOperationExecutor<Context> executor = new(new IAsyncOperation<Context>[]
            {
                new LoadAvatarObjOp(),
                new MeasureSkeletonOp(),
                new FindBlendShapesOp(),
                new InstallAnimationOp(),
                new InstallIKHandlers(),
                new InstallAnimController()
            })
            { Timeout = timeout };

            return (executor, context);
        }

        /// <summary>
        /// Try to find a particular limb, like left foot or the right hand. Uses some
        /// heuristics to determine the naming pattern, like lHand, Left_Hand, leftHand
        /// and so on.
        /// </summary>
        /// <param name="context">The avatar downloader context</param>
        /// <param name="limb">Hand, Arm, Leg, ...</param>
        /// <param name="right">true if the right one</param>
        /// <returns>The limb transform, or null if it's missing (Ouch..!)</returns>
        /// <remarks>Updates the context once the naming pattern has been found.</remarks>
        internal static Transform TrySidedLimb(AvatarDownloaderContext context, string limb, bool right)
        {
            /// <summary>
            /// 0 - Limb name
            /// 1 - Side name letter ('l' or 'r')
            /// 2 - Side name word ('left' or 'right')
            /// </summary>
            string[] SidedLimbPattern = new string[]
            {
            "{1}{0}",       // lHand
            "{0}{1}",       // Handl
            "{1}_{0}",      // l_Hand
            "{0}_{1}",      // Hand_l
            "{2}{0}",       // leftHand
            "{0}{2}",       // Handleft
            "{2}_{0}",      // left_Hand
            "{0}_{2}",      // Hand_left
            };

            static void GetSideWord(bool right, out string letter, out string word, bool sidedCapitalized)
            {
                if (sidedCapitalized == true)
                {
                    letter = right ? "R" : "L";
                    word = right ? "Right" : "Left";
                }
                else
                {
                    letter = right ? "r" : "l";
                    word = right ? "right" : "left";
                }
            }

            Transform avatarTransform = context.Avatar.transform;

            // We've already got something, stick to the pattern.
            if (context.SidedCapitalized != null)
            {
                bool sidedCapitalized = context.SidedCapitalized.Value;
                GetSideWord(right, out string letter, out string word, sidedCapitalized);

                string toFind = string.Format(SidedLimbPattern[context.SidedPatternIndex], limb, letter, word);
                return avatarTransform.FindRecursive(toFind);
            }

            foreach (bool tryCap in new bool[] { false, true })
            {
                GetSideWord(right, out string letter, out string word, tryCap);
                for (int i = 0; i < SidedLimbPattern.Length; i++)
                {
                    string toFind = string.Format(SidedLimbPattern[i], limb, letter, word);
                    Transform found = avatarTransform.FindRecursive(toFind);
                    if (found != null)
                    {
                        context.SidedCapitalized = tryCap;
                        context.SidedPatternIndex = i;
                        return found;
                    }
                }
            }

            return null;
        }

        public string GetAvatarCacheFile(Cid cid)
            => $"{ConfigUtils.temporaryCachePath}/AvatarCache/{Core.Utils.GetURLHash(cid)}.glb";

        public GameObject GetLoadedAvatar(Context _context)
            => (_context as AvatarDownloaderContext).Avatar;

        public IObjectStats GetAvatarRating(Context _context)
            => _context as IObjectStats;

        public IAvatarMeasures GetAvatarMeasures(Context _context)
            => _context as IAvatarMeasures;
    }
}