// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GOAPAction
{
    public string Name { get; private set; }
    public Dictionary<string, Func<NPCState, WorldState, bool>> Conditions { get; private set; }
    public Dictionary<string, object> Effects { get; private set; }
    public Dictionary<string, float> Cost { get; private set; }

    public GOAPAction(
        string name,
        Dictionary<string, Func<NPCState, WorldState, bool>> conditions,
        Dictionary<string, object> effects,
        Dictionary<string, float> cost
    )
    {
        Name = name;
        Conditions = new Dictionary<string, Func<NPCState, WorldState, bool>>(conditions);
        Effects = new Dictionary<string, object>(effects);
        Cost = new Dictionary<string, float>(cost);
    }

    public bool IsApplicable(NPCState npcState, WorldState worldState)
    {
        foreach (var condition in Conditions)
        {
            if (!condition.Value(npcState, worldState))
                return false;
        }

        foreach (var resourceCost in Cost)
        {
            if (resourceCost.Key == "time")
                continue;

            if (npcState.Resources.TryGetValue(resourceCost.Key, out float currentValue))
            {
                if (currentValue - resourceCost.Value < 0)
                    return false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    public (NPCState, WorldState) Apply(NPCState npcState, WorldState worldState)
    {
        NPCState newNpcState = npcState.Copy();
        WorldState newWorldState = worldState.Copy();

        foreach (var effect in Effects)
        {
            string key = effect.Key;
            object value = effect.Value;

            if (newNpcState.UpperBody.ContainsKey(key))
            {
                newNpcState.UpperBody[key] = value;
            }
            else if (newNpcState.LowerBody.ContainsKey(key))
            {
                newNpcState.LowerBody[key] = value;
            }
            else if (newNpcState.Resources.ContainsKey(key))
            {
                newNpcState.Resources[key] = Convert.ToSingle(value);
            }
            else if (key == "pickup_item")
            {
                string itemName = value.ToString();
                string currentLocation = newNpcState.LowerBody["location"].ToString();
                if (
                    newWorldState.Places.ContainsKey(currentLocation)
                    && newWorldState.Places[currentLocation].Inventory.Contains(itemName)
                )
                {
                    newWorldState.Places[currentLocation].Inventory.Remove(itemName);
                    newNpcState.Inventory.Add(itemName);
                }
            }
            else if (key == "drop_item")
            {
                string itemName = value.ToString();
                string currentLocation = newNpcState.LowerBody["location"].ToString();
                if (newNpcState.Inventory.Contains(itemName))
                {
                    newNpcState.Inventory.Remove(itemName);
                    newWorldState.Places[currentLocation].Inventory.Add(itemName);
                }
            }
            else if (key.StartsWith("place_state:", StringComparison.OrdinalIgnoreCase))
            {
                // Handle keys in the format "place_state:<placeName>:<stateKey>"
                var parts = key.Split(':');
                if (parts.Length == 3)
                {
                    string placeName = parts[1].ToLower();
                    string stateKey = parts[2].ToLower();

                    if (newWorldState.Places.ContainsKey(placeName))
                    {
                        newWorldState.Places[placeName].State[stateKey] = value;
                    }
                    else
                    {
                        Debug.LogWarning($"GOAPAction: Cannot find place '{placeName}'.");
                    }
                }
                else
                {
                    Debug.LogWarning($"GOAPAction: Invalid place_state key format '{key}'.");
                }
            }
            else
            {
                newNpcState.StateData[key] = value;
            }
        }

        foreach (var resourceCost in Cost)
        {
            string resource = resourceCost.Key;
            float costValue = resourceCost.Value;

            if (resource == "time")
            {
                newNpcState.Resources[resource] += costValue;
            }
            else
            {
                newNpcState.Resources[resource] -= costValue;
            }
        }

        // Generalize debug logs to avoid dependency on specific keys
        //Debug.Log($"Action '{Name}' applied. Updated World State:");
        foreach (var place in newWorldState.Places)
        {
            string placeStateInfo = string.Join(
                ", ",
                place.Value.State.Select(kvp => $"{kvp.Key}:{kvp.Value}")
            );
            //Debug.Log($" - {place.Key}: {placeStateInfo}");
        }

        return (newNpcState, newWorldState);
    }

    private void ApplyUseItem(ref NPCState npcState, ref WorldState worldState, string itemName)
    {
        if (npcState.Inventory.Contains(itemName))
        {
            npcState.Inventory.Remove(itemName);
            if (worldState.Items.ContainsKey(itemName))
            {
                var item = worldState.Items[itemName];
                if (item.Behaviors.ContainsKey("use"))
                {
                    var behavior = item.Behaviors["use"];
                    foreach (var effect in (Dictionary<string, object>)behavior["effects"])
                    {
                        if (npcState.Resources.ContainsKey(effect.Key))
                        {
                            npcState.Resources[effect.Key] += Convert.ToSingle(effect.Value);
                        }
                        else
                        {
                            npcState.StateData[effect.Key] = effect.Value;
                        }
                    }
                }
            }
            npcState.StateData[$"used_{itemName}"] = true;
            Debug.Log($"Item '{itemName}' used. State Data: {npcState.StateData}");
        }
    }

    public override string ToString()
    {
        return $"GOAPAction(Name={Name}, Conditions=[{string.Join(", ", Conditions.Keys)}], Effects=[{string.Join(", ", Effects.Keys)}], Cost=[{string.Join(", ", Cost.Keys)}])";
    }
}
