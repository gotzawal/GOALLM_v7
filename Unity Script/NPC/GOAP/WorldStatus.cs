// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;

[Serializable]
public class WorldStatus
{
    public Dictionary<string, SerializablePlace> Places { get; set; }
    public Dictionary<string, SerializableItem> Items { get; set; }

    public WorldStatus(WorldState worldState)
    {
        Places = new Dictionary<string, SerializablePlace>();
        foreach (var kvp in worldState.Places)
        {
            Places[kvp.Key] = new SerializablePlace(kvp.Value);
        }

        Items = new Dictionary<string, SerializableItem>();
        foreach (var kvp in worldState.Items)
        {
            Items[kvp.Key] = new SerializableItem(kvp.Value);
        }
    }
}

[Serializable]
public class SerializablePlace
{
    public string Name { get; set; }
    public List<string> Inventory { get; set; }
    public Dictionary<string, object> State { get; set; }

    public SerializablePlace(Place place)
    {
        Name = place.Name;
        Inventory = new List<string>(place.Inventory);
        State = new Dictionary<string, object>(place.State);
    }
}

[Serializable]
public class SerializableItem
{
    public string Name { get; set; }

    // Add other serializable fields as needed.

    public SerializableItem(Item item)
    {
        Name = item.Name;
        // Initialize other fields
    }
}
