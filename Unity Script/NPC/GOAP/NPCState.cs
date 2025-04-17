// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // added part

public class NPCState
{
    public Dictionary<string, object> UpperBody { get; private set; }
    public Dictionary<string, object> LowerBody { get; private set; }
    public Dictionary<string, float> Resources { get; private set; }
    public List<string> Inventory { get; private set; }
    public GameObject GameObject { get; set; } // added part
    public Dictionary<string, object> StateData { get; private set; }

    public NPCState(
        Dictionary<string, object> upperBody,
        Dictionary<string, object> lowerBody,
        Dictionary<string, float> resources,
        List<string> inventory = null,
        Dictionary<string, object> stateData = null
    )
    {
        UpperBody = new Dictionary<string, object>(upperBody);
        LowerBody = new Dictionary<string, object>(lowerBody);
        Resources = new Dictionary<string, float>(resources);
        Inventory = inventory != null ? new List<string>(inventory) : new List<string>();
        StateData =
            stateData != null
                ? new Dictionary<string, object>(stateData)
                : new Dictionary<string, object>();
    }

    public NPCState Copy()
    {
        var roundedResources = Resources.ToDictionary(
            kvp => kvp.Key,
            kvp => (float)Math.Round(kvp.Value, 2)
        );
        return new NPCState(
            new Dictionary<string, object>(UpperBody),
            new Dictionary<string, object>(LowerBody),
            new Dictionary<string, float>(roundedResources),
            new List<string>(Inventory),
            new Dictionary<string, object>(StateData)
        );
    }

    public override bool Equals(object obj)
    {
        if (obj is NPCState other)
        {
            var thisResources = Resources
                .Where(kvp => kvp.Key != "time" && kvp.Key != "health" && kvp.Key != "mental")
                .OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var otherResources = other
                .Resources.Where(kvp =>
                    kvp.Key != "time" && kvp.Key != "health" && kvp.Key != "mental"
                )
                .OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return UpperBody
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual(other.UpperBody.OrderBy(kvp => kvp.Key))
                && LowerBody
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual(other.LowerBody.OrderBy(kvp => kvp.Key))
                && thisResources
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual(otherResources.OrderBy(kvp => kvp.Key))
                && Inventory.SequenceEqual(other.Inventory)
                && StateData
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual(other.StateData.OrderBy(kvp => kvp.Key));
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var kvp in UpperBody.OrderBy(kvp => kvp.Key))
            hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
        foreach (var kvp in LowerBody.OrderBy(kvp => kvp.Key))
            hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
        foreach (
            var kvp in Resources
                .Where(kvp => kvp.Key != "time" && kvp.Key != "health" && kvp.Key != "mental")
                .OrderBy(kvp => kvp.Key)
        )
            hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
        foreach (var item in Inventory.OrderBy(i => i))
            hash ^= item.GetHashCode();
        foreach (var kvp in StateData.OrderBy(kvp => kvp.Key))
            hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        return $"NPCState(UpperBody={UpperBody}, LowerBody={LowerBody}, Resources={Resources}, Inventory=[{string.Join(", ", Inventory)}], StateData={StateData})";
    }
}
