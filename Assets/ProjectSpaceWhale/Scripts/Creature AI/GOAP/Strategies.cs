using Pathfinding;
using System;
using UnityEngine;

public interface IActionStrategy
{
    bool CanPerform {  get; }
    bool Complete { get; }

    bool EndEarly { get; }

    void Start()
    {
        // noop
    }

    void Update(float deltaTime)
    {
        // noop
    }

    void Stop()
    {
        // noop
    }

    void StopEarly()
    {
        // noop
    }
}


public class IdleStrategy : IActionStrategy
{
    public bool CanPerform => true; // Agent can always Idle
    public bool Complete { get; private set; }

    public bool EndEarly => false;

    readonly CountdownTimer timer;

    public IdleStrategy(float duration)
    {
        timer = new CountdownTimer(duration);
        timer.OnTimerStart += () => Complete = false;
        timer.OnTimerStop += () => Complete = true;
    }

    public void Start()
    {
        timer.Start();
        //agent.Destination.target = null;
    }
    public void Update(float deltaTime) => timer.Tick(deltaTime);
}

public class LocateStrategy : IActionStrategy
{
    readonly GoapAgent agent;
    readonly MemoryObject objectSearched;

    readonly Transform[] targetChilds;

    public bool CanPerform => !Complete;
    public bool Complete { get; private set; }

    public bool EndEarly => false;

    public LocateStrategy(GoapAgent agent, Transform targetPoints, MemoryObject objectSearched)
    {
        this.agent = agent;
        this.objectSearched = objectSearched;
        targetChilds = targetPoints.GetComponentsInChildren<Transform>();
    }

    public void Start()
    {
        UpdateDestination();
        agent.Memory.SetSearchBehavior(objectSearched);
        Complete = false;
        agent.reachedEndOfPath = false;
    }

    public void Update(float duration)
    {
        if (agent.reachedEndOfPath)
        {
            UpdateDestination();
        }

        if (agent.Memory.TargetValid())
        {
            Complete = true;
        }
    }

    public void Stop()
    {
        agent.ResetPathfinding();
    }

    private void UpdateDestination()
    {
        int randomIndex = UnityEngine.Random.Range(0, targetChilds.Length);
        agent.Destination.target = targetChilds[randomIndex];
        agent.reachedEndOfPath = false;
    }
}

public class MoveStrategy : IActionStrategy
{
    readonly GoapAgent agent;
    readonly Func<Transform> destination;

    public MoveStrategy(GoapAgent agent, Func<Transform> destination)
    {
        this.agent = agent;
        this.destination = destination;
    }

    public void Start()
    {
        var dest = destination?.Invoke();
        if (dest != null)
            agent.Destination.target = dest;
        agent.reachedEndOfPath = false;
    }

    public void Stop() => agent.ResetPathfinding();

    public bool CanPerform => !Complete;
    public bool Complete => agent.reachedEndOfPath;

    public bool EndEarly => false;
}

public class EatStrategy : IActionStrategy
{
    public bool CanPerform => true;
    public bool Complete { get; private set; }

    public bool EndEarly => false;

    readonly CountdownTimer timer;
    readonly GoapAgent agent;
    readonly Action stopAction;

    public EatStrategy(GoapAgent agent, float duration, float hungerIncrease, Action stopAction)
    {
        timer = new CountdownTimer(duration);
        timer.OnTimerStart += () => Complete = false;
        timer.OnTimerStop += () => Complete = true;
        timer.OnTimerStop += () => agent.hunger += hungerIncrease;
        this.agent = agent;
        this.stopAction = stopAction;
        Complete = false;
    }

    public void Start()
    {
        timer.Start();
        //agent.Destination.target = null;
    }
    public void Update(float deltaTime)
    {
        agent.path = null;
        timer.Tick(deltaTime);
    }

    public void Stop()
    {
        stopAction.Invoke();
    }
}

public class SleepStrategy : IActionStrategy
{
    public bool CanPerform => true;
    public bool Complete => false;

    public bool EndEarly => false;

    readonly GoapAgent agent;

    public SleepStrategy(GoapAgent agent)
    {
        this.agent = agent;
    }

    public void Start()
    {
        agent.AgentSleeping = true;
        agent.path = null;
    }
}

public class GrabStrategy : IActionStrategy
{
    public bool CanPerform => !Complete;
    public bool Complete { get; private set; }

    public bool EndEarly { get; private set; }

    readonly GoapAgent agent;
    readonly Func<Transform> destination;

    public GrabStrategy(GoapAgent agent, Func<Transform> destination)
    {
        this.agent = agent;
        this.destination = destination;
    }

    public void Start()
    {
        var dest = destination?.Invoke();
        if (dest != null)
            agent.Destination.target = dest;
        agent.reachedEndOfPath = false;
        Complete = false;
        EndEarly = false;
    }

    public void Stop() => agent.ResetPathfinding();

    public void StopEarly() => Stop();

    public void Update(float duration)
    {
        if (agent.beliefs[Beliefs.PreyAlive].Evaluate())
        {
            EndEarly = true;
        }

        if (agent.reachedEndOfPath)
        {
            agent.Memory.Target.Transform.SetParent(agent.transform);
            agent.PreyGrabbed = true;
            Complete = true;
        }
    }

}

public class VoidStrategy : IActionStrategy
{ 
    public bool CanPerform => true;
    public bool Complete => true;

    public bool EndEarly => false;



}
