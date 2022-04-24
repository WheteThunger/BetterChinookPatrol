using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Better Chinook Path", "WhiteThunder", "0.1.0")]
    [Description("Properly randomizes the order in which chinooks visit monuments.")]
    internal class BetterChinookPath : CovalencePlugin
    {
        #region Fields

        private const float VanillaDropZoneDistanceTolerance = 200;

        private Configuration _pluginConfig;
        private List<Vector3> _eligiblePatrolPoints = new List<Vector3>();

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginConfig.Init(this);
        }

        private void OnServerInitialized()
        {
            var sb = new StringBuilder();
            var dropZoneCount = 0;

            foreach (var monumentInfo in TerrainMeta.Path.Monuments)
            {
                if (!_pluginConfig.AllowsMonument(monumentInfo))
                    continue;

                _eligiblePatrolPoints.Add(monumentInfo.transform.position);

                var hasDropZone = false;
                var closestDropZone = CH47DropZone.GetClosest(monumentInfo.transform.position);
                if (closestDropZone != null)
                {
                    hasDropZone = Vector3Ex.Distance2D(closestDropZone.transform.position, monumentInfo.transform.position) < VanillaDropZoneDistanceTolerance;
                }

                if (hasDropZone)
                {
                    dropZoneCount++;
                }

                var dropZoneInfo = hasDropZone ? " -- HAS DROP ZONE" : string.Empty;
                sb.AppendLine($"- {monumentInfo.name}{dropZoneInfo}");
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

                if (_pluginConfig.MinCrateDropsPerChinook > 1 && _pluginConfig.MaxCrateDropsPerChinook > 1)
                {
                    ch47.numCrates = UnityEngine.Random.Range(_pluginConfig.MinCrateDropsPerChinook, _pluginConfig.MaxCrateDropsPerChinook + 1);
                }
            });
        }

        #endregion

        #region Exposed Hooks

        private bool ChinookWasBlocked(CH47HelicopterAIController ch47)
        {
            var result = Interface.CallHook("OnBetterChinookPath", ch47);
            return result is bool && (bool)result == false;
        }

        #endregion

        #region Pathfinder

        private class BetterCH47PathFinder : CH47PathFinder
        {
            private const float RevisitMaxProximity = 100;

            public List<Vector3> _patrolPath = new List<Vector3>();
            private int _patrolPathIndex = 0;

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
                {
                    return Vector3.zero;
                }

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

            [JsonProperty("Disallowed monument types")]
            private string[] DisallowedMonumentTypesNames = new string[]
            {
                "Cave",
                "WaterWell",
            };

            [JsonProperty("Disallowed monument tiers")]
            private string[] DisallowedMonumentTierNames = new string[]
            {
                "Tier0"
            };

            [JsonProperty("Disallowed monument prefabs (partial match)")]
            private string[] DisallowedMonumentPrefabs = new string[0];

            [JsonProperty("Force allow monument prefabs (partial match)")]
            private string[] ForceAllowedMonumentPrefabs = new string[0];

            public void Init(BetterChinookPath pluginInstance)
            {
                if (DisallowedMonumentTypesNames != null)
                {
                    foreach (var monumentTypeName in DisallowedMonumentTypesNames)
                    {
                        MonumentType monumentType;
                        if (Enum.TryParse<MonumentType>(monumentTypeName, out monumentType))
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
                        if (Enum.TryParse<MonumentTier>(monumentTierName, out monumentTier))
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

            public bool AllowsMonument(MonumentInfo monumentInfo)
            {
                var monumentName = monumentInfo.name;
                if (monumentName.Contains("monument_marker.prefab"))
                {
                    monumentName = monumentInfo.transform.root.name;
                }

                if (ForceAllowedMonumentPrefabs != null)
                {
                    foreach (var allowedPartialPrefab in ForceAllowedMonumentPrefabs)
                    {
                        if (string.IsNullOrWhiteSpace(allowedPartialPrefab))
                            continue;

                        if (monumentName.Contains(allowedPartialPrefab, CompareOptions.IgnoreCase))
                            return true;
                    }
                }

                if (DisallowedMonumentPrefabs != null)
                {
                    foreach (var allowedPartialPrefab in DisallowedMonumentPrefabs)
                    {
                        if (string.IsNullOrWhiteSpace(allowedPartialPrefab))
                            continue;

                        if (monumentName.Contains(allowedPartialPrefab, CompareOptions.IgnoreCase))
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

        #region Configuration Boilerplate

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

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
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
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion

        #endregion
    }
}
