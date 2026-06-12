using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RouteRandomRedexed.Helpers;

namespace RouteRandomRedexed;

internal static class RandomRouteOnlyCompat
{
    private const string helperTypeName = "RandomRouteOnly.Helper";
    private const string configManagerTypeName = "RandomRouteOnly.ConfigManager";

    internal static List<CompatibleNoun> FilterRecentLevels(List<CompatibleNoun> routePlanetNodes) {
        if (!TryGetRecentLevelIds(out HashSet<int> recentLevelIds) || recentLevelIds.Count == 0) {
            return routePlanetNodes;
        }

        List<CompatibleNoun> filteredRoutePlanetNodes = [.. routePlanetNodes];
        foreach (CompatibleNoun compatibleNoun in routePlanetNodes.ToList()) {
            TerminalNode confirmNode = compatibleNoun.result.GetNodeAfterConfirmation();
            SelectableLevel moonLevel = StartOfRound.Instance.levels[confirmNode.buyRerouteToMoon];
            if (recentLevelIds.Contains(moonLevel.levelID)) {
                filteredRoutePlanetNodes.Remove(compatibleNoun);
            }
        }

        if (filteredRoutePlanetNodes.Count > 0) {
            return filteredRoutePlanetNodes;
        }

        // If there would be no moons left to choose, clear the recent moons list and use the full list instead.
        TryClearRecentLevels();
        return routePlanetNodes;
    }

    internal static TerminalNode GetWeightedRandom(List<CompatibleNoun> routePlanetNodes) {
        if (!TryGetLevelWeights(out Dictionary<int, int> levelWeights) || levelWeights.Count == 0) {
            return routePlanetNodes[UnityEngine.Random.Range(0, routePlanetNodes.Count)].result;
        }

        List<(int levelID, TerminalNode result, int weight)> weightedCandidates = [];
        foreach (CompatibleNoun compatibleNoun in routePlanetNodes) {
            TerminalNode confirmNode = compatibleNoun.result.GetNodeAfterConfirmation();
            int levelIndex = confirmNode.buyRerouteToMoon;
            if (levelIndex < 0 || levelIndex >= StartOfRound.Instance.levels.Length) {
                continue;
            }

            int levelID = StartOfRound.Instance.levels[levelIndex].levelID;
            if (levelWeights.TryGetValue(levelID, out int weight) && weight > 0) {
                weightedCandidates.Add((levelID, compatibleNoun.result, weight));
            }
        }

        if (weightedCandidates.Count == 0) {
            return routePlanetNodes[UnityEngine.Random.Range(0, routePlanetNodes.Count)].result;
        }

        int weightSum = weightedCandidates.Sum(candidate => candidate.weight);
        int selectionRoll = UnityEngine.Random.Range(1, weightSum + 1);
        RouteRandomRedexed.Log.LogDebug("Level selection roll = " + selectionRoll);

        foreach ((int levelID, TerminalNode result, int weight) in weightedCandidates) {
            if (selectionRoll <= weight) {
                RouteRandomRedexed.Log.LogInfo("Randomly selected level " + GetLevelName(levelID));
                return result;
            }

            selectionRoll -= weight;
            RouteRandomRedexed.Log.LogDebug("Skipping level " + levelID);
        }

        // Fallback if the weighted walk somehow misses due to bad data.
        return weightedCandidates[0].result;
    }

    private static bool TryGetRecentLevelIds(out HashSet<int> recentLevelIds) {
        recentLevelIds = [];

        if (!TryGetStaticMemberValue(helperTypeName, "recentLevels", out object recentLevelsValue) || recentLevelsValue is null) {
            return false;
        }

        if (recentLevelsValue is not IEnumerable enumerable) {
            return false;
        }

        foreach (object entry in enumerable) {
            if (entry is int levelID) {
                recentLevelIds.Add(levelID);
            }
        }

        return recentLevelIds.Count > 0;
    }

    private static bool TryClearRecentLevels() {
        if (!TryGetStaticMemberValue(helperTypeName, "recentLevels", out object recentLevelsValue) || recentLevelsValue is null) {
            return false;
        }

        MethodInfo clearMethod = recentLevelsValue.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
        if (clearMethod is null) {
            return false;
        }

        clearMethod.Invoke(recentLevelsValue, null);
        return true;
    }

    private static bool TryGetLevelWeights(out Dictionary<int, int> levelWeights) {
        levelWeights = [];

        if (!TryGetStaticMemberValue(configManagerTypeName, "levelWeights", out object levelWeightsValue) || levelWeightsValue is not IDictionary dictionary) {
            return false;
        }

        foreach (DictionaryEntry entry in dictionary) {
            if (entry.Key is not int levelID) {
                continue;
            }

            if (TryReadConfigEntryInt(entry.Value, out int weight) && weight > 0) {
                levelWeights[levelID] = weight;
            }
        }

        return levelWeights.Count > 0;
    }

    private static bool TryReadConfigEntryInt(object configEntry, out int value) {
        value = default;

        if (configEntry is null) {
            return false;
        }

        PropertyInfo valueProperty = configEntry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        object rawValue = valueProperty?.GetValue(configEntry);
        if (rawValue is int intValue) {
            value = intValue;
            return true;
        }

        return rawValue != null && int.TryParse(rawValue.ToString(), out value);
    }

    private static bool TryGetStaticMemberValue(string typeName, string memberName, out object value) {
        value = null;

        Type targetType = FindLoadedType(typeName);
        if (targetType is null) {
            return false;
        }

        FieldInfo field = AccessTools.Field(targetType, memberName);
        if (field is not null) {
            value = field.GetValue(null);
            return true;
        }

        PropertyInfo property = AccessTools.Property(targetType, memberName);
        if (property is not null) {
            value = property.GetValue(null);
            return true;
        }

        return false;
    }

    private static Type FindLoadedType(string typeName) {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            Type targetType = assembly.GetType(typeName, throwOnError: false);
            if (targetType is not null) {
                return targetType;
            }
        }

        return null;
    }

    private static string GetLevelName(int levelID) {
        SelectableLevel level = StartOfRound.Instance.levels.FirstOrDefault(candidate => candidate.levelID == levelID);
        return level?.name ?? levelID.ToString();
    }
}
