using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Better Chinook Patrol", "WhiteThunder", "0.2.0")]
    [Description("Allows customizing which monuments chinooks will visit.")]
    internal class BetterChinookPatrol : CovalencePlugin
    {
        #region Fields

        private const float VanillaDropZoneDistanceTolerance = 200;

        private Configuration _config;
        private List<Vector3> _eligiblePatrolPoints = new List<Vector3>();

        #endregion

        #region Hooks

        private void Init()
        {
            _config.Init(this);
        }

        private void OnServerInitialized()
        {
            var sb = new StringBuilder();
            var dropZoneCount = 0;

            foreach (var monumentInfo in TerrainMeta.Path.Monuments)
            {
                if (_config.DisallowSafeZoneMonuments && monumentInfo.IsSafeZone)
                    continue;

                string monumentName;
                if (!_config.AllowsMonument(monumentInfo, out monumentName))
                    continue;

                var monumentPosition = monumentInfo.transform.position;
                _eligiblePatrolPoints.Add(monumentPosition);

                var hasDropZone = false;
                var closestDropZone = CH47DropZone.GetClosest(monumentPosition);
                if (closestDropZone != null)
                {
                    hasDropZone = Vector3Ex.Distance2D(closestDropZone.transform.position, monumentPosition) < VanillaDropZoneDistanceTolerance;
                }

                if (hasDropZone)
                {
                    dropZoneCount++;
                }

                var dropZoneInfo = hasDropZone ? " -- HAS DROP ZONE" : string.Empty;
                sb.AppendLine($"- {monumentName}{dropZoneInfo}");
            }

            Log($"{_eligiblePatrolPoints.Count} monuments on this map may be visited by Chinooks. {dropZoneCount} have drop zones.\n{sb}");
        }

        private void OnEntitySpawned(CH47HelicopterAIController ch47)
        {
            // Ignore reinforcement chinooks.
            if (ch47.ShouldLand())
                return;

            var brain = ch47.GetComponent<CH47AIBrain>();
            if (brain == null)
                return;

            NextTick(() =>
            {
                // If the brain doesn't have a path finder, perhaps another plugin is controlling it.
                var pathFinder = brain.PathFinder as CH47PathFinder;
                if (pathFinder == null)
                    return;

                if (ChinookWasBlocked(ch47))
                    return;

                brain.PathFinder = new BetterCH47PathFinder(_eligiblePatrolPoints);

                // If the chinook is already in a patrol state, its interest point must be updated.
                if (brain.CurrentState != null && brain.CurrentState.StateType == AIState.Patrol)
                {
                    brain.mainInterestPoint = brain.PathFinder.GetRandomPatrolPoint();
                }

                if (_config.MinCrateDropsPerChinook > 1 && _config.MaxCrateDropsPerChinook > 1)
                {
                    ch47.numCrates = UnityEngine.Random.Range(_config.MinCrateDropsPerChinook, _config.MaxCrateDropsPerChinook + 1);
                }
            });
        }

        #endregion

        #region Exposed Hooks

        private bool ChinookWasBlocked(CH47HelicopterAIController ch47)
        {
            var result = Interface.CallHook("OnBetterChinookPatrol", ch47);
            return result is bool && (bool)result == false;
        }

        #endregion

        #region Helpers

        private static class StringUtils
        {
            public static bool Equals(string a, string b) =>
                string.Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;

            public static bool Contains(string haystack, string needle) =>
                haystack.Contains(needle, CompareOptions.IgnoreCase);
        }

        #endregion

        #region Pathfinder

        private class BetterCH47PathFinder : CH47PathFinder
        {
            private const float RevisitMaxProximity = 100;

            public List<Vector3> _patrolPath;
            private int _patrolPathIndex;

            public BetterCH47PathFinder(List<Vector3> eligiblePatrolPoints)
            {
                // Randomly shuffle the patrol points.
                _patrolPath = eligiblePatrolPoints.OrderBy(x => UnityEngine.Random.Range(0, 1000)).ToList();

                for (var i = _patrolPath.Count - 1; i >= 0; i--)
                {
                    for (var j = 0; j < i; j++)
                    {
                        // Remove any patrol points that are close to previous patrol points.
                        if (Vector3Ex.Distance2D(_patrolPath[i], _patrolPath[j]) < RevisitMaxProximity)
                        {
                            _patrolPath.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            public override Vector3 GetRandomPatrolPoint()
            {
                if (_patrolPath.Count == 0)
                    return Vector3.zero;

                if (_patrolPathIndex >= _patrolPath.Count)
                {
                    _patrolPathIndex = 0;
                }

                return _patrolPath[_patrolPathIndex++];
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : SerializableConfiguration
        {
            [JsonIgnore]
            public List<MonumentType> DisallowedMonumentTypes = new List<MonumentType>();

            [JsonIgnore]
            public MonumentTier DisallowedMonumentTiersMask;

            [JsonProperty("Min crate drops per chinook")]
            public int MinCrateDropsPerChinook = 1;

            [JsonProperty("Max crate drops per chinook")]
            public int MaxCrateDropsPerChinook = 1;

            [JsonProperty("Disallow safe zone monuments")]
            public bool DisallowSafeZoneMonuments = true;

            [JsonProperty("Disallowed monument types")]
            private string[] DisallowedMonumentTypesNames =
            {
                "Cave",
                "WaterWell",
            };

            [JsonProperty("Disallowed monument tiers")]
            private string[] DisallowedMonumentTierNames =
            {
                "Tier0"
            };

            [JsonProperty("Disallowed monument prefabs (partial match)")]
            private string[] DisallowedMonumentPartialPrefabs = Array.Empty<string>();

            [JsonProperty("Disallowed monument prefabs (exact match)")]
            private string[] DisallowedMonumentExactPrefabs = Array.Empty<string>();

            [JsonProperty("Force allow monument prefabs (partial match)")]
            private string[] ForceAllowedMonumentPartialPrefabs = Array.Empty<string>();

            [JsonProperty("Force allow monument prefabs (exact match)")]
            private string[] ForceAllowedMonumentExactPrefabs = Array.Empty<string>();

            public void Init(BetterChinookPatrol pluginInstance)
            {
                if (DisallowedMonumentTypesNames != null)
                {
                    foreach (var monumentTypeName in DisallowedMonumentTypesNames)
                    {
                        MonumentType monumentType;
                        if (Enum.TryParse(monumentTypeName, ignoreCase: true, result: out monumentType))
                        {
                            DisallowedMonumentTypes.Add(monumentType);
                        }
                        else
                        {
                            pluginInstance.LogError($"Invalid monument type: {monumentTypeName}");
                        }
                    }
                }

                if (DisallowedMonumentTierNames != null)
                {
                    foreach (var monumentTierName in DisallowedMonumentTierNames)
                    {
                        MonumentTier monumentTier;
                        if (Enum.TryParse(monumentTierName, ignoreCase: true, result: out monumentTier))
                        {
                            DisallowedMonumentTiersMask |= monumentTier;
                        }
                        else
                        {
                            pluginInstance.LogError($"Invalid monument tier: {monumentTierName}");
                        }
                    }
                }
            }

            public bool AllowsMonument(MonumentInfo monumentInfo, out string monumentName)
            {
                monumentName = monumentInfo.name;
                if (monumentName.Contains("monument_marker.prefab"))
                {
                    monumentName = monumentInfo.transform.root.name;
                }

                if (ForceAllowedMonumentPartialPrefabs != null)
                {
                    foreach (var partialPrefab in ForceAllowedMonumentPartialPrefabs)
                    {
                        if (!string.IsNullOrWhiteSpace(partialPrefab) && StringUtils.Contains(monumentName, partialPrefab))
                            return true;
                    }
                }

                if (ForceAllowedMonumentExactPrefabs != null)
                {
                    foreach (var exactPrefab in ForceAllowedMonumentExactPrefabs)
                    {
                        if (!string.IsNullOrWhiteSpace(exactPrefab) && StringUtils.Equals(monumentName, exactPrefab))
                            return true;
                    }
                }

                if (DisallowedMonumentPartialPrefabs != null)
                {
                    foreach (var partialPrefab in DisallowedMonumentPartialPrefabs)
                    {
                        if (!string.IsNullOrWhiteSpace(partialPrefab) && StringUtils.Contains(monumentName, partialPrefab))
                            return false;
                    }
                }

                if (DisallowedMonumentExactPrefabs != null)
                {
                    foreach (var partialPrefab in DisallowedMonumentExactPrefabs)
                    {
                        if (!string.IsNullOrWhiteSpace(partialPrefab) && StringUtils.Equals(monumentName, partialPrefab))
                            return false;
                    }
                }

                if ((DisallowedMonumentTiersMask & monumentInfo.Tier) != 0)
                    return false;

                if (DisallowedMonumentTypes.Contains(monumentInfo.Type))
                    return false;

                return true;
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion
    }
}
