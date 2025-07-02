/*
 * Copyright (c) 2023, willneedit
 * 
 * Licensed by the Mozilla Public License 2.0,
 * residing in the LICENSE.md file in the project's root directory.
 */

using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Arteranos.Core
{
    public static partial class Extensions
    {
        /// <summary>
        /// Finds the transform in the hierarchy tree by name, including searching the
        /// entire subtree below.
        /// </summary>
        /// <param name="t">The transform to begin searching</param>
        /// <param name="name">The transform's name to search for</param>
        /// <returns>The first found transform, otherwise null</returns>
        public static Transform FindRecursive(this Transform t, string name)
        {
            if(t.name == name) return t;

            for(int i = 0, c = t.childCount; i < c; i++)
            {
                Transform res = FindRecursive(t.GetChild(i), name);
                if(res != null) return res;
            }

            return null;
        }

        /// <summary>
        /// Performs a movement over time to a dedicated transform, like with a camera movement
        /// in a cutscene.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="targetTransform"></param>
        /// <param name="duration"></param>
        /// <param name="token"></param>
        public static async Task LerpTransform(this Transform transform,
            Transform targetTransform, float duration, CancellationToken token)
        {
            // ctx?.Cancel();
            // ctx = new CancellationTokenSource();

            float time = 0f;
            Vector3 startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            Vector3 startScale = transform.localScale;

            while(time < duration && !token.IsCancellationRequested)
            {
                float t = time / duration;

                t = 0.5f - (float) Mathf.Cos(t * Mathf.PI) * 0.5f;
                transform.SetLocalPositionAndRotation(Vector3.Lerp(startPosition, targetTransform.localPosition, t), Quaternion.Lerp(startRotation, targetTransform.localRotation, t));
                transform.localScale = Vector3.Lerp(startScale, targetTransform.localScale, t);
                time += Time.deltaTime;
                await Task.Delay(8);
            }

            if(!token.IsCancellationRequested)
            {
                transform.SetLocalPositionAndRotation(targetTransform.localPosition, targetTransform.localRotation);
                transform.localScale = targetTransform.localScale;
            }
        }

        public static string HumanReadable(this DateTime dt)
        {
            TimeSpan difference = DateTime.UtcNow - dt;

            double t = difference.TotalMinutes; int ti = (int)t;
            if (t < 30)
                return t switch
                {
                    < 0.1f => "just now",
                    < 1.0f => "less than a minute",
                    < 2.0f => "one minute ago",
                    _ => $"{ti} minutes ago"
                };

            t = difference.TotalHours; ti = (int)t;
            if (t < 12)
                return t switch
                {
                    < 1.0f => "less than a hour",
                    < 2.0f => "a hour ago",
                    _ => $"{ti} hours ago"
                };

            t = difference.TotalDays; ti = (int)t;
            if (t < 360)
                return t switch
                {
                    < 1.0f => "less than a day",
                    < 2.0f => "yesterday",
                    < 7.0f => $"{ti} days ago",
                    < 10.0f => "around a week ago",
                    < 15.0f => "around two weeks ago",
                    < 30.0f => "around three weeks ago",
                    < 40.0f => "around a month ago",
                    < 60.0f => "more than last month",
                    < 180.0f => $"{ti} months ago",
                    _ => "more than half a year"
                };

            return "more than a year ago";

        }

        public static int CommonStart(this string s1, string s2)
        {
            int full = Math.Min(s1.Length, s2.Length);
            for (int i = 0; i < full; i++) if (s1[i] != s2[i]) return i;

            return full;
        }
    }
}
