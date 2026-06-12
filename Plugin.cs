using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace RouteRandomRedexed;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class RouteRandomRedexed : BaseUnityPlugin
{
    public static RouteRandomRedexed Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    public static bool constellationsLoaded = false;
    public static bool randomRouteOnlyLoaded = false;
    public static bool lethalLevelLoaderLoaded = false;

    private void Awake() {
        Log = base.Logger;
        Instance = this;

        LoadConfigs();

        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);
        Harmony.PatchAll();
        
        Log.LogInfo($"{MyPluginInfo.PLUGIN_GUID} has loaded!");
    }

    internal static void Unpatch()
    {
        Log.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Log.LogDebug("Finished unpatching!");
    }

    #region Config

    public static ConfigEntry<bool> ConfigAllowMildWeather;
    public static ConfigEntry<bool> ConfigAllowDustCloudsWeather;
    public static ConfigEntry<bool> ConfigAllowRainyWeather;
    public static ConfigEntry<bool> ConfigAllowStormyWeather;
    public static ConfigEntry<bool> ConfigAllowFoggyWeather;
    public static ConfigEntry<bool> ConfigAllowFloodedWeather;
    public static ConfigEntry<bool> ConfigAllowEclipsedWeather;
    public static ConfigEntry<bool> ConfigAllowCostlyPlanets;
    public static ConfigEntry<bool> ConfigRemoveCostOfCostlyPlanets;
    public static ConfigEntry<bool> ConfigSkipConfirmation;
    public static ConfigEntry<bool> ConfigDifferentPlanetEachTime;
    public static ConfigEntry<bool> ConfigHidePlanet;
    public static ConfigEntry<bool> ConfigConstellationSupport;
    public static ConfigEntry<bool> ConfigUseWeights;
    public static ConfigEntry<bool> ConfigBypassLLLLock;

    private void LoadConfigs() {
        ConfigAllowMildWeather = Config.Bind("Allowed Weathers",
            "AllowMildWeather",
            true,
            "Whether or not to allow the 'Mild' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowDustCloudsWeather = Config.Bind("Allowed Weathers",
            "AllowDustCloudsWeather",
            false,
            "Whether or not to allow the 'Dust Clouds' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowRainyWeather = Config.Bind("Allowed Weathers",
            "AllowRainyWeather",
            false,
            "Whether or not to allow the 'Rainy' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowStormyWeather = Config.Bind("Allowed Weathers",
            "AllowStormyWeather",
            false,
            "Whether or not to allow the 'Stormy' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowFoggyWeather = Config.Bind("Allowed Weathers",
            "AllowFoggyWeather",
            false,
            "Whether or not to allow the 'Foggy' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowFloodedWeather = Config.Bind("Allowed Weathers",
            "AllowFloodedWeather",
            false,
            "Whether or not to allow the 'Flooded' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowEclipsedWeather = Config.Bind("Allowed Weathers",
            "AllowEclipsedWeather",
            false,
            "Whether or not to allow the 'Eclipsed' weather to be chosen by the 'route randomfilterweather' command");

        ConfigAllowCostlyPlanets = Config.Bind("Costly Planets",
            "AllowCostlyPlanets",
            false,
            "Whether or not to allow costly planets (85-Rend, 7-Dine, 8-Titan). NOTE: You will still be prompted to pay the fee to fly there, enable the MakeCostlyPlanetsFree option to avoid that");

        ConfigRemoveCostOfCostlyPlanets = Config.Bind("Costly Planets",
            "RemoveCostOfCostlyPlanets",
            false,
            "Whether or not to remove the cost of costly planets when they're chosen randomly and allows them to be chosen even when AllowCostlyPlanets is false");

        ConfigSkipConfirmation = Config.Bind("General",
            "SkipConfirmation",
            false,
            "Whether or not to skip the confirmation screen when using 'route random' or 'route randomwithweather' commands");

        ConfigDifferentPlanetEachTime = Config.Bind("General",
            "DifferentPlanetEachTime",
            false,
            "Prevents 'route random' and 'route randomwithweather' commands from choosing the same planet you're on");

        ConfigHidePlanet = Config.Bind("General",
            "HidePlanet",
            false,
            "Hides the planet you get randomly routed to, both in the terminal response and at the helm. NOTE: This will ALWAYS hide the orbited planet (even when selected manually) and will skip the confirmation screen");

        ConfigConstellationSupport = Config.Bind("General",
            "LethalConstellationsSupport",
            true,
            "Turns on compatibility logic for the mod LethalConstellations. Route random will only select moons from the current constellation if enabled");

        ConfigUseWeights = Config.Bind("General",
            "Use RandomRouteOnly moon weights",
            true,
            "Whether to use the moon weights configured in RandomRouteOnly's config for random moon selection");

        ConfigBypassLLLLock = Config.Bind("General",
            "BypassLethalLevelLoaderLock",
            true,
            "Bypasses LethalLevelLoader's moon lock when using 'route random' or 'route randomfilterweather' commands. Enable this if you want to route to ALL moons regardless of their lock status.");
    }

    #endregion
}
