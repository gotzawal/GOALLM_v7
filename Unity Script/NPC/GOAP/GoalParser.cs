// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq; // Added for LINQ usage
using System.Text.RegularExpressions;
using UnityEngine; // Added for Debug.Log usage

public static class GoalParser
{
    /// <summary>
    /// Analyzes the given sentence and converts it to a GOAP goal.
    /// </summary>
    /// <param name="sentence">Sentence to analyze</param>
    /// <param name="actions">List of available GOAP actions</param>
    /// <param name="worldState">Current world state</param>
    /// <param name="weight">Importance of the goal</param>
    /// <returns>Generated Goal object or null</returns>
    public static Goal ParseSentenceToGoal(
        string sentence,
        List<GOAPAction> actions,
        WorldState worldState,
        float weight = 1f
    )
    {
        if (string.IsNullOrWhiteSpace(sentence) || sentence.Trim().ToLower() == "none")
            return null;

        // Preprocess sentence: trim and remove period
        sentence = sentence.Trim().TrimEnd('.');

        // 1. Remove articles ("the", "a", "an")
        string[] articles = { "the", "a", "an" };
        foreach (var article in articles)
        {
            // Use word boundary (\b) to remove exact words
            sentence = Regex.Replace(sentence, $@"\b{article}\b\s*", "", RegexOptions.IgnoreCase);
        }

        // 2. Check for "Use <item> at <location>" pattern
        var matchUseAt = Regex.Match(
            sentence,
            @"Use\s+(.+?)\s+(in|on|at)\s+(.+)",
            RegexOptions.IgnoreCase
        );
        if (matchUseAt.Success)
        {
            string itemName = matchUseAt.Groups[1].Value.Trim().ToLower();
            string preposition = matchUseAt.Groups[2].Value.Trim().ToLower();
            string location = matchUseAt.Groups[3].Value.Trim().ToLower();
            string goalName = $"Use_{itemName}_at_{location}";

            // Check if the item has 'use' functionality
            if (
                !worldState.Items.ContainsKey(itemName)
                || !worldState.Items[itemName].UseEffects.Any()
            )
            {
                Debug.LogError($"Item '{itemName}' does not have use functionality.");
                return null;
            }

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                // Check 'used_snack=true' state
                return npcState.StateData.TryGetValue($"used_{itemName}", out var usedValue)
                    && Convert.ToBoolean(usedValue);
            };

            // Include use effects in Goal's Effect
            var effects = new Dictionary<string, object>(
                worldState.Items[itemName].UseEffects,
                StringComparer.OrdinalIgnoreCase
            );

            return new Goal(goalName, condition, weight, effects);
        }

        // 3. Check for "Use <item>" pattern
        var matchUse = Regex.Match(sentence, @"Use\s+(.+)", RegexOptions.IgnoreCase);
        if (matchUse.Success)
        {
            string itemName = matchUse.Groups[1].Value.Trim().ToLower();
            string goalName = $"Use_{itemName}";

            // Check if the item has 'use' functionality
            if (
                !worldState.Items.ContainsKey(itemName)
                || !worldState.Items[itemName].UseEffects.Any()
            )
            {
                Debug.LogError($"Item '{itemName}' does not have use functionality.");
                return null;
            }

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                // Check 'used_snack=true' state
                return npcState.StateData.TryGetValue($"used_{itemName}", out var usedValue)
                    && Convert.ToBoolean(usedValue);
            };

            // Include use effects in Goal's Effect
            var effects = new Dictionary<string, object>(
                worldState.Items[itemName].UseEffects,
                StringComparer.OrdinalIgnoreCase
            );

