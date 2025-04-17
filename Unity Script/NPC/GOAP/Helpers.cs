// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // added part

public static class Helpers
{
    public static string MakeHashable(object obj)
    {
        if (obj is Dictionary<string, object> dict)
        {
            return "{"
                + string.Join(
                    ",",
                    dict.OrderBy(kvp => kvp.Key)
                        .Select(kvp => $"{kvp.Key}:{MakeHashable(kvp.Value)}")
                )
                + "}";
        }
        else if (obj is Dictionary<string, Place> placeDict)
        {
            return "{"
                + string.Join(
                    ",",
                    placeDict
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => $"{kvp.Key}:{MakeHashable(kvp.Value)}")
                )
                + "}";
        }
        else if (obj is Dictionary<string, Item> itemDict)
        {
            return "{"
                + string.Join(
                    ",",
                    itemDict
                        .OrderBy(kvp => kvp.Key)
                        .Select(kvp => $"{kvp.Key}:{MakeHashable(kvp.Value)}")
                )
                + "}";
        }
        else if (obj is List<object> list)
        {
            return "[" + string.Join(",", list.Select(x => MakeHashable(x))) + "]";
        }
        else if (obj is List<string> strList)
        {
            return "[" + string.Join(",", strList.OrderBy(s => s)) + "]";
        }
        else if (obj is HashSet<object> set)
        {
            return "{" + string.Join(",", set.Select(x => MakeHashable(x))) + "}";
        }
        else if (obj is Place place)
        {
            return $"Place(Name={place.Name},Inventory={MakeHashable(place.Inventory)},State={MakeHashable(place.State)})";
        }
        else if (obj is Item item)
        {
            return $"Item(Name={item.Name},Behaviors={MakeHashable(item.Behaviors)},State={MakeHashable(item.State)})";
        }
        else if (obj is string || obj is int || obj is float || obj is double || obj is bool)
        {
            return obj.ToString();
        }
        else
        {
            return obj.ToString();
        }
    }
}
