using System.Collections;
using UnityEngine;

public class GreenAI_V1 : GoapAgent
{
    // Debug
    [Header("Debug")]
    [SerializeField] Transform TestInterestPoints;


    public bool isGrounded;
    public bool SetDirectControl = false;
    public float y_velocity;
    public float shortest_distance;
    public int best_direction;
    public float x_distance;
    public float y_distance;

    // Movement Input
    [Space]
    [Header("Movement Input")]
    public float moveInput;
    public bool dashInput;
    public bool jumpInputDown;
    public bool jumpInputUp;
    public bool attackInput;
    GreenAI_Movement movement;
    public LayerMask GroundLayer;

    // Constants
    float RaycastToGroundLength = 12.0f;
    float RaycastFallSearchLength = 0.28f;

    // Other
    private float queuedPathCooldown = 0;
    public float storedXDirection;

    // Sensors
    private Eyesight eyeball;

    [Space]
    [Header("AI Info")]
    public float PreyKillRange = 20f;

    private void Awake()
    {
        movement = GetComponent<GreenAI_Movement>();
        eyeball = GetComponent<Eyesight>();

        GoapAgentSetup();

        InvokeRepeating(nameof(SecondTimer), 1f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        if (AgentSleeping)
        {
            movement.AIMove(0, false, false, false, false);
            return;
        }

        isGrounded = movement.isGrounded;
        y_velocity = movement.rb2d.linearVelocity.y;

        // AI HERE
        // ========================================
        // ========================================
        // ========================================

        // Update the plan and current action if there is one
        if (currentAction == null)
        {
            UpdatePlan();
        }
        
        // If we have a current action, execute it
        if (actionPlan != null && currentAction != null)
        {
            ExecutePlan();

            // TODO: Find a better way to include logic below
            // __________________________________________________________________________
            if (currentGoal != null)
            {
                if (currentGoal.Name == Goals.KeepHungerUp && beliefs[Beliefs.DayAlmostOver].Evaluate())
                {
                    ResetGoal();
                }
                else if (currentGoal.Name == Goals.SeekShelter && beliefs[Beliefs.AgentHungerStarving].Evaluate())
                {
                    ResetGoal();
                }
            }
            // _________________________________________________________________________
            
        }

        // Find a new path to the pathfinding target or update the waypoint
        PathFind();
        
        // Creature movement
        if (path != null && currentWaypoint < path.vectorPath.Count)
        {
            Move(path.vectorPath[currentWaypoint]);
            eyeball.ChangeLookDirectionToTarget(path.vectorPath[currentWaypoint]);
        }


        // ========================================
        // ========================================
        // ========================================

        if (Input.GetKeyDown(KeyCode.K))
        {
            SetDirectControl = !SetDirectControl;
        }
        if (SetDirectControl)
        {
            DirectControl();
        }

        if (path == null)
        {
            movement.AIMove(0, false, false, false, false);
        }
        else
            movement.AIMove(moveInput, dashInput, jumpInputDown, jumpInputUp, attackInput);
        
    }

    private void SecondTimer()
    {
        if (hunger > 0)
        {
            hunger--;
        }
        if (dayTimer > 0)
        {
            dayTimer--;
        }
    }

    public override void BeliefsList(BeliefFactory factory)
    {
        factory.AddBelief(Beliefs.Nothing, () => false);
        factory.AddBelief(Beliefs.AgentIdle, () => IsIdle());
        factory.AddBelief(Beliefs.AgentMoving, () => !IsIdle());

        factory.AddBelief(Beliefs.AgentHungerStarving, () => hunger <= 10);
        factory.AddBelief(Beliefs.AgentHungerLow, () => hunger > 10 && hunger <= 30);
        factory.AddBelief(Beliefs.AgentHungerFull, () => hunger >= 50);

        factory.AddBelief(Beliefs.PreySensed, () => Memory.TargetValid() && Memory.Target.RecentlySeen());
        factory.AddLocationBelief(Beliefs.PreyInKillRange, PreyKillRange, () => Memory.TargetValid() ? Memory.Target.Position : Vector3.zero);
        factory.AddBelief(Beliefs.PreyAlive, () => !IsPreyDead());
        factory.AddBelief(Beliefs.PreyDead, () => IsPreyDead());
        factory.AddBelief(Beliefs.PreyGrabbed, () => PreyGrabbed);
        factory.AddBelief(Beliefs.SafeToEat, () => true); // TODO

        factory.AddLocationBelief(Beliefs.AgentAtShelter, 2f, () => Memory.ShelterValid() ? Memory.SavedShelter.Position : Vector3.zero);
        factory.AddBelief(Beliefs.ShelterKnown, () => IsShelterKnown());
        factory.AddBelief(Beliefs.AgentSleeping, () => AgentSleeping);
        factory.AddBelief(Beliefs.DayAlmostOver, () => dayTimer < 30);

    }

    public override void ActionsList()
    {
        actions.Add(new AgentAction.Builder(Actions.LocateFood)
            .WithStrategy(new LocateStrategy(this, TestInterestPoints, MemoryObject.Food))
            .AddEffect(beliefs[Beliefs.PreySensed])
            .Build());

        actions.Add(new AgentAction.Builder(Actions.ChasePrey)
            .WithStrategy(new MoveStrategy(this, () => Memory.Target.Transform))
            .AddEffect(beliefs[Beliefs.PreyInKillRange])
            .AddPrecondition(beliefs[Beliefs.PreySensed])
            .AddPrecondition(beliefs[Beliefs.PreyAlive])
            .Build());

        // TODO
        actions.Add(new AgentAction.Builder(Actions.KillPrey)
            .WithStrategy(new VoidStrategy())
            .AddEffect(beliefs[Beliefs.PreyDead])
            .AddPrecondition(beliefs[Beliefs.PreySensed])
            .AddPrecondition(beliefs[Beliefs.PreyAlive])
            .Build());

        actions.Add(new AgentAction.Builder(Actions.GrabPrey)
            .WithStrategy(new GrabStrategy(this, () => Memory.Target.Transform))
            .AddEffect(beliefs[Beliefs.PreyGrabbed])
            .AddPrecondition(beliefs[Beliefs.PreySensed])
            .AddPrecondition(beliefs[Beliefs.PreyDead])
            .Build());

        // TODO
        actions.Add(new AgentAction.Builder(Actions.MoveToEat)
            .WithStrategy(new VoidStrategy())
            .AddEffect(beliefs[Beliefs.SafeToEat])
            .Build());

        actions.Add(new AgentAction.Builder(Actions.EatPrey)
            .WithStrategy(new EatStrategy(this, 5f, 20f, () => DestroyTarget())) // Expand later to include the prey being eaten
            .AddEffect(beliefs[Beliefs.AgentHungerLow])
            //.AddPrecondition(beliefs[Beliefs.SafeToEat])
            .AddPrecondition(beliefs[Beliefs.PreyGrabbed])
            .Build());

        // ===========================================================================

        actions.Add(new AgentAction.Builder(Actions.LocateShelter)
            .WithStrategy(new LocateStrategy(this, TestInterestPoints, MemoryObject.Shelter))
            .AddEffect(beliefs[Beliefs.ShelterKnown])
            .Build());

        actions.Add(new AgentAction.Builder(Actions.MoveToShelter)
            .WithStrategy(new MoveStrategy(this, () => Memory.SavedShelter.Transform))
            .AddPrecondition(beliefs[Beliefs.ShelterKnown])
            .AddEffect(beliefs[Beliefs.AgentAtShelter])
            .Build());

        actions.Add(new AgentAction.Builder(Actions.Sleep)
            .WithStrategy(new SleepStrategy(this))
            .AddEffect(beliefs[Beliefs.AgentSleeping])
            .AddPrecondition(beliefs[Beliefs.AgentAtShelter])
            .Build());

        // ===========================================================================

        actions.Add(new AgentAction.Builder(Actions.Relax)
            .WithStrategy(new IdleStrategy(5))
            .AddEffect(beliefs[Beliefs.Nothing])
            .Build());
    }

    public override void GoalsList()
    {
        goals.Add(new AgentGoal.Builder(Goals.KeepHungerUp)
            .WithPriority(3)
            .WithDesiredEffect(beliefs[Beliefs.AgentHungerLow])
            .Build());

        goals.Add(new AgentGoal.Builder(Goals.PreventStarvation)
            .WithPriority(5)
            .WithDesiredEffect(beliefs[Beliefs.AgentHungerLow])
            .WithPrecondition(beliefs[Beliefs.AgentHungerStarving])
            .Build());

        goals.Add(new AgentGoal.Builder(Goals.SeekShelter)
            .WithPriority(4)
            .WithDesiredEffect(beliefs[Beliefs.AgentSleeping])
            .WithPrecondition(beliefs[Beliefs.DayAlmostOver])
            .Build());

        goals.Add(new AgentGoal.Builder(Goals.ChillOut)
            .WithPriority(1)
            .WithDesiredEffect(beliefs[Beliefs.Nothing])
            .Build());

    }

    private bool IsPreyDead()
    {
        if (!Memory.TargetValid())
            return false;
        return Memory.Target.Transform.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero;
    }

    private bool IsShelterKnown()
    {
        if (!Memory.ShelterValid())
        {
            Memory.SavedShelter = Memory.GetLatest(MemoryObject.Shelter);
        }
        return Memory.SavedShelter != null;
    }

    private void DestroyTarget()
    {
        Destroy(Memory.Target.Transform.gameObject);
        Memory.Target = null;
        PreyGrabbed = false;
    }

    public override void SensorsList()
    {
        sensors.Add(eyeball);
        eyeball.Memory = Memory;
    }

    public override bool QueuePathConditions()
    {
        return isGrounded;
    }

    public override void PathFind()
    {
        // --- Path Reset Conditions ---
        bool shouldQueuePath =
            (isGrounded && movement.groundTime == 0.0f && queuedPathCooldown < 0.1f) ||
            (reachedEndOfPath && y_velocity < -1f);

        if (shouldQueuePath)
        {
            queuedPathCooldown = 1.0f;
            SetPath(pathQueued);
        }

        // --- Cooldown Decrement ---
        queuedPathCooldown = Mathf.Max(0f, queuedPathCooldown - Time.deltaTime);

        // --- Path Following ---
        if (path == null)
            return;

        // Stop if we reached the end of the path
        reachedEndOfPath = currentWaypoint >= path.vectorPath.Count;
        if (reachedEndOfPath)
            return;

        // --- Waypoint Advance Conditions ---
        Vector2 targetPos = (Vector2)path.vectorPath[currentWaypoint];
        float sqrDist = ((Vector2)transform.position - targetPos).sqrMagnitude;

        bool closeAndGrounded = isGrounded && (sqrDist < (nextWaypointDist * nextWaypointDist));
        bool fallingNear = (sqrDist < 9f &&
                            Mathf.Abs(x_distance) < 0.25f &&
                            y_distance < -0.25f &&
                            y_velocity < 0f);

        if (closeAndGrounded || fallingNear)
            currentWaypoint++;
    }

    private void Move(Vector2 target)
    {
        x_distance = target.x - transform.position.x;
        y_distance = target.y - transform.position.y;
        float x_distance_abs = Mathf.Abs(x_distance);
        int x_direction = (int)Mathf.Sign(x_distance);
        float angle = Mathf.Atan2(y_distance, x_distance) * Mathf.Rad2Deg;
        if (angle > 0f)
        {
            angle = Mathf.Abs(angle - 90);
        }

        // Left and Right Input
        if (!isGrounded && y_velocity < 0f)
        {
            int best_location = ClosestGroundSearch(x_direction);
            if (movement.jumpsLeft == 0 || shortest_distance < x_distance_abs)
            {  
                if (best_location == 0)
                {
                    moveInput = 0;
                }
                else
                {
                    moveInput = Mathf.Sign(best_location);
                }
            }
            else
            {
                moveInput = x_direction;
            }
        }
        else if (!isGrounded && y_velocity > 1f && x_distance_abs < 0.05f)
        {
            moveInput = 0;
        }
        else
        {
            moveInput = x_direction;
        }
        
        // Jump Input
        if (isGrounded && y_distance > 0.1)
        {
            if (JumpOverFall(ClosestFallSearch(x_direction)))
            {
                jumpInputDown = true;
                storedXDirection = x_direction;
            }
            else if(angle < 35f && angle > 0f)
            {
                jumpInputDown = true;
                storedXDirection = x_direction;
            }
            else
            {
                jumpInputDown = false;
            }
        }
        else if (!isGrounded && y_distance > 0.1 && y_velocity < -3f && angle > 0f && movement.jumpsLeft > 0 && shortest_distance > x_distance_abs)
        {
            jumpInputDown = true;
        }
        else
        {
            jumpInputDown = false;
        }
        
        // Release Jump Input
        if (!isGrounded && y_velocity > 0f && x_distance_abs < 0.1f && y_distance < -0.75f)
        {
            jumpInputUp = true;
        }
        else { jumpInputUp = false; }

        // Dash
        if (x_distance_abs > 4.0f && y_distance < 0.1f)
        {
            moveInput = x_direction;
            dashInput = true;
        }
        else
        {
            dashInput = false;
        }

    }

    public bool JumpOverFall(int fall_num)
    {
        //Debug.Log(fall_num);
        if (fall_num == 1)
        {
            return true;
        }
        return false;
    }

    private int ClosestFallSearch(float x_direction)
    {

        Vector2 v_ONE = new Vector2(1 * x_direction, -1f);
        Vector2 v_TWO = new Vector2(2 * x_direction, -1f);
        Vector2 v_FOUR = new Vector2(4 * x_direction, -1f);
        Vector2 v_SIX = new Vector2(6 * x_direction, -1f);
        Vector2 v_EIGHT = new Vector2(8 * x_direction, -1f);

        Debug.DrawRay(transform.position, v_ONE * RaycastFallSearchLength, Color.red);
        Debug.DrawRay(transform.position, v_TWO * RaycastFallSearchLength, Color.red);
        Debug.DrawRay(transform.position, v_FOUR * RaycastFallSearchLength, Color.red);
        Debug.DrawRay(transform.position, v_SIX * RaycastFallSearchLength, Color.red);
        Debug.DrawRay(transform.position, v_EIGHT * RaycastFallSearchLength, Color.red);
        //Debug.DrawRay(transform.position, new Vector2(-1, -1) * RaycastFallSearchLength, Color.red);
        //Debug.DrawRay(transform.position, new Vector2(-2, -1) * RaycastFallSearchLength, Color.red);
        //Debug.DrawRay(transform.position, new Vector2(-4, -1) * RaycastFallSearchLength, Color.red);
        //Debug.DrawRay(transform.position, new Vector2(-6, -1) * RaycastFallSearchLength, Color.red);
        //Debug.DrawRay(transform.position, new Vector2(-8, -1) * RaycastFallSearchLength, Color.red);

        if (!RaycastFallSearch(v_ONE))
        {
            return 1;
        }
        else if (!RaycastFallSearch(v_TWO))
        {
            return 2;
        }
        else if (!RaycastFallSearch(v_FOUR))
        {
            return 4;
        }
        else if (!RaycastFallSearch(v_SIX))
        {
            return 6;
        }
        else if (!RaycastFallSearch(v_EIGHT))
        {
            return 8;
        }
        return 0;
    }

    private bool RaycastFallSearch(Vector2 vector)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, vector, (vector * RaycastFallSearchLength).magnitude, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
        {
            float myAngle = Mathf.Abs(90f - (Vector3.Angle(hit.normal, Vector3.up)) - 90f);
            if (myAngle < movement.slopeLimit)
            {
                //Debug.Log(hit.collider.name);
                return true;
            }
        }
        return false;
    }

