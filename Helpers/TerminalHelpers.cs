using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace RouteRandomRedexed.Helpers;

public static class TerminalHelper
{
    private static readonly string[] currentNodeFieldNames = {
        "currentNode",
        "currentNodeInTerminal",
        "currentTerminalNode"
    };

    private static FieldInfo? currentNodeField;
    private static PropertyInfo? currentNodeProperty;
    private static bool currentNodeResolved;
    private static bool currentNodeWarningLogged;

    public static TerminalKeyword GetKeyword(this Terminal terminal, string keywordName) => terminal.terminalNodes.allKeywords.First(kw => kw.name == keywordName);

    public static TerminalNode GetNodeAfterConfirmation(this TerminalNode node) => node.terminalOptions.First(cn => cn.noun.name == "Confirm").result;

    public static bool NodeRoutesToCurrentOrbitedMoon(this TerminalNode node) => StartOfRound.Instance.levels[node.buyRerouteToMoon] == StartOfRound.Instance.currentLevel;

    public static void AddKeyword(this Terminal terminal, TerminalKeyword newKeyword) => terminal.terminalNodes.allKeywords = terminal.terminalNodes.allKeywords.AddToArray(newKeyword);

    public static void AddKeywords(this Terminal terminal, params TerminalKeyword[] newKeywords) {
        foreach (TerminalKeyword newKeyword in newKeywords) {
            terminal.AddKeyword(newKeyword);
        }
    }

    public static void AddCompatibleNounToKeyword(this Terminal terminal, string keywordName, CompatibleNoun newCompatibleNoun) {
        TerminalKeyword keyword = terminal.terminalNodes.allKeywords.FirstOrDefault(kw => kw.name == keywordName) ?? throw new ArgumentException($"Failed to find keyword with name {keywordName}");
        keyword.compatibleNouns = keyword.compatibleNouns.AddToArray(newCompatibleNoun);
    }

    public static void AddCompatibleNounsToKeyword(this Terminal terminal, string keywordName, params CompatibleNoun[] newCompatibleNouns) {
        foreach (CompatibleNoun newCompatibleNoun in newCompatibleNouns) {
            terminal.AddCompatibleNounToKeyword(keywordName, newCompatibleNoun);
        }
    }

    public static bool ResultIsRealMoon(this CompatibleNoun compatibleNoun) => compatibleNoun.result.buyRerouteToMoon == -2;

    public static bool ResultIsAffordable(this CompatibleNoun compatibleNoun) =>
        compatibleNoun.result.itemCost <= 0 || RouteRandomRedexed.ConfigAllowCostlyPlanets.Value || RouteRandomRedexed.ConfigRemoveCostOfCostlyPlanets.Value;

    public static bool TrySetCurrentNode(this Terminal terminal, TerminalNode node) {
        if (terminal is null || node is null) {
            return false;
        }

        if (!currentNodeResolved) {
            currentNodeResolved = true;

            // Try fields
            foreach (string fieldName in currentNodeFieldNames) {
                FieldInfo candidate = AccessTools.Field(typeof(Terminal), fieldName);
                if (candidate != null && typeof(TerminalNode).IsAssignableFrom(candidate.FieldType)) {
                    currentNodeField = candidate;
                    candidate.SetValue(terminal, node);
                    return true;
                }
            }

            // Try properties
            foreach (string fieldName in currentNodeFieldNames) {
                PropertyInfo candidate = AccessTools.Property(typeof(Terminal), fieldName);
                if (candidate != null && candidate.CanWrite && typeof(TerminalNode).IsAssignableFrom(candidate.PropertyType)) {
                    currentNodeProperty = candidate;
                    candidate.SetValue(terminal, node);
                    return true;
                }
            }

            // Field fallback
            currentNodeField = typeof(Terminal)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(foundField => typeof(TerminalNode).IsAssignableFrom(foundField.FieldType) && foundField.Name.Contains("current", StringComparison.InvariantCultureIgnoreCase));

            if (currentNodeField != null) {
                currentNodeField.SetValue(terminal, node);
                return true;
            }

            // Property fallback
            currentNodeProperty = typeof(Terminal)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(foundProperty => foundProperty.CanWrite && typeof(TerminalNode).IsAssignableFrom(foundProperty.PropertyType) && foundProperty.Name.Contains("current", StringComparison.InvariantCultureIgnoreCase));

            if (currentNodeProperty != null) {
                currentNodeProperty.SetValue(terminal, node);
                return true;
            }

            if (!currentNodeWarningLogged) {
                currentNodeWarningLogged = true;
                RouteRandomRedexed.Log.LogWarning("Failed to find Terminal current node field/property for state sync.");
            }

            return false;
        }

        if (currentNodeField != null) {
            currentNodeField.SetValue(terminal, node);
            return true;
        }

        if (currentNodeProperty != null) {
            currentNodeProperty.SetValue(terminal, node);
            return true;
        }

        return false;
    }

    public static bool TryMakeRouteMoonNodeFree(TerminalNode routeMoonNode, out TerminalNode freeMoonNode) {
        CompatibleNoun confirmCompatibleNoun = routeMoonNode.terminalOptions.FirstOrDefault(node => node.noun.name == "Confirm");
        CompatibleNoun denyCompatibleNoun = routeMoonNode.terminalOptions.FirstOrDefault(node => node.noun.name == "Deny");
        if (confirmCompatibleNoun == null || denyCompatibleNoun == null) {
            freeMoonNode = null;
            return false;
        }

        TerminalNode freeConfirmNode = new() {
            name = $"{confirmCompatibleNoun.result.name}Free",
            buyRerouteToMoon = confirmCompatibleNoun.result.buyRerouteToMoon,
            clearPreviousText = true,
            displayText = confirmCompatibleNoun.result.displayText,
            itemCost = 0
        };

        freeMoonNode = new TerminalNode {
            name = $"{routeMoonNode.name}Free",
            buyRerouteToMoon = -2,
            clearPreviousText = true,
            displayPlanetInfo = routeMoonNode.displayPlanetInfo,
            displayText = routeMoonNode.displayText,
            itemCost = 0,
            overrideOptions = true,
            terminalOptions = new[] {
                denyCompatibleNoun, new CompatibleNoun (confirmCompatibleNoun.noun, freeConfirmNode)
            }
        };
        return true;
    }
}
