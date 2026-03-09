#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using DrWario.Runtime;

namespace DrWario.Editor
{
    /// <summary>
    /// Captures a snapshot of the active scene's composition.
    /// Called once on session start, not per frame.
    /// </summary>
    public static class SceneCensusCapture
    {
        public static SceneCensus Capture()
        {
            var census = new SceneCensus();

            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.IsValid()) return census;

                // Count all active GameObjects
                var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                census.TotalGameObjects = allObjects.Length;

                // Component distribution — count by type name
                var componentCounts = new Dictionary<string, int>();
                int totalComponents = 0;
                foreach (var go in allObjects)
                {
                    var components = go.GetComponents<Component>();
                    foreach (var c in components)
                    {
                        if (c == null) continue; // Missing script
                        totalComponents++;
                        string typeName = c.GetType().Name;
                        if (componentCounts.ContainsKey(typeName))
                            componentCounts[typeName]++;
                        else
                            componentCounts[typeName] = 1;
                    }
                }
                census.TotalComponents = totalComponents;

                // Top 20 component types
                census.ComponentDistribution = componentCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(20)
                    .Select(kv => new ComponentCount { TypeName = kv.Key, Count = kv.Value })
                    .ToArray();

                // Lights by type
                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var light in lights)
                {
                    switch (light.type)
                    {
                        case LightType.Directional: census.DirectionalLights++; break;
                        case LightType.Point: census.PointLights++; break;
                        case LightType.Spot: census.SpotLights++; break;
                        case LightType.Rectangle: census.AreaLights++; break;
                    }
                }

                // Key subsystems
                census.CanvasCount = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length;
                census.CameraCount = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
                census.ParticleSystemCount = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length;
                census.LODGroupCount = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length;
                census.RigidbodyCount = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
                census.Rigidbody2DCount = Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None).Length;

                census.IsValid = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DrWario] Scene census capture failed: {e.Message}");
            }

            return census;
        }

        /// <summary>
        /// Captures active MonoBehaviour types with instance counts and sample GameObject names.
        /// Called once at session start alongside the scene census.
        /// Returns top 30 script types by instance count.
        /// </summary>
        public static List<ActiveScriptEntry> CaptureActiveScripts()
        {
            var result = new List<ActiveScriptEntry>();
            try
            {
                var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

                // Group by type: track count + sample GameObject names
                var counts = new Dictionary<string, int>();
                var sampleNames = new Dictionary<string, List<string>>();

                foreach (var mb in allBehaviours)
                {
                    if (mb == null || !mb.enabled) continue;
                    string typeName = mb.GetType().Name;

                    // Skip Unity internals, built-in UI, and DrWario's own components
                    string ns = mb.GetType().Namespace ?? "";
                    if (typeName.StartsWith("DrWario") || typeName == "RuntimeCollector")
                        continue;
                    if (ns.StartsWith("UnityEngine") || ns.StartsWith("Unity.") ||
                        ns.StartsWith("TMPro") || ns.StartsWith("UnityEditor"))
                        continue;

                    counts.TryGetValue(typeName, out int c);
                    counts[typeName] = c + 1;

                    if (!sampleNames.TryGetValue(typeName, out var names))
                    {
                        names = new List<string>();
                        sampleNames[typeName] = names;
                    }
                    if (names.Count < 3)
                        names.Add(mb.gameObject.name);
                }

                // Sort by instance count descending, take top 30
                foreach (var kv in counts.OrderByDescending(kv => kv.Value).Take(30))
                {
                    result.Add(new ActiveScriptEntry
                    {
                        TypeName = kv.Key,
                        InstanceCount = kv.Value,
                        SampleGameObjectNames = sampleNames[kv.Key].ToArray()
                    });
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DrWario] Active scripts capture failed: {e.Message}");
            }

            return result;
        }
    }
}
#endif
