using System;
using System.Collections.Generic;
using UnityEngine;

public class BeliefFactory {
    readonly GoapAgent agent;
    readonly Dictionary<Beliefs, AgentBelief> beliefs;

    public BeliefFactory(GoapAgent agent, Dictionary<Beliefs, AgentBelief> beliefs) {
        this.agent = agent;
        this.beliefs = beliefs;
    }

    public void AddBelief(Beliefs key, Func<bool> condition) {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(condition)
            .Build());
    }

    public void AddSensorBelief(Beliefs key, Sensor sensor, MemoryObject type, double time)
    {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(() => Time.timeAsDouble - sensor.Memory.GetLatest(type)?.TimeStamp < time)
            .Build());
    }

    public void AddLocationBelief(Beliefs key, float distance, Transform locationCondition) {
        AddLocationBelief(key, distance, () => locationCondition == null ? Vector3.zero : locationCondition.position);
    }

    public void AddLocationBelief(Beliefs key, float distance, Func<Vector3> locationCondition) {
        beliefs.Add(key, new AgentBelief.Builder(key)
            .WithCondition(() => IsInRange(locationCondition, distance))
            .WithLocation(locationCondition)
            .Build());
    }

    private bool IsInRange(Func<Vector3> pos, float range) => Vector3.Distance(agent.transform.position, pos.Invoke()) < range;
}
