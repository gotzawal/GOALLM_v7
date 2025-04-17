// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;

public class Item
{
    public string Name { get; private set; }
    public Dictionary<string, Dictionary<string, object>> Behaviors { get; private set; }
    public Dictionary<string, object> State { get; private set; }

    public Item(
        string name,
        Dictionary<string, Dictionary<string, object>> behaviors,
        Dictionary<string, object> state = null
    )
    {
        Name = name;
        Behaviors = new Dictionary<string, Dictionary<string, object>>(
            behaviors,
            StringComparer.OrdinalIgnoreCase
        );
        State =
            state != null
                ? new Dictionary<string, object>(state, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Retrieves the effects associated with the 'use' behavior.
    /// </summary>
    public Dictionary<string, object> UseEffects
    {
        get
        {
            if (Behaviors.TryGetValue("use", out var effects))
                return new Dictionary<string, object>(effects, StringComparer.OrdinalIgnoreCase);
            else
                return new Dictionary<string, object>();
        }
    }

    public Item Copy()
    {
        return new Item(
            Name,
            new Dictionary<string, Dictionary<string, object>>(
                Behaviors,
                StringComparer.OrdinalIgnoreCase
            ),
            new Dictionary<string, object>(State, StringComparer.OrdinalIgnoreCase)
        );
    }

    public override bool Equals(object obj)
    {
        if (obj is Item other)
        {
            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase)
                && Behaviors.OrderBy(k => k.Key).SequenceEqual(other.Behaviors.OrderBy(k => k.Key))
                && State.OrderBy(k => k.Key).SequenceEqual(other.State.OrderBy(k => k.Key));
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = Name.ToLower().GetHashCode();
        foreach (var behavior in Behaviors.OrderBy(b => b.Key))
            hash ^= Helpers.MakeHashable(behavior.Value).GetHashCode();
        foreach (var kvp in State.OrderBy(k => k.Key))
            hash ^= Helpers.MakeHashable(kvp.Value).GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        return $"Item(Name={Name}, Behaviors={Behaviors}, State={State})";
    }
}
