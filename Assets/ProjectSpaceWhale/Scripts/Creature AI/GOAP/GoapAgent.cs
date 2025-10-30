using Pathfinding;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum Beliefs
{
    Nothing,
    AgentIdle,
    AgentMoving,

    AgentHungerStarving,
    AgentHungerLow,
    AgentHungerFull,

    PreySensed,
    AgentAtShelter,
    PreyInKillRange,
    PreyAlive,
    PreyDead,
    PreyGrabbed,
    SafeToEat,

    ShelterKnown,
    AgentSleeping,
    DayAlmostOver
}

public enum Actions
{
    Nothing,
    LocateFood,
    ChasePrey,
    KillPrey,
    GrabPrey,
    MoveToEat,
    EatPrey,
    LocateShelter,
    MoveToShelter,
    Sleep,
    Relax
}

public enum Goals
{
    KeepHungerUp,
    PreventStarvation,
    SeekShelter,
    ChillOut
}


[RequireComponent(typeof(AIDestinationSetter))]
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(ObjectType))]
public abstract class GoapAgent : MonoBehaviour {
    [Header("Pathfinding")]
    [HideInInspector] public AIDestinationSetter Destination;
    public Path path;
    public Path pathQueued;
    [HideInInspector] public Seeker seeker;
    public float DistanceToCurrentTarget;
    public float nextWaypointDist = 5f;
    public int currentWaypoint = 0;
    public bool reachedEndOfPath = false;

    private float repathInterval = 0.3f;
    private float nextRepathTime = 0f;

    [Header("GOAP")]
    public AgentGoal lastGoal;
    public AgentGoal currentGoal;
    public ActionPlan actionPlan;
    public AgentAction currentAction;

    public Dictionary<Beliefs, AgentBelief> beliefs;
    public HashSet<AgentAction> actions;
    public HashSet<AgentGoal> goals;

    public IGoapPlanner gPlanner;

    public HashSet<Sensor> sensors;

    [Header("Memory")]
    [SerializeField] public CreatureMemory Memory;

    [Header("Test Stats")]
    public float hunger = 70f;
    public float dayTimer = 200f;
    public bool AgentSleeping = false;
    public bool PreyGrabbed = false;


    // ========================== Pathfinding ==============================
    private void LateUpdate()
    {
        UpdatePath();
    }

    public void UpdatePath()
    {
        if (Time.time < nextRepathTime) return;
        if (!seeker.IsDone()) return;
        if (Destination.target == null) return;

        seeker.StartPath(transform.position, Destination.target.position, QueuePath);
        nextRepathTime = Time.time + repathInterval;
    }

    public void QueuePath(Path p)
    {
        if (QueuePathConditions())
        {
            SetPath(p);
        }
        else
        {
            pathQueued = p;
        }

    }

    public void SetPath(Path p)
    {
        if (p != null && !p.error)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    public abstract bool QueuePathConditions();
    public abstract void PathFind();

    // ===================== GOAP =========================

    public void GoapAgentSetup()
    {
        Destination = GetComponent<AIDestinationSetter>();
        seeker = GetComponent<Seeker>();
        gPlanner = new GoapPlanner();
        Memory = new CreatureMemory(GetComponent<ObjectType>().GetName);

        SetupSensors();
        SetupBeliefs();
        SetupActions();
        SetupGoals();
    }

    void SetupBeliefs()
    {
        beliefs = new Dictionary<Beliefs, AgentBelief>();
        BeliefFactory factory = new BeliefFactory(this, beliefs);

        BeliefsList(factory);
    }

    void SetupActions()
    {
        actions = new HashSet<AgentAction>();

        ActionsList();
    }

    void SetupGoals()
    {
        goals = new HashSet<AgentGoal>();

        GoalsList();
    }

    void SetupSensors()
    {
        sensors = new HashSet<Sensor>();

        SensorsList();
    }

    public abstract void BeliefsList(BeliefFactory factory);
    public abstract void ActionsList();
    public abstract void GoalsList();
    public abstract void SensorsList();

    public void CalculatePlan()
    {
        var priorityLevel = currentGoal?.Priority ?? 0;

        HashSet<AgentGoal> goalsToCheck = goals;

        // If we have a current goal, we only want to check goals with higher priority
        if (currentGoal != null)
        {
            Debug.Log("Current goal exists, checking goals with higher priority");
            goalsToCheck = new HashSet<AgentGoal>(goals.Where(g => g.Priority > priorityLevel));
        }

        var potentialPlan = gPlanner.Plan(this, goalsToCheck, lastGoal);
        if (potentialPlan != null)
        {
            actionPlan = potentialPlan;
        }
    }

    public void UpdatePlan()
    {
        Debug.Log("Calculating any potential new plan");
        CalculatePlan();

        if (actionPlan != null && actionPlan.Actions.Count > 0)
        {
            currentGoal = actionPlan.AgentGoal;
            Debug.Log($"Goal: {currentGoal.Name} with {actionPlan.Actions.Count} actions in plan");
            currentAction = actionPlan.Actions.Pop();
            Debug.Log($"Popped action: {currentAction.Name}");
            // Verify all precondition effects are true
            if (currentAction.Preconditions.All(b => b.Evaluate()))
            {
                currentAction.Start();
            }
            else
            {
                Debug.Log("Preconditions not met, clearing current action and goal");
                ResetGoal();
            }
        }
    }

    public void ExecutePlan()
    {
        // Early exit if there’s no current action
        if (currentAction == null)
            return;

        currentAction.Update(Time.deltaTime);

        bool actionFinished = currentAction.Complete || currentAction.EndEarly;

        if (!actionFinished) return;

        // Handle completion or early termination
        if (currentAction.Complete)
            Debug.Log($"{currentAction.Name} complete");
        else
            Debug.Log($"{currentAction.Name} ended early");

        // Stop appropriately
        if (currentAction.Complete)
            currentAction.Stop();
        else
            currentAction.StopEarly();

        currentAction = null;

        // If no more actions remain, finish plan
        if (actionPlan.Actions.Count == 0)
        {
            Debug.Log("Plan complete");
            lastGoal = currentGoal;
            currentGoal = null;
        }
    }

    public bool IsIdle()
    {
        return path == null || reachedEndOfPath;
    }

    public void ResetGoal()
    {
        // Force the planner to re-evaluate the plan
        currentGoal = null;
        currentAction = null;
    }

    public void ResetPathfinding()
    {
        reachedEndOfPath = false;
        Destination.target = null;
    }
}