    private int ClosestGroundSearch(int x_direction)
    {
        Vector2 v_P75 = new Vector2(0.75f, -1f);
        Vector2 v_N75 = new Vector2(-0.75f, -1f);
        Vector2 v_P50 = new Vector2(0.5f, -1f);
        Vector2 v_N50 = new Vector2(-0.5f, -1f);
        Vector2 v_P25 = new Vector2(0.25f, -1f);
        Vector2 v_N25 = new Vector2(-0.25f, -1f);
        Vector2 v_P10 = new Vector2(0.1f, -1f);
        Vector2 v_N10 = new Vector2(-0.1f, -1f);
        Vector2 v_P04 = new Vector2(0.04f, -1f);
        Vector2 v_N04 = new Vector2(-0.04f, -1f);

        //Debug.DrawRay(transform.position, Vector2.down * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_P75 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_N75 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_P50 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_N50 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_P25 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_N25 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_P10 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_N10 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_P04 * RaycastToGroundLength, Color.blue);
        Debug.DrawRay(transform.position, v_N04 * RaycastToGroundLength, Color.blue);

        shortest_distance = 100f;
        best_direction = 0;
        float P04_Distance;
        float N04_Distance;

        if (RaycastValidGround(v_P04, out float distance_P1) && CheckShortestDistance(distance_P1))
        {
            best_direction = 1;
        }
        if (RaycastValidGround(v_P10, out float distance_P2) && CheckShortestDistance(distance_P2))
        {
            best_direction = 2;
        }
        if (RaycastValidGround(v_P25, out float distance_P3) && CheckShortestDistance(distance_P3))
        {
            best_direction = 3;
        }
        if (RaycastValidGround(v_P50, out float distance_P4) && CheckShortestDistance(distance_P4))
        {
            best_direction = 4;
        }
        if (RaycastValidGround(v_P75, out float distance_P5) && CheckShortestDistance(distance_P5))
        {
            best_direction = 5;
        }

        if (RaycastValidGround(v_N04, out float distance_N1) && CheckShortestDistance(distance_N1))
        {
            best_direction = -1;
        }
        if (RaycastValidGround(v_N10, out float distance_N2) && CheckShortestDistance(distance_N2))
        {
            best_direction = -2;
        }
        if (RaycastValidGround(v_N25, out float distance_N3) && CheckShortestDistance(distance_N3))
        {
            best_direction = -3;
        }
        if (RaycastValidGround(v_N50, out float distance_N4) && CheckShortestDistance(distance_N4))
        {
            best_direction = -4;
        }
        if (RaycastValidGround(v_N75, out float distance_N5) && CheckShortestDistance(distance_N5))
        {
            best_direction = -5;
        }

        P04_Distance = distance_P1;
        N04_Distance = distance_N1;
        if (Mathf.Abs(best_direction) == 1)
        {
            if (Mathf.Abs(P04_Distance - N04_Distance) < 0.1f)
            {
                if (y_velocity < 0f && y_velocity != -5f && storedXDirection != x_direction)
                {
                    best_direction = 0;
                }
                else
                {
                    best_direction = x_direction;
                }
            }
        }

        if (best_direction == 0)
        {
            //Debug.Log("NOTHING");
            best_direction = 0;
        }
        return best_direction;
    }

    private bool RaycastValidGround(Vector2 vector, out float distance)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, vector, (vector * RaycastToGroundLength).magnitude, GroundLayer);
        if (hit.collider != null)
        {
            float myAngle = Mathf.Abs(90f - (Vector3.Angle(hit.normal, Vector3.up)) - 90f);
            if (myAngle < movement.slopeLimit)
            {
                //Debug.Log(hit.collider.name);
                distance = hit.distance;
                return true;
            }
        }
        distance = 0;
        return false;
    }

    private bool CheckShortestDistance(float distance)
    {
        if (distance > shortest_distance)
        {
            return false;
        }
        shortest_distance = distance;
        return true;
    }

    private void DirectControl()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        dashInput = Input.GetKey(KeyCode.LeftShift);
        jumpInputDown = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.J);
        jumpInputUp = Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.J);
        attackInput = Input.GetMouseButton(0);
    }
}
