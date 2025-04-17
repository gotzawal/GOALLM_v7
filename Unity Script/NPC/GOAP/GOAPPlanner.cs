// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Assuming Place, Item, GOAPAction, Goal, NPCState, WorldState, GOAPPlanner, ActionFactory, GoalParser, NPCStatus, Helpers are defined elsewhere

public class GOAPPlanner
{
    private List<GOAPAction> actions;
    private List<Goal> goals;

    public GOAPPlanner(List<Goal> goals, List<GOAPAction> actions)
    {
        this.goals = goals;
        this.actions = actions;
    }

    public List<GOAPAction> Plan(NPCState npcState, WorldState worldState)
    {
        var openList = new PriorityQueue<StateNode>();
        var closedList = new HashSet<string>();
        var cameFrom = new Dictionary<string, (string, GOAPAction)>();

        var initialAchievedGoals = new HashSet<string>();
        float initialHeuristic = Heuristic(npcState, worldState, initialAchievedGoals);
        openList.Enqueue(
            new StateNode(npcState, worldState, initialAchievedGoals, 0, initialHeuristic),
            initialHeuristic
        );

        string initialKey = GenerateStateKey(npcState, worldState, initialAchievedGoals);
        cameFrom[initialKey] = (null, null);
        var costSoFar = new Dictionary<string, float> { { initialKey, 0 } };

        int maxIterations = 1000; // Reduced from 10000
        int iterations = 0;

        while (!openList.IsEmpty())
        {
            if (iterations++ > maxIterations)
            {
                Debug.LogError(
                    "GOAP Planner exceeded maximum iterations (5000). No valid plan found."
                );
                return null;
            }

            var currentNode = openList.Dequeue();
            string currentKey = GenerateStateKey(
                currentNode.NPCState,
                currentNode.WorldState,
                currentNode.AchievedGoals
            );

            if (closedList.Contains(currentKey))
                continue;

            closedList.Add(currentKey);

            // Debugging: Current state and achieved goals
            //Debug.Log($"\n[GOAPPlanner] Processing State Key: {currentKey}");
            //Debug.Log($"[GOAPPlanner] Achieved Goals: {string.Join(", ", currentNode.AchievedGoals)}");

            // Check if all goals are met
            bool allGoalsMet = goals.All(goal =>
                goal.Condition(currentNode.NPCState, currentNode.WorldState) || goal.Weight <= 0
            );
            //Debug.Log($"[GOAPPlanner] All Goals Met: {allGoalsMet}");
            if (allGoalsMet)
            {
                Debug.Log("[GOAPPlanner] All goals achieved. Reconstructing plan.");
                return ReconstructPlan(cameFrom, currentKey);
            }

            foreach (var action in actions)
            {
                if (action.IsApplicable(currentNode.NPCState, currentNode.WorldState))
                {
                    //Debug.Log($"[GOAPPlanner] Action '{action.Name}' is applicable.");
                    var (newNpcState, newWorldState) = action.Apply(
                        currentNode.NPCState,
                        currentNode.WorldState
                    );
                    float actionCost = action.Cost.Values.Sum(Math.Abs);
                    float newCost = currentNode.CostSoFar + actionCost;

                    var newAchievedGoals = new HashSet<string>(currentNode.AchievedGoals);
                    foreach (var goal in goals)
                    {
                        if (
                            !newAchievedGoals.Contains(goal.Name)
                            && goal.Condition(newNpcState, newWorldState)
                        )
                        {
                            newAchievedGoals.Add(goal.Name);
                            //Debug.Log($"[GOAPPlanner] Goal '{goal.Name}' achieved.");
                        }
                    }

                    string newKey = GenerateStateKey(newNpcState, newWorldState, newAchievedGoals);

                    // Avoid revisiting the same state
                    if (!costSoFar.ContainsKey(newKey) || newCost < costSoFar[newKey])
                    {
                        costSoFar[newKey] = newCost;
                        float h = Heuristic(newNpcState, newWorldState, newAchievedGoals);
                        float priority = newCost + h;
                        openList.Enqueue(
                            new StateNode(newNpcState, newWorldState, newAchievedGoals, newCost, h),
                            priority
                        );
                        cameFrom[newKey] = (currentKey, action);
                        //Debug.Log($"[GOAPPlanner] Enqueued new state with key: {newKey} and priority: {priority}");
                    }
                }
                else
                {
                    //Debug.Log($"[GOAPPlanner] Action '{action.Name}' is not applicable.");
                }
            }
        }

        Debug.LogError(
            "GOAP Planner could not find a plan to achieve the goals within iteration limits."
        );
        return null;
    }

    private float Heuristic(NPCState npcState, WorldState worldState, HashSet<string> achievedGoals)
    {
        // Sum of remaining goal weights as a heuristic
        return goals
            .Where(goal => !achievedGoals.Contains(goal.Name) && goal.Weight > 0)
            .Sum(goal => goal.Weight);
    }

    private string GenerateStateKey(
        NPCState npcState,
        WorldState worldState,
        HashSet<string> achievedGoals
    )
    {
        // Simplify state representation to improve hashing speed
        string key =
            $"{Helpers.MakeHashable(npcState.UpperBody)}|"
            + $"{Helpers.MakeHashable(npcState.LowerBody)}|"
            + $"{Helpers.MakeHashable(npcState.Resources.Where(kvp => kvp.Key != "time").ToDictionary(kvp => kvp.Key, kvp => kvp.Value))}|"
            + $"{string.Join(",", npcState.Inventory.OrderBy(i => i))}|"
            + $"{Helpers.MakeHashable(npcState.StateData)}|"
            + $"{Helpers.MakeHashable(worldState.Places)}|"
            + $"{Helpers.MakeHashable(worldState.Items)}|"
            + $"{string.Join(",", achievedGoals.OrderBy(g => g))}";
        return key;
    }

    private List<GOAPAction> ReconstructPlan(
        Dictionary<string, (string, GOAPAction)> cameFrom,
        string currentKey
    )
    {
        var actions = new List<GOAPAction>();
        while (cameFrom[currentKey].Item2 != null)
        {
            actions.Add(cameFrom[currentKey].Item2);
            currentKey = cameFrom[currentKey].Item1;
        }
        actions.Reverse();
        return actions;
    }

    private class StateNode
    {
        public NPCState NPCState { get; private set; }
        public WorldState WorldState { get; private set; }
        public HashSet<string> AchievedGoals { get; private set; }
        public float CostSoFar { get; private set; }
        public float Heuristic { get; private set; }

        public StateNode(
            NPCState npcState,
            WorldState worldState,
            HashSet<string> achievedGoals,
            float costSoFar,
            float heuristic
        )
        {
            NPCState = npcState;
            WorldState = worldState;
            AchievedGoals = new HashSet<string>(achievedGoals);
            CostSoFar = costSoFar;
            Heuristic = heuristic;
        }
    }
}