            return new Goal(goalName, condition, weight, effects);
        }

        // 4. Check for "Do <action> in/on <location>" pattern
        var matchActionAtLocation = Regex.Match(
            sentence,
            @"Do\s+(.+?)\s+(in|on)\s+(.+)",
            RegexOptions.IgnoreCase
        );
        if (matchActionAtLocation.Success)
        {
            string actionName = matchActionAtLocation.Groups[1].Value.Trim().ToLower();
            string location = matchActionAtLocation.Groups[3].Value.Trim().ToLower();
            string goalName = $"Do_{actionName}_at_{location}";

            var action = actions.Find(a =>
                a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase)
            );
            if (action != null)
            {
                Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
                {
                    if (
                        !npcState.LowerBody.ContainsKey("location")
                        || !npcState
                            .LowerBody["location"]
                            .ToString()
                            .Equals(location, StringComparison.OrdinalIgnoreCase)
                    )
                        return false;

                    foreach (var effect in action.Effects)
                    {
                        if (!CheckEffectApplied(effect.Key, effect.Value, npcState, ws))
                            return false;
                    }
                    return true;
                };

                return new Goal(
                    goalName,
                    condition,
                    weight,
                    new Dictionary<string, object>(action.Effects, StringComparer.OrdinalIgnoreCase)
                );
            }
            else
            {
                Debug.LogError($"Action '{actionName}' not found.");
                return null;
            }
        }

        // 6. Check for "Change <state> of <object> to <value>" pattern
        var matchChangeState = Regex.Match(
            sentence,
            @"Change\s+(.+?)\s+of\s+(.+?)\s+to\s+(.+)",
            RegexOptions.IgnoreCase
        );
        if (matchChangeState.Success)
        {
            string stateKey = matchChangeState.Groups[1].Value.Trim().ToLower();
            string objName = matchChangeState.Groups[2].Value.Trim().ToLower();
            string desiredValue = matchChangeState.Groups[3].Value.Trim().ToLower();
            string goalName = $"Change_{stateKey}_of_{objName}_to_{desiredValue}";

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                return CheckState(objName, stateKey, desiredValue, npcState, ws);
            };

            return new Goal(goalName, condition, weight);
        }

        // 6. Check for "Go to <location>" pattern
        var matchGoToLocation = Regex.Match(sentence, @"Go\s+to\s+(.+)", RegexOptions.IgnoreCase);
        if (matchGoToLocation.Success)
        {
            string location = matchGoToLocation.Groups[1].Value.Trim().ToLower();
            string goalName = $"Go_to_{location}";

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                return npcState.LowerBody.ContainsKey("location")
                    && npcState
                        .LowerBody["location"]
                        .ToString()
                        .Equals(location, StringComparison.OrdinalIgnoreCase);
            };

            return new Goal(goalName, condition, weight);
        }

        // 7. Check for "Pick up <item> at <place>" pattern
        var matchPickUpAt = Regex.Match(
            sentence,
            @"Pick\s+up\s+(.+?)\s+at\s+(.+)",
            RegexOptions.IgnoreCase
        );
        if (matchPickUpAt.Success)
        {
            string itemName = matchPickUpAt.Groups[1].Value.Trim().ToLower();
            string placeName = matchPickUpAt.Groups[2].Value.Trim().ToLower();
            string goalName = $"Pick_up_{itemName}_at_{placeName}";

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                // Goal is achieved when NPC has the item (case insensitive)
                return npcState.Inventory.Any(i =>
                    i.Equals(itemName, StringComparison.OrdinalIgnoreCase)
                );
            };

            return new Goal(goalName, condition, weight);
        }

        // 8. Check for "Pick up <item>" pattern
        var matchPickUp = Regex.Match(sentence, @"Pick\s+up\s+(.+)", RegexOptions.IgnoreCase);
        if (matchPickUp.Success)
        {
            string itemName = matchPickUp.Groups[1].Value.Trim().ToLower();
            string goalName = $"Pick_up_{itemName}";

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                // Goal is achieved when NPC has the item (case insensitive)
                return npcState.Inventory.Any(i =>
                    i.Equals(itemName, StringComparison.OrdinalIgnoreCase)
                );
            };

            return new Goal(goalName, condition, weight);
        }

        // 9. Check for "Drop <item> at <location>" pattern
        var matchDropItem = Regex.Match(
            sentence,
            @"Drop\s+(.+?)\s+at\s+(.+)",
            RegexOptions.IgnoreCase
        );
        if (matchDropItem.Success)
        {
            string itemName = matchDropItem.Groups[1].Value.Trim().ToLower();
            string location = matchDropItem.Groups[2].Value.Trim().ToLower();
            string goalName = $"Drop_{itemName}_at_{location}";

            Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
            {
                if (
                    !npcState.LowerBody.ContainsKey("location")
                    || !npcState
                        .LowerBody["location"]
                        .ToString()
                        .Equals(location, StringComparison.OrdinalIgnoreCase)
                )
                    return false;

                if (
                    !npcState.Inventory.Any(i =>
                        i.Equals(itemName, StringComparison.OrdinalIgnoreCase)
                    )
                )
                    return false;

                if (!ws.Places.ContainsKey(location))
                    return false;

                return ws.Places[location]
                    .Inventory.Any(i => i.Equals(itemName, StringComparison.OrdinalIgnoreCase));
            };

            return new Goal(goalName, condition, weight);
        }

        // 10. Check for "Do <action>" pattern
        var matchAction = Regex.Match(sentence, @"Do\s+(.+)", RegexOptions.IgnoreCase);
        if (matchAction.Success)
        {
            string actionName = matchAction.Groups[1].Value.Trim().ToLower();
            string goalName = $"Do_{actionName}";

            var action = actions.Find(a =>
                a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase)
            );
            if (action != null)
            {
                Func<NPCState, WorldState, bool> condition = (npcState, ws) =>
                {
                    if (action.Effects.Count == 0)
                        return npcState.StateData.ContainsKey($"did_{actionName}")
                            && Convert.ToBoolean(npcState.StateData[$"did_{actionName}"]);

                    foreach (var effect in action.Effects)
                    {
                        if (!CheckEffectApplied(effect.Key, effect.Value, npcState, ws))
                            return false;
                    }
                    return true;
                };

                return new Goal(
                    goalName,
                    condition,
                    weight,
                    new Dictionary<string, object>(action.Effects, StringComparer.OrdinalIgnoreCase)
                );
            }
            else
            {
                Debug.LogError($"Action '{actionName}' not found.");
                return null;
            }
        }

        Debug.Log("Sentence parsing failed.");
        return null;
    }

    /// <summary>
    /// Checks if the given effect has been applied to the NPC state.
    /// </summary>
    private static bool CheckEffectApplied(
        string effectKey,
        object effectValue,
        NPCState npcState,
        WorldState worldState
    )
    {
        bool result = false;
        if (npcState.UpperBody.TryGetValue(effectKey, out var upperValue))
        {
            result = upperValue
                .ToString()
                .Equals(effectValue.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        else if (npcState.LowerBody.TryGetValue(effectKey, out var lowerValue))
        {
            result = lowerValue
                .ToString()
                .Equals(effectValue.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        else if (npcState.Resources.TryGetValue(effectKey, out var resourceValue))
        {
            if (float.TryParse(effectValue.ToString(), out float floatValue))
            {
                result = Math.Abs(resourceValue - floatValue) < 0.001f;
            }
        }
        else if (effectKey.StartsWith("place_state:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = effectKey.Split(':');
            if (parts.Length == 3)
            {
                string placeName = parts[1].ToLower();
                string stateKey = parts[2].ToLower();
                if (worldState.Places.TryGetValue(placeName, out var place))
                {
                    result =
                        place.State.TryGetValue(stateKey, out var placeStateValue)
                        && placeStateValue
                            .ToString()
                            .Equals(effectValue.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        else if (effectKey.StartsWith("item_state:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = effectKey.Split(':');
            if (parts.Length == 3)
            {
                string itemName = parts[1].ToLower();
                string stateKey = parts[2].ToLower();
                if (worldState.Items.TryGetValue(itemName, out var item))
                {
                    result =
                        item.State.TryGetValue(stateKey, out var itemStateValue)
                        && itemStateValue
                            .ToString()
                            .Equals(effectValue.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        else if (effectKey.Equals("pose", StringComparison.OrdinalIgnoreCase))
        {
            result =
                npcState.LowerBody.TryGetValue("pose", out var poseValue)
                && poseValue
                    .ToString()
                    .Equals(effectValue.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        else if (effectKey.Equals("use_item", StringComparison.OrdinalIgnoreCase))
        {
            string usedItemKey = $"used_{effectValue.ToString().ToLower()}";
            result =
                npcState.StateData.TryGetValue(usedItemKey, out var usedValue)
                && Convert.ToBoolean(usedValue);
        }
        else if (effectKey.Equals("pickup_item", StringComparison.OrdinalIgnoreCase))
        {
            string itemName = effectValue.ToString().ToLower();
            result = npcState.Inventory.Any(i =>
                i.Equals(itemName, StringComparison.OrdinalIgnoreCase)
            );
        }
        else if (effectKey.Equals("drop_item", StringComparison.OrdinalIgnoreCase))
        {
            string itemName = effectValue.ToString().ToLower();
            string currentLocation = npcState.LowerBody.TryGetValue("location", out var loc)
                ? loc.ToString().ToLower()
                : "";
            if (worldState.Places.TryGetValue(currentLocation, out var place))
            {
                result = place.Inventory.Any(i =>
                    i.Equals(itemName, StringComparison.OrdinalIgnoreCase)
                );
            }
        }
        else
        {
            result =
                npcState.StateData.TryGetValue(effectKey, out var stateValue)
                && stateValue
                    .ToString()
                    .Equals(effectValue.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        //Debug.Log($"Effect '{effectKey}': '{effectValue}' check result: {result}");
        return result;
    }

    /// <summary>
    /// Checks if the state of the given object has been changed to the desired value.
    /// </summary>
    private static bool CheckState(
        string objName,
        string stateKey,
        string desiredValue,
        NPCState npcState,
        WorldState worldState
    )
    {
        if (objName.Equals("NPC", StringComparison.OrdinalIgnoreCase))
        {
            if (npcState.UpperBody.TryGetValue(stateKey, out var upperValue))
            {
                return upperValue
                    .ToString()
                    .Equals(desiredValue, StringComparison.OrdinalIgnoreCase);
            }
            else if (npcState.LowerBody.TryGetValue(stateKey, out var lowerValue))
            {
                return lowerValue
                    .ToString()
                    .Equals(desiredValue, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return npcState.StateData.TryGetValue(stateKey, out var stateDataValue)
                    && stateDataValue
                        .ToString()
                        .Equals(desiredValue, StringComparison.OrdinalIgnoreCase);
            }
        }
        else if (worldState.Places.TryGetValue(objName, out var place))
        {
            return place.State.TryGetValue(stateKey, out var placeStateValue)
                && placeStateValue
                    .ToString()
                    .Equals(desiredValue, StringComparison.OrdinalIgnoreCase);
        }
        else if (worldState.Items.TryGetValue(objName, out var item))
        {
            return item.State.TryGetValue(stateKey, out var itemStateValue)
                && itemStateValue
                    .ToString()
                    .Equals(desiredValue, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            return false;
        }
    }
}
