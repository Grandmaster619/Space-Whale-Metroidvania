using System.Collections.Generic;
using UnityEngine;

public class AgentGoal
{
    public Goals Name { get; }
    public float Priority { get; private set; }
    public HashSet<AgentBelief> DesiredEffects { get; } = new();
    public HashSet<AgentBelief> Preconditions { get; } = new();

    AgentGoal(Goals name)
    {
        Name = name;
    }

    public class Builder
    {
        readonly AgentGoal goal;

        public Builder(Goals name)
        {
            goal = new AgentGoal(name);
        }

        public Builder WithPriority(float priority)
        {
            goal.Priority = priority;
            return this;
        }

        public Builder WithDesiredEffect(AgentBelief effect)
        {
            goal.DesiredEffects.Add(effect);
            return this;
        }

        public Builder WithPrecondition(AgentBelief precondition)
        {
            goal.Preconditions.Add(precondition);
            return this;
        }

        public AgentGoal Build()
        {
            return goal;
        }
    }
}
