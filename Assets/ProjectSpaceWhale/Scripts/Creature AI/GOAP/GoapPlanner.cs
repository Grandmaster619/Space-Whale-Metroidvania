using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IGoapPlanner
{
    ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null);
}

public class GoapPlanner : IGoapPlanner
{
    public ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null)
    {
        List<AgentGoal> orderedGoals = goals
        // Goal is relevant only if at least one desired effect is false
        .Where(g => g.DesiredEffects.Any(b => !b.Evaluate()))
        // AND either it has no preconditions OR all preconditions are true
        .Where(g => g.Preconditions.Count == 0 || g.Preconditions.All(p => p.Evaluate()))
        // Sort by priority
        .OrderByDescending(g => g.Priority)
        .ToList();

        // Try to solve each goal in order
        foreach (var goal in orderedGoals)
        {
            ActionPlan newActionPlan = EvaluateActions(agent, goal);
            if (newActionPlan != null)
            {
                return newActionPlan;
            }
        }

        Debug.LogWarning("No Plan found");
        return null;
    }

    public ActionPlan EvaluateActions(GoapAgent agent, AgentGoal goal) 
    {
        Node goalNode = new Node(null, null, goal.DesiredEffects, 0);

        // If we can find a path to the goal, return the plan
        if (FindPath(goalNode, agent.actions))
        {
            // If the goalNode has no leaves and no action to perform try a different goal
            if (goalNode.IsLeafDead) return null;

            Stack<AgentAction> actionStack = new Stack<AgentAction>();
            while (goalNode.Leaves.Count > 0)
            {
                var cheapestLeaf = goalNode.Leaves.OrderBy(leaf => leaf.Cost).First();
                goalNode = cheapestLeaf;
                actionStack.Push(cheapestLeaf.Action);
            }

            return new ActionPlan(goal, actionStack, goalNode.Cost);
        }

        return null;
    }

    bool FindPath(Node parent, HashSet<AgentAction> actions)
    {
        // Order actions by cost, ascending
        var orderedActions = actions.OrderBy(a => a.Cost);

        foreach (var action in actions)
        {
            var requiredEffects = parent.RequiredEffects;

            // Remove any effects that evaluate to true, there is no action to take
            requiredEffects.RemoveWhere(b => b.Evaluate());

            // If there are no required effects to fulfill, we have a plan
            if (requiredEffects.Count == 0)
            {
                return true;
            }

            if (action.Effects.Any(requiredEffects.Contains))
            {
                var newRequiredEffects = new HashSet<AgentBelief>(requiredEffects);
                newRequiredEffects.ExceptWith(action.Effects);
                newRequiredEffects.UnionWith(action.Preconditions);

                var newAvailableActions = new HashSet<AgentAction>(actions);
                newAvailableActions.Remove(action);

                var newNode = new Node(parent, action, newRequiredEffects, parent.Cost + action.Cost);
                
                // Explore the new node recursively
                if (FindPath(newNode, newAvailableActions))
                {
                    parent.Leaves.Add(newNode);
                    newRequiredEffects.ExceptWith(newNode.Action.Preconditions);
                }

                // If all effects at this depth have been satisfied, return true
                if (newRequiredEffects.Count == 0)
                {
                    return true;
                }
            }
        }
        return false;
    }
}

public class Node
{
    public Node Parent { get; }
    public AgentAction Action { get; }
    public HashSet<AgentBelief> RequiredEffects { get; }
    public List<Node> Leaves { get; }
    public float Cost { get; }

    public bool IsLeafDead => Leaves.Count == 0 && Action == null;

    public Node(Node parent, AgentAction action, HashSet<AgentBelief> effects, float cost)
    {
        Parent = parent;
        Action = action;
        RequiredEffects = new HashSet<AgentBelief>(effects);
        Leaves = new List<Node>();
        Cost = cost;
    }



}

public class ActionPlan
{
    public AgentGoal AgentGoal { get; }
    public Stack<AgentAction> Actions { get; }
    public float TotalCost { get; set; }

    public ActionPlan(AgentGoal goal, Stack<AgentAction> actions, float totalCost)
    {
        AgentGoal = goal; 
        Actions = actions; 
        TotalCost = totalCost;
    }
}
