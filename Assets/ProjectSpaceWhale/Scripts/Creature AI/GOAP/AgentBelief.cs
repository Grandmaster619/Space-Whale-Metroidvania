using System;
using UnityEngine;

public class AgentBelief {
    private Beliefs name;

    Func<bool> condition = () => false;
    Func<Vector3> observedLocation = () => Vector3.zero;

    public AgentBelief(Beliefs name) {
        this.name = name;
    }

    public bool Evaluate() => condition();

    public Beliefs Name => name;

    public Vector3 Location => observedLocation();

    public void UpdateLocation(Vector3 location) { observedLocation = () => location; }

    public class Builder {
        private AgentBelief belief;

        public Builder(Beliefs name) {
            belief = new AgentBelief(name);
        }

        public Builder WithCondition(Func<bool> condition) {
            belief.condition = condition;
            return this;
        }

        public Builder WithLocation(Func<Vector3> observedLocation) {
            belief.observedLocation = observedLocation;
            return this;
        }

        public AgentBelief Build() {
            return belief;
        }
    }
}
