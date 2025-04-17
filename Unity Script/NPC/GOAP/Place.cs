// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // Added part

public class Place
{
    public string Name { get; private set; }
    public List<string> Inventory { get; private set; }

    public GameObject GameObject { get; private set; } // Added part
    public Dictionary<string, object> State { get; private set; }


    // 새로 추가: CanSit 프로퍼티
    public bool CanSit { get; set; }
    public Place(
        string name,
        GameObject gameObject,
        List<string> inventory = null,
        Dictionary<string, object> state = null
    )
    {
        Name = name;
        Inventory = inventory != null ? new List<string>(inventory) : new List<string>();
        GameObject = gameObject;
        CanSit = false; // 기본값 false; 원하는 경우 생성자에 인수로 전달하거나 Setter를 통해 변경

        State =
            state != null
                ? new Dictionary<string, object>(state)
                : new Dictionary<string, object>();
    }

    public Place Copy()
    {
        return new Place(
            Name,
            GameObject,
            new List<string>(Inventory),
            new Dictionary<string, object>(State)
        );
    }

    public override bool Equals(object obj)
    {
        if (obj is Place other)
        {
            return Name == other.Name
                && Inventory.SequenceEqual(other.Inventory)
                && State.OrderBy(k => k.Key).SequenceEqual(other.State.OrderBy(k => k.Key));
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = Name.GetHashCode();
        foreach (var item in Inventory.OrderBy(i => i))
            hash ^= item.GetHashCode();
        foreach (var kvp in State.OrderBy(k => k.Key))
            hash ^= Helpers.MakeHashable(kvp.Value).GetHashCode();
        return hash;
    }

    public override string ToString()
    {
        return $"Place(Name={Name}, Inventory=[{string.Join(", ", Inventory)}], State={State})";
    }
}
