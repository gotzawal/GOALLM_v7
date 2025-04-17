// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;

public class WorldState
{
    public Dictionary<string, Place> Places { get; set; }
    public Dictionary<string, Item> Items { get; set; }

    public WorldState(Dictionary<string, Place> places, Dictionary<string, Item> items)
    {
        Places = new Dictionary<string, Place>(places);
        Items = new Dictionary<string, Item>(items);
    }

    public WorldState Copy()
    {
        var placesCopy = Places.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Copy());
        var itemsCopy = Items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Copy());
        return new WorldState(placesCopy, itemsCopy);
    }

    public override bool Equals(object obj)
    {
        if (obj is WorldState other)
        {
            return Places
                    .OrderBy(kvp => kvp.Key)
                    .SequenceEqual(other.Places.OrderBy(kvp => kvp.Key))
                && Items.OrderBy(kvp => kvp.Key).SequenceEqual(other.Items.OrderBy(kvp => kvp.Key));
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var kvp in Places.OrderBy(kvp => kvp.Key))
            hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
        foreach (var kvp in Items.OrderBy(kvp => kvp.Key))
            hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        return $"WorldState(Places={Places}, Items={Items})";
    }
}
