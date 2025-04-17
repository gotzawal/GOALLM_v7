// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;

public static class ActionFactory
{
    public static GOAPAction CreatePickAction(string itemName)
    {
        return new GOAPAction(
            name: $"pick_{itemName}",
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "hold",
                    (npc, world) =>
                        npc.UpperBody.ContainsKey("hold") &&
                        npc.UpperBody["hold"].ToString() == "none"
                },
                {
                    "item_at_location",
                    (npc, world) =>
                    {
                        string location = npc.LowerBody["location"].ToString();
                        return world.Places.ContainsKey(location) &&
                               world.Places[location].Inventory.Contains(itemName);
                    }
                }
            },
            effects: new Dictionary<string, object>
            {
                { "hold", itemName },
                { "pickup_item", itemName }
            },
            cost: new Dictionary<string, float>
            {
                { "time", 0.5f },
                { "health", 1f },
                { "mental", 1f }
            }
        );
    }

    public static GOAPAction CreateDropAction(string itemName)
    {
        return new GOAPAction(
            name: $"drop_{itemName}",
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "hold",
                    (npc, world) =>
                        npc.UpperBody.ContainsKey("hold") &&
                        npc.UpperBody["hold"].ToString() == itemName
                },
                {
                    "pose",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("pose") &&
                        npc.LowerBody["pose"].ToString() == "stand"
                }
            },
            effects: new Dictionary<string, object>
            {
                { "hold", "none" },
                { "drop_item", itemName }
            },
            cost: new Dictionary<string, float>
            {
                { "time", 0.5f },
                { "health", 1f },
                { "mental", 1f }
            }
        );
    }

    public static GOAPAction CreateMoveAction(string fromPlace, string toPlace, float timeCost, float healthCost)
    {
        return new GOAPAction(
            name: $"move_{fromPlace}_to_{toPlace}",
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "pose",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("pose") &&
                        npc.LowerBody["pose"].ToString() == "stand"
                },
                {
                    "location",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("location") &&
                        npc.LowerBody["location"].ToString() == fromPlace
                }
            },
            effects: new Dictionary<string, object>
            {
                { "location", toPlace }
            },
            cost: new Dictionary<string, float>
            {
                { "time", timeCost },
                { "health", healthCost },
                { "mental", 0f }
            }
        );
    }

    public static GOAPAction CreateGestureAction(string gestureName)
    {
        return new GOAPAction(
            name: gestureName, // Gesture 이름 그대로 사용
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "hold",
                    (npc, world) =>
                        npc.UpperBody.ContainsKey("hold") &&
                        npc.UpperBody["hold"].ToString() == "none"
                },
                {
                    "pose",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("pose") &&
                        npc.LowerBody["pose"].ToString() == "stand"
                }
            },
            effects: new Dictionary<string, object>
            {
                { $"did_{gestureName}", true }
            },
            cost: new Dictionary<string, float>
            {
                { "time", 1f },
                { "health", 1f },
                { "mental", 1f }
            }
        );
    }

    /// <summary>
    /// 아이템 사용 액션 생성.
    /// 조건: 인벤토리에 해당 아이템이 존재해야 함.
    /// 효과: "used_[itemName]" 플래그를 true로 설정.
    /// </summary>
    public static GOAPAction CreateUseAction(string itemName)
    {
        return new GOAPAction(
            name: $"use_{itemName}",
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    $"has_{itemName}",
                    (npc, world) =>
                        npc.Inventory.Any(i => i.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                }
            },
            effects: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { $"used_{itemName}", true }
            },
            cost: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "time", 1f },
                { "health", 0f },
                { "mental", 0f }
            }
        );
    }

    /// <summary>
    /// 특정 장소에서 앉는 액션 생성.
    /// 조건: NPC의 현재 위치가 해당 장소이며, 현재 자세가 "stand" 여야 함.
    /// 효과: pose를 "sit"으로 변경.
    /// </summary>
    public static GOAPAction CreateSitAction(string placeName)
    {
        string lowerPlace = placeName.ToLower();
        return new GOAPAction(
            name: $"sit_{lowerPlace}",
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "location",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("location") &&
                        npc.LowerBody["location"].ToString().Equals(lowerPlace, StringComparison.OrdinalIgnoreCase)
                },
                {
                    "pose",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("pose") &&
                        npc.LowerBody["pose"].ToString().Equals("stand", StringComparison.OrdinalIgnoreCase)
                }
            },
            effects: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "pose", "sit" }
            },
            cost: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "time", 1f },
                { "health", 1f },
                { "mental", 1f }
            }
        );
    }

    /// <summary>
    /// 특정 장소에서 일어나는 액션 생성.
    /// 조건: NPC의 현재 위치가 해당 장소이며, 현재 자세가 "sit" 여야 함.
    /// 효과: pose를 "stand"로 변경.
    /// </summary>
    public static GOAPAction CreateStandAction(string placeName)
    {
        string lowerPlace = placeName.ToLower();
        return new GOAPAction(
            name: $"stand_{lowerPlace}",
            conditions: new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "location",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("location") &&
                        npc.LowerBody["location"].ToString().Equals(lowerPlace, StringComparison.OrdinalIgnoreCase)
                },
                {
                    "pose",
                    (npc, world) =>
                        npc.LowerBody.ContainsKey("pose") &&
                        npc.LowerBody["pose"].ToString().Equals("sit", StringComparison.OrdinalIgnoreCase)
                }
            },
            effects: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "pose", "stand" }
            },
            cost: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                { "time", 1f },
                { "health", 3f },
                { "mental", 3f }
            }
        );
    }

    /// <summary>
    /// 특정 장소의 상태를 변경하는 액션 생성 (단일 state change).
    /// 액션 이름은 "set_{place}_{statekey}_{value}" 형태로 생성됩니다.
    /// 조건: NPC의 현재 위치가 해당 장소여야 함.
    /// </summary>
    public static GOAPAction CreatePlaceStateChangeAction(string placeName, string stateKey, object value)
    {
        string lowerPlace = placeName.ToLower();
        string lowerStateKey = stateKey.ToLower();
        string valueStr = value.ToString().ToLower();

        var conditions = new Dictionary<string, Func<NPCState, WorldState, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "location",
                (npc, world) =>
                    npc.LowerBody.ContainsKey("location") &&
                    npc.LowerBody["location"].ToString().Equals(lowerPlace, StringComparison.OrdinalIgnoreCase)
            }
        };

        var effects = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { $"place_state:{lowerPlace}:{lowerStateKey}", value }
        };

        var cost = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "time", 0.5f },
            { "health", 0f },
            { "mental", 0f }
        };

        return new GOAPAction(
            name: $"set_{lowerPlace}_{lowerStateKey}_{valueStr}",
            conditions: conditions,
            effects: effects,
            cost: cost
        );
    }
}
