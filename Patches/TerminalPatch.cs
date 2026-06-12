using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RouteRandomRedexed.Helpers;
using Random = System.Random;

namespace RouteRandomRedexed.Patches;

[HarmonyPatch(typeof(Terminal))]
public class TerminalPatch
{
    private static readonly TerminalNode noSuitablePlanetsNode = new() {
        name = "NoSuitablePlanets",
        displayText = "\nNo suitable planets found.\nConsider route random.\n\n\n",
        clearPreviousText = true
    };

    private static readonly TerminalNode hidePlanetHackNode = new() {
        name = "HidePlanetHack",
        displayText = "\nRouting autopilot to [REDACTED].\nYour new balance is [playerCredits].\n\nPlease enjoy your flight.\n\n\n",
        clearPreviousText = true
        // buyRerouteToMoon and itemCost fields are set on the fly before returning this node
        // Least obtrusive way I could find to hide the route chosen but still actually go there
    };

    private static TerminalKeyword routeKeyword;

    private static TerminalKeyword randomKeyword;
    private static TerminalKeyword randomFilterWeatherKeyword;

    private static CompatibleNoun routeRandomCompatibleNoun;
    private static CompatibleNoun routeRandomFilterWeatherCompatibleNoun;

    private static readonly Random rand = new();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Terminal.Awake))]
    public static void AddNewTerminalWords(Terminal __instance) {
        try {
            routeKeyword = __instance.GetKeyword("Route");

            randomKeyword = new TerminalKeyword {
                word = "random",
                name = "Random",
                defaultVerb = routeKeyword,
                compatibleNouns = Array.Empty<CompatibleNoun>()
            };
            randomFilterWeatherKeyword = new TerminalKeyword {
                word = "randomfilterweather",
                name = "RandomFilterWeather",
                defaultVerb = routeKeyword,
                compatibleNouns = Array.Empty<CompatibleNoun>()
            };

            routeRandomCompatibleNoun = new CompatibleNoun(
                randomKeyword,
                new TerminalNode {
                    name = "routeRandom",
                    buyRerouteToMoon = -1,
                    terminalOptions = Array.Empty<CompatibleNoun>()
                }
            );
            routeRandomFilterWeatherCompatibleNoun = new CompatibleNoun (
                randomFilterWeatherKeyword,
                new TerminalNode {
                    name = "routeRandomFilterWeather",
                    buyRerouteToMoon = -1,
                    terminalOptions = Array.Empty<CompatibleNoun>()
                }
            );

            TerminalKeyword moonsKeyword = __instance.GetKeyword("Moons");
            moonsKeyword.specialKeywordResult.displayText +=
                "* Random   //   Routes you to a random moon, regardless of weather conditions\n* RandomFilterWeather   //   Routes you to a random moon, filtering out disallowed weather conditions\n\n";

            __instance.AddKeywords(randomKeyword, randomFilterWeatherKeyword);
            __instance.AddCompatibleNounsToKeyword("Route", routeRandomCompatibleNoun, routeRandomFilterWeatherCompatibleNoun);
        } catch (Exception e) {
            RouteRandomRedexed.Log.LogError("Failed to add Terminal keywords and compatible nouns!");
            RouteRandomRedexed.Log.LogError(e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Terminal.ParsePlayerSentence))]
    public static TerminalNode RouteToRandomPlanet(TerminalNode __result, Terminal __instance) {
        if (__result is null || __instance is null) {
            RouteRandomRedexed.Log.LogDebug($"Terminal node was null? ({__result is null})");
            RouteRandomRedexed.Log.LogDebug($"Terminal was null? ({__instance is null})");
            return __result;
        }

        bool choseRouteRandom = __result.name == "routeRandom";
        bool choseRouteRandomFilterWeather = __result.name == "routeRandomFilterWeather";
        if (!choseRouteRandom && !choseRouteRandomFilterWeather) {
            RouteRandomRedexed.Log.LogDebug($"Didn't choose random or randomfilterweather (chose {__result.name})");
            return __result;
        }

        // .Distinct check here as Dine was registered twice for some reason? Didn't bother looking into why :P
        List<CompatibleNoun> routePlanetNodes = routeKeyword.compatibleNouns.Where(noun => noun.ResultIsRealMoon() && noun.ResultIsAffordable()).Distinct(new CompatibleNounComparer()).ToList();
        RouteRandomRedexed.Log.LogInfo($"Moons before filtering: {routePlanetNodes.Count}");

        if (choseRouteRandomFilterWeather) {
            foreach (CompatibleNoun compatibleNoun in routePlanetNodes.ToList()) {
                TerminalNode confirmNode = compatibleNoun.result.GetNodeAfterConfirmation();
                SelectableLevel moonLevel = StartOfRound.Instance.levels[confirmNode.buyRerouteToMoon];
                if (!WeatherIsAllowed(moonLevel.currentWeather)) {
                    routePlanetNodes.Remove(compatibleNoun);
                }
            }

            RouteRandomRedexed.Log.LogInfo($"Moons after filtering weather: {routePlanetNodes.Count}");
        }

        if (RouteRandomRedexed.ConfigDifferentPlanetEachTime.Value) {
            routePlanetNodes.RemoveAll(rpn => rpn.result.GetNodeAfterConfirmation().NodeRoutesToCurrentOrbitedMoon());
            RouteRandomRedexed.Log.LogInfo($"Moons after filtering orbited moon: {routePlanetNodes.Count}");
        }

        // Remove moons not in the current constellation
        if (RouteRandomRedexed.constellationsLoaded && RouteRandomRedexed.ConfigConstellationSupport.Value) {
            foreach (CompatibleNoun compatibleNoun in routePlanetNodes.ToList()) {
                TerminalNode confirmNode = compatibleNoun.result.GetNodeAfterConfirmation();
                SelectableLevel moonLevel = StartOfRound.Instance.levels[confirmNode.buyRerouteToMoon];
                if (!ConstellationsCompat.IsLevelInConstellation(moonLevel)) {
                    routePlanetNodes.Remove(compatibleNoun);
                }
            }

            RouteRandomRedexed.Log.LogInfo($"Moons after filtering constellation: {routePlanetNodes.Count}");
        }

        // Remove moons that are in the RandomRouteOnly recent levels list
        if (RouteRandomRedexed.randomRouteOnlyLoaded) {
            routePlanetNodes = RandomRouteOnlyCompat.FilterRecentLevels(routePlanetNodes);
            RouteRandomRedexed.Log.LogInfo($"Moons after filtering RandomRouteOnly recent moons list: {routePlanetNodes.Count}");
        }

        // Almost never happens, but sanity check
        if (routePlanetNodes.Count <= 0) {
            RouteRandomRedexed.Log.LogInfo("No suitable moons found D:");
            return noSuitablePlanetsNode;
        }

        // Use RandomRouteOnly moon weights for selection if enabled
        TerminalNode chosenNode;
        if(RouteRandomRedexed.randomRouteOnlyLoaded && RouteRandomRedexed.ConfigUseWeights.Value){
            chosenNode = RandomRouteOnlyCompat.GetWeightedRandom(routePlanetNodes);
        }else{
            chosenNode = rand.NextFromCollection(routePlanetNodes).result;
        }
        RouteRandomRedexed.Log.LogInfo($"Chosen moon: {chosenNode.name}");

        if (RouteRandomRedexed.ConfigRemoveCostOfCostlyPlanets.Value) {
            if (TerminalHelper.TryMakeRouteMoonNodeFree(chosenNode, out TerminalNode freeNode)) {
                chosenNode = freeNode;
            }

            RouteRandomRedexed.Log.LogInfo("Made moon free!");
        }

        if (RouteRandomRedexed.ConfigHidePlanet.Value) {
            TerminalNode confirmationNode = chosenNode.GetNodeAfterConfirmation();
            hidePlanetHackNode.buyRerouteToMoon = confirmationNode.buyRerouteToMoon;
            hidePlanetHackNode.itemCost = RouteRandomRedexed.ConfigRemoveCostOfCostlyPlanets.Value ? 0 : confirmationNode.itemCost;
            RouteRandomRedexed.Log.LogInfo("Hidden moon!");
            __instance.TrySetCurrentNode(chosenNode);
            return hidePlanetHackNode;
        }

        __instance.TrySetCurrentNode(chosenNode);
        return RouteRandomRedexed.ConfigSkipConfirmation.Value ? chosenNode.GetNodeAfterConfirmation() : chosenNode;
    }

    private static bool WeatherIsAllowed(LevelWeatherType weatherType) {
        return weatherType switch {
            LevelWeatherType.None => RouteRandomRedexed.ConfigAllowMildWeather.Value,
            LevelWeatherType.DustClouds => RouteRandomRedexed.ConfigAllowDustCloudsWeather.Value,
            LevelWeatherType.Rainy => RouteRandomRedexed.ConfigAllowRainyWeather.Value,
            LevelWeatherType.Stormy => RouteRandomRedexed.ConfigAllowStormyWeather.Value,
            LevelWeatherType.Foggy => RouteRandomRedexed.ConfigAllowFoggyWeather.Value,
            LevelWeatherType.Flooded => RouteRandomRedexed.ConfigAllowFloodedWeather.Value,
            LevelWeatherType.Eclipsed => RouteRandomRedexed.ConfigAllowEclipsedWeather.Value,
            _ => false
        };
    }
}

internal class CompatibleNounComparer : EqualityComparer<CompatibleNoun>
{
    public override bool Equals(CompatibleNoun x, CompatibleNoun y) => x?.result.name.Equals(y?.result.name, StringComparison.InvariantCultureIgnoreCase) ?? false;

    public override int GetHashCode(CompatibleNoun obj) => obj.result.GetHashCode();
}
