using BepInEx.Bootstrap;
using HarmonyLib;
using RouteRandomRedexed.Helpers;
using TMPro;
using UnityEngine.Video;

namespace RouteRandomRedexed.Patches;

[HarmonyPatch(typeof(StartOfRound))]
public class StartOfRoundPatch
{
	[HarmonyPostfix]
    [HarmonyPatch("Start")]
	private static void Compat(){
		// Check if LethalConstellations is active
		if(Chainloader.PluginInfos.ContainsKey("com.github.darmuh.LethalConstellations")) RouteRandomRedexed.constellationsLoaded = true;
        if(Chainloader.PluginInfos.ContainsKey("Index154.RandomRouteOnly")) RouteRandomRedexed.randomRouteOnlyLoaded = true;
        if(Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader")) RouteRandomRedexed.lethalLevelLoaderLoaded = true;
        if(RouteRandomRedexed.lethalLevelLoaderLoaded && RouteRandomRedexed.ConfigBypassLLLLock.Value) LethalLevelLoaderCompat.UnlockAllRoutes();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.SetMapScreenInfoToCurrentLevel))]
    public static void HideMapScreenInfo(StartOfRound __instance, VideoPlayer ___screenLevelVideoReel, TextMeshProUGUI ___screenLevelDescription) {
        if (__instance.currentLevel.name == "CompanyBuildingLevel" || !RouteRandomRedexed.ConfigHidePlanet.Value) {
            return;
        }

        ___screenLevelDescription.text = "Orbiting: [REDACTED]\nPopulation: Unknown\nConditions: Unknown\nFauna: Unknown\nWeather: Unknown";
        ___screenLevelVideoReel.enabled = false;
        // For some reason just setting .enabled to false here didn't work, so we also undo the other stuff it sets
        ___screenLevelVideoReel.clip = null;
        ___screenLevelVideoReel.gameObject.SetActive(false);
        ___screenLevelVideoReel.Stop();
    }
}
