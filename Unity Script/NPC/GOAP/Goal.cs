// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;

public class Goal
{
    public string Name { get; private set; }
    public Func<NPCState, WorldState, bool> Condition { get; private set; }
    public bool Achieved { get; set; }
    public float Weight { get; private set; }
    public Dictionary<string, object> RequiredState { get; private set; }

    public Goal(
        string name,
        Func<NPCState, WorldState, bool> condition,
        float weight = 1f,
        Dictionary<string, object> requiredState = null
    )
    {
        Name = name;
        Condition = condition;
        Achieved = false;
        Weight = weight;
        RequiredState =
            requiredState != null
                ? new Dictionary<string, object>(requiredState)
                : new Dictionary<string, object>();
    }
}
