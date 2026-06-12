using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace RouteRandomRedexed.Helpers;

public static class LethalLevelLoaderCompat
{
    private const string PatchedContentTypeName = "LethalLevelLoader.PatchedContent";
    private const string ExtendedLevelsPropertyName = "ExtendedLevels";
    private const string IsRouteLockedPropertyName = "IsRouteLocked";

    private static bool warnedAboutFailure;

    public static void UnlockAllRoutes() {
        if (!TryGetExtendedLevels(out IEnumerable? extendedLevels) || extendedLevels is null) {
            return;
        }

        int unlockedCount = 0;
        foreach (object extendedLevel in extendedLevels) {
            if (TryUnlockRoute(extendedLevel)) {
                unlockedCount++;
            }
        }

        if (unlockedCount > 0) {
            RouteRandomRedexed.Log.LogInfo($"Unlocked {unlockedCount} LethalLevelLoader routes.");
        }
    }

    private static bool TryGetExtendedLevels(out IEnumerable? extendedLevels) {
        extendedLevels = null;

        Type patchedContentType = AccessTools.TypeByName(PatchedContentTypeName);
        if (patchedContentType == null) {
            WarnOnce("Failed to find LethalLevelLoader.PatchedContent for route unlock compatibility.");
            return false;
        }

        PropertyInfo extendedLevelsProperty = AccessTools.Property(patchedContentType, ExtendedLevelsPropertyName);
        if (extendedLevelsProperty == null) {
            WarnOnce("Failed to find LethalLevelLoader PatchedContent.ExtendedLevels for route unlock compatibility.");
            return false;
        }

        object? extendedLevelsValue = extendedLevelsProperty.GetValue(null);
        extendedLevels = extendedLevelsValue as IEnumerable;
        if (extendedLevels is null) {
            WarnOnce("LethalLevelLoader PatchedContent.ExtendedLevels was empty or unavailable.");
            return false;
        }

        return true;
    }

    private static bool TryUnlockRoute(object extendedLevel) {
        PropertyInfo isRouteLockedProperty = AccessTools.Property(extendedLevel.GetType(), IsRouteLockedPropertyName);
        if (isRouteLockedProperty == null || !isRouteLockedProperty.CanWrite) {
            WarnOnce("Failed to find writable LethalLevelLoader ExtendedLevel.IsRouteLocked property.");
            return false;
        }

        if (isRouteLockedProperty.GetValue(extendedLevel) is not bool isRouteLocked || !isRouteLocked) {
            return false;
        }

        isRouteLockedProperty.SetValue(extendedLevel, false);
        return true;
    }

    private static void WarnOnce(string message) {
        if (warnedAboutFailure) {
            return;
        }

        warnedAboutFailure = true;
        RouteRandomRedexed.Log.LogWarning(message);
    }
}
