using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ------------ Enums -------------------------
public enum PlayerState
{
    Idle,
    Run,
    Jump,
    Fall,

    Turn, // TODO
    Backflip, // TODO

    WallSlide,
    WallJump,

    LedgeHang,
    LedgeClimb,

    Dash,
    Wavedash,
    Wallrun,

    Attack,
    WallClimb
}

public class Player_Movement : MonoBehaviour
{
    // ------------- Inspector Fields -------------------------
    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float airMoveSpeed = 4.5f;
    [SerializeField] float jumpForce = 14f;
    [SerializeField] float wallSlideSpeed = 2.5f;
    [SerializeField] float wallSlideHoldDownSpeed = 3.8f;
    [SerializeField] float turnBoost = 1.25f;

    [Header("Wall Jump")]
    [SerializeField] float wallJumpHorizontalBoost = 10f;
    [SerializeField] float wallJumpVerticalBoost = 12f;
    [SerializeField] float wallJumpControlLock = 0.15f;

    [Header("Ledge")]
    [SerializeField] float ledgeJumpForce = 12f;
    [SerializeField] float ledgeClimbUpTime = 0.3f;
    [SerializeField] Vector2 ledgeClimbOffset = new Vector2(0.5f, 1.2f); // forward, up
    [SerializeField] AnimationCurve ledgeClimbCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Forgiveness")]
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBuffer = 0.1f;
    [SerializeField] float slopeLimit = 58f;
    [SerializeField] float groundedBuffer = 0.1f;
    [SerializeField] float wallCheck = 0.3f;

    [Header("Other")]
    [SerializeField] LayerMask GroundLayer;
    [SerializeField] float gravityScale;

    [Header("Ledge Detection")]
    [SerializeField] float ledgeCheckHorizontal = 0.4f;
    [SerializeField] float ledgeCheckUpper = 1.5f;
    [SerializeField] float ledgeCheckLower = 0.2f;
    [SerializeField] Vector2 ledgeOffset = new Vector2(0.3f, 1.2f);
    private Vector2 ledgeHangPoint;
    private bool ledgeDetected;

    [Header("Dash Settings")]
    [SerializeField] bool dashUnlocked = true;
    [SerializeField] float initialDashSpeed = 15f;
    [SerializeField] float runningDashSpeed = 5;
    [SerializeField] float dashDuration = 0.25f;
    [SerializeField] float waveDashForce = 6f;
    [SerializeField] float waveDashResist = 0.1f;
    [SerializeField] float waveDashGroundedTime = 0.2f;
    [SerializeField] float minWallRunForce = 8f;
    [SerializeField] float wallRunMultiplier;
    [SerializeField] float wallRunJumpMultiplier = 0.2f;

    [Header("Attack")]
    [SerializeField] float attackCooldown = 0.4f;
    [SerializeField] Vector3 ranUpAttackRange_Top;
    [SerializeField] Vector3 ranUpAttackRange_Down;
    [SerializeField] float ranUpAttackRadius = 0.02f;
    [SerializeField] Vector3 ranForwardAttackRange_Top;
    [SerializeField] Vector3 ranForwardAttackRange_Down;
    [SerializeField] float ranForwardAttackRadius = 0.04f;

    [Header("Wall Climb")]
    [SerializeField] float wallMoveSpeed = 4f;
    [SerializeField] float wallCheckDistance = 0.3f;
    [SerializeField] float secondSphereCastOffset = 0.5f;
    RaycastHit2D wall_climb_wall_pos;

    public float GetAttackCooldown() { return attackCooldown; }
    private float attack_timer;
    public float GetAttackTimer() { return attack_timer; }


    [Header("Debug")]
    [SerializeField] float groundedTime;
    [SerializeField] float airTime;
    [SerializeField] private PlayerState state = PlayerState.Idle;


    // -------------- Components -------------------------
    private Rigidbody2D rb;
    private BoxCollider2D col;


    // --------------- State Data ---------------------------
    public PlayerState GetPlayerState() { return state; }

    private float horizontalInput;
    private float verticalInput;
    private bool dashPressedInput;
    private bool dashHeldInput;
    private int facingDirection;
    public int GetFacingDirection() { return facingDirection; }
    private bool attackInput;

    private bool isGrounded;
    private bool onWall;
    private bool nearLedge;

    private float lastGroundedTime;
    private float lastJumpPressedTime;
    private bool jumpHeld;
    private bool jumpPressedWithBuffer;
    private float wallJumpLockTimer;

    private Vector2 dashDirection;
    private float dashTimer;
    private bool usedDash;
    private bool midAirDash;

    // --------------- Other ---------------------------
    Vector3 wallPosition;
    List<int> pool = new List<int>() { 0, 1, 2, 3 };
    private Vector3 attackPosition;
    public Vector3 GetAttackPosition() { return attackPosition; }
    private int tailIndex;
    private int shuffleIndex;
    public int GetTailIndex() { return tailIndex; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        gravityScale = rb.gravityScale;
        col = GetComponent<BoxCollider2D>();

        lastJumpPressedTime = -999f;
        facingDirection = 1;
    }

    void Update()
    {
        // --- Input---
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        dashPressedInput = Input.GetKeyDown(KeyCode.C);
        dashHeldInput = Input.GetKey(KeyCode.C);
        attackInput = Input.GetKeyDown(KeyCode.X);

        // jump buffering
        if (Input.GetButtonDown("Jump"))
        {
            lastJumpPressedTime = Time.time;
            jumpHeld = true;
        }

        if (Input.GetButtonUp("Jump"))
            jumpHeld = false;

        // dash
        if (state != PlayerState.Dash && dashPressedInput && dashUnlocked && !usedDash)
        {
            StartDash();
        }

        // attack
        if (attackInput && attack_timer <= 0)
        {
            StartAttack();
        }
        else if (attack_timer > 0)
        {
            attack_timer -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        // --- Environment Checks (physics) ---
        var bounds = col.bounds;
        jumpPressedWithBuffer = Time.time - lastJumpPressedTime <= jumpBuffer;
        facingDirection = CheckFacingDirection();
        isGrounded = CheckGrounded(bounds);
        onWall = CheckWall(bounds);
        nearLedge = CheckLedge(bounds);

        // record grounded time for coyote and update dash
        if (isGrounded || state == PlayerState.LedgeHang)
        {
            lastGroundedTime = Time.time;
            groundedTime += Time.fixedDeltaTime;
            usedDash = false;
            airTime = 0f;
        }
        else
        {
            groundedTime = 0f;
            airTime += Time.fixedDeltaTime;
        }

        // --- State Machine (physics-tied) ---
        switch (state)
        {
            case PlayerState.Idle: HandleIdle(); break;
            case PlayerState.Run: HandleRun(); break;
            case PlayerState.Jump: HandleJump(); break;
            case PlayerState.Fall: HandleFall(); break;

            //case PlayerState.Turn: HandleTurn(); break;
            //case PlayerState.Backflip: HandleBackflip(); break;

            case PlayerState.WallSlide: HandleWallSlide(); break;
            case PlayerState.WallJump: HandleWallJump(); break;

            case PlayerState.LedgeHang: HandleLedgeHang(); break;
            case PlayerState.LedgeClimb: HandleLedgeClimb(); break;

            case PlayerState.Dash: HandleDash(); break;
            case PlayerState.Wavedash: HandleWavedash(); break;
            case PlayerState.Wallrun: HandleWallRun(); break;

            case PlayerState.Attack: HandleAttack(); break;
            case PlayerState.WallClimb: HandleWallClimb(); break;
        }

        MomentumHandler();
    }

    

    void HandleIdle()
    {
        // Start moving
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            ChangeState(PlayerState.Run);
            return;
        }

        // Jump
        if (TryJump())
        {
            Jump(jumpForce);
            ChangeState(PlayerState.Jump);
            return;
        }

        // Airborne
        if (!isGrounded)
            ChangeState(PlayerState.Fall);
    }

    void HandleRun()
    {
        // Stop moving
        if (Mathf.Abs(horizontalInput) < 0.1f)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        /*if (Mathf.Sign(horizontalInput) != facingDirection)
        {
            ChangeState(PlayerState.Turn);
            return;
        }*/

        // Jump
        if (TryJump())
        {
            Jump(jumpForce);
            ChangeState(PlayerState.Jump);
            return;
        }

        // Airborne
        if (!isGrounded)
            ChangeState(PlayerState.Fall);
    }

    void HandleJump()
    {
        // Start falling
        if (rb.linearVelocity.y < 0)
            ChangeState(PlayerState.Fall);

        // Jump cancelled
        if (!jumpHeld && rb.linearVelocity.y > 0)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    void HandleFall()
    {
        // Touch ground
        if (isGrounded)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        // Move into wall
        if (onWall && horizontalInput != 0)
        {
            ChangeState(PlayerState.WallSlide);
            return;
        }

        // Jump
        if (TryJump())
        {
            Jump(jumpForce);
            ChangeState(PlayerState.Jump);
            return;
        }

        // Grab ledge
        if (nearLedge && !ledgeDetected && verticalInput != -1)
        {
            SetLedgeHang();
        }
    }

    // TODO
    void HandleTurn()
    {
        facingDirection *= -1;

        rb.linearVelocity = new Vector2(turnBoost * moveSpeed * facingDirection, rb.linearVelocity.y);

        if (TryJump())
        {
            // Backflip jump
            ChangeState(PlayerState.Backflip);
            return;
        }

        if (Mathf.Abs(horizontalInput) > 0.1f)
            ChangeState(PlayerState.Run);
    }

    // TODO
    void HandleBackflip()
    {
        if (rb.linearVelocity.y < 0)
            ChangeState(PlayerState.Fall);
    }

    void HandleWallSlide()
    {
        // Wall sliding
        if (rb.linearVelocity.y < -wallSlideSpeed)
        {
            if (verticalInput == -1)
                rb.linearVelocity = new Vector2(0, -wallSlideHoldDownSpeed);
            else
                rb.linearVelocity = new Vector2(0, -wallSlideSpeed);
        }

        // No longer on wall
        if (!onWall || horizontalInput == 0)
        {
            ChangeState(PlayerState.Fall);
            return;
        }

        // Wall Jump
        if (!isGrounded && jumpPressedWithBuffer)
        {
            StartWallJump(wallJumpHorizontalBoost);
            ChangeState(PlayerState.WallJump);
        }

        // Grab ledge
        if (nearLedge && !ledgeDetected)
        {
            SetLedgeHang();
        }

        // Touch ground
        if (isGrounded)
        {
            ChangeState(PlayerState.Run);
            return;
        }
        
    }

    void HandleWallJump()
    {
        if (Time.time > wallJumpLockTimer)
        {
            rb.linearVelocity = new Vector2(airMoveSpeed, rb.linearVelocity.y);
            if (rb.linearVelocity.y < 0)
                ChangeState(PlayerState.Fall);
            else if (isGrounded)
                ChangeState(PlayerState.Idle);
        }
    }

    void HandleLedgeHang()
    {
        // Lock player in place
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        transform.position = ledgeHangPoint;

        // Jump up
        if (jumpPressedWithBuffer)
        {
            rb.gravityScale = gravityScale;
            Jump(ledgeJumpForce);
            ledgeDetected = false;
            ChangeState(PlayerState.Jump);
            return;
        }

        // Climb up
        if (verticalInput > 0)
        {
            ChangeState(PlayerState.LedgeClimb);
            StartCoroutine(ClimbLedge());
            return;
        }

        // Drop down
        if (verticalInput < 0)
        {
            rb.gravityScale = gravityScale;
            ledgeDetected = false;
            ChangeState(PlayerState.Fall);
            return;
        }

        // Move away from wall
        if ((horizontalInput > 0f && wallPosition.x < transform.position.x) 
            || (horizontalInput < 0f && wallPosition.x > transform.position.x))
        {
            rb.gravityScale = gravityScale;
            ledgeDetected = false;
            ChangeState(PlayerState.Fall);
            return;
        }
    }

    void HandleLedgeClimb()
    {
        // Jump up
        if (jumpPressedWithBuffer)
        {
            rb.gravityScale = gravityScale;
            Jump(ledgeJumpForce);
            ledgeDetected = false;
            ChangeState(PlayerState.Jump);
            return;
        }
    }

    void HandleDash()
    {
        // Wavedash
        if (midAirDash && dashDirection.y < 0 && groundedTime >= waveDashGroundedTime && TryJump())
        {
            Jump(waveDashForce);
            ChangeState(PlayerState.Wavedash);
            return;
        }

        // Wall run
        if (isGrounded && onWall)
        {
            rb.linearVelocity = new Vector2(0f, minWallRunForce + wallRunMultiplier * dashTimer);
            ChangeState(PlayerState.Wallrun);
            return;
        }

        dashTimer -= Time.deltaTime;
        lastGroundedTime = -999f;

        // End dash
        if (dashTimer <= 0f)
        {
            if (dashDirection.y < 0)
                rb.linearVelocity = new Vector2(facingDirection * moveSpeed, -1 * moveSpeed);
            else if (dashDirection.y > 0)
                rb.linearVelocity = new Vector2(facingDirection * moveSpeed, 1f);
            else
                rb.linearVelocity = new Vector2(facingDirection * moveSpeed, rb.linearVelocity.y);

            if (isGrounded)
                ChangeState(PlayerState.Idle);
            else
                ChangeState(PlayerState.Fall);
        }
    }

    void HandleWavedash()
    {
        // Touch ground
        if (isGrounded)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        // Lost momentum (hit obstacle or something)
        if (Mathf.Abs(rb.linearVelocity.x) <= airMoveSpeed)
        {
            ChangeState(PlayerState.Fall);
            return;
        }

        // Move in the opposite direction
        if (MoveOppositeDirection())
        {
            rb.linearVelocity += new Vector2(-facingDirection * waveDashResist * Time.fixedDeltaTime, 0f);
        }

        // Jump cancelled
        if (!jumpHeld && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
    }

    void HandleWallRun()
    {
        // Start falling
        if (rb.linearVelocity.y < 0 || !onWall)
        {
            ChangeState(PlayerState.Fall);
            return;
        }

        // Touch ground
        if (isGrounded)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        // Wall Jump
        if (!isGrounded && jumpPressedWithBuffer)
        {
            StartWallJump(Mathf.Max(wallJumpHorizontalBoost, wallJumpHorizontalBoost * wallRunJumpMultiplier * rb.linearVelocity.y));
            ChangeState(PlayerState.WallJump);
            return;
        }
    }

    void HandleAttack()
    {
        if (attack_timer < 0)
        {
            // Grounded or not
            if (isGrounded)
            {
                ChangeState(PlayerState.Idle);
                return;
            }
            else
            {
                ChangeState(PlayerState.Fall);
                return;
            }              
        }
    }

    void HandleWallClimb()
    {
        if (CheckWallDuringAttack(out RaycastHit2D hit))
        {
            wall_climb_wall_pos = hit;
            Debug.DrawLine(transform.position, wall_climb_wall_pos.point);
        }

        // ---------------- MOVEMENT INPUT ----------------
        Vector2 input = new Vector2(horizontalInput, verticalInput).normalized;
        Vector2 desiredVelocity = input * wallMoveSpeed;
        Vector2 nextPos = (Vector2)transform.position + desiredVelocity * Time.fixedDeltaTime;

        Vector2 offset = nextPos - wall_climb_wall_pos.point;
        float distance = offset.magnitude;

        // TODO: fix priority --V
        float newWallCheckDistance = wallCheckDistance + secondSphereCastOffset;

        //  INSIDE THE CIRCLE? MOVE NORMALLY
        if (distance <= newWallCheckDistance)
        {
            rb.linearVelocity = desiredVelocity;
            return;
        }

        // Get nearest valid point ON the circle
        Vector2 clampedPos = wall_climb_wall_pos.point + offset.normalized * newWallCheckDistance;

        // SLERP toward it instead of snapping
        transform.position = (Vector2)Vector3.Slerp(transform.position, clampedPos, wallMoveSpeed * 4f * Time.deltaTime);
        rb.linearVelocity = Vector2.zero;  // Stay floating


        if (jumpPressedWithBuffer)
        {
            Jump(jumpForce);
            ChangeState(PlayerState.Jump);
            return;
        }
    }

    public void StruckWallWithAttack()
    {
        if (state == PlayerState.Attack)
        {
            if (CheckWallDuringAttack(out RaycastHit2D hit))
            {
                wall_climb_wall_pos = hit;
                rb.linearVelocity = Vector3.zero;
                rb.gravityScale = 0f;
                state = PlayerState.WallClimb;
            }
        }
    }

    void MomentumHandler()
    {
        // Disable under certain conditions
        if (state == PlayerState.WallJump && Time.time < wallJumpLockTimer)
            return;
        if (state == PlayerState.LedgeHang || state == PlayerState.Dash || state == PlayerState.Wavedash || state == PlayerState.WallClimb)
            return;

        bool grounded = (state == PlayerState.Idle || state == PlayerState.Run || state == PlayerState.Turn);
        float targetSpeed = dashHeldInput ? runningDashSpeed : (grounded ? moveSpeed : airMoveSpeed);
        Vector2 velocity = rb.linearVelocity;


        velocity.x = horizontalInput * targetSpeed;

        rb.linearVelocity = new Vector2(velocity.x, velocity.y);
    }

    void SetLedgeHang()
    {
        // Find exact ledge position
        RaycastHit2D hit = Physics2D.Raycast(transform.position + Vector3.up * ledgeCheckLower, Vector2.right * facingDirection, wallCheck, GroundLayer);
        if (hit)
        {
            ledgeHangPoint = new Vector2(hit.point.x - ((col.bounds.extents.x - ledgeOffset.x) * facingDirection), hit.point.y - col.bounds.extents.y * ledgeOffset.y);
            ledgeDetected = true;
            ChangeState(PlayerState.LedgeHang);
        }
    }

    IEnumerator ClimbLedge()
    {
        Vector2 startPos = transform.position;
        Vector2 targetPos = startPos + new Vector2(
            facingDirection * ledgeClimbOffset.x,
            ledgeClimbOffset.y
        );

        // Midpoint directly above the start position, same X
        Vector2 midPos = new Vector2(startPos.x, targetPos.y);

        float duration = ledgeClimbUpTime;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            float eval = ledgeClimbCurve.Evaluate(t);

            if (t < 0.5f)
            {
                // Go upward
                float subT = eval / 0.5f; // normalize within first half
                transform.position = Vector2.Lerp(startPos, midPos, subT);
            }
            else
            {
                // Go sideways
                float subT = (eval - 0.5f) / 0.5f; // normalize second half
                transform.position = Vector2.Lerp(midPos, targetPos, subT);
            }

            yield return null;
        }

        transform.position = targetPos;
        rb.gravityScale = gravityScale;
        ledgeDetected = false;
        ChangeState(PlayerState.Idle);
    }

    private void StartDash()
    {
        usedDash = true;
        dashTimer = dashDuration;
        state = PlayerState.Dash;
        
        midAirDash = !isGrounded;

        // Apply the initial boost
        Vector2 inputDir = new Vector2(horizontalInput, verticalInput);
        if (inputDir == Vector2.zero)
            inputDir = new Vector2(facingDirection, 0f); // default forward dash
        dashDirection = inputDir.normalized;
        rb.linearVelocity = dashDirection * initialDashSpeed;

    }

    private void StartAttack()
    {
        attack_timer = attackCooldown;
        state = PlayerState.Attack;

        if (verticalInput > 0f)
        {

            tailIndex = Extra_Random.GetRandomNonRepeating(shuffleIndex, out int newIndex, pool);
            shuffleIndex = newIndex;
            attackPosition = Extra_Random.RandomPointInCapsule(ranUpAttackRange_Top, ranUpAttackRange_Down, ranUpAttackRadius);
        }
        else
        {
            tailIndex = Extra_Random.GetRandomNonRepeating(shuffleIndex, out int newIndex, pool);
            shuffleIndex = newIndex;
            attackPosition = Extra_Random.RandomPointInCapsule(ranForwardAttackRange_Top, ranForwardAttackRange_Down, ranForwardAttackRadius);
        }
            
    }

    private void StartWallJump(float horizontal_boost)
    {
        // Flip facing direction
        facingDirection *= -1;

        // Push off the wall
        rb.linearVelocity = new Vector2(facingDirection * horizontal_boost, wallJumpVerticalBoost);

        // Temporarily disable air control
        wallJumpLockTimer = Time.time + wallJumpControlLock;
    }

    void Jump(float force)
    {
        float horizontalCarry = Mathf.Abs(rb.linearVelocity.x) > moveSpeed ? rb.linearVelocity.x : 0f;

        rb.linearVelocity = new Vector2(horizontalCarry, force);
        jumpHeld = true;

        lastJumpPressedTime = -999f;
        lastGroundedTime = -999f;
    }

    bool TryJump()
    {
        // Coyote time: recently left ground
        bool canUseCoyote = (Time.time - lastGroundedTime <= coyoteTime);

        if (jumpPressedWithBuffer && (isGrounded || canUseCoyote))
        {
            return true;
        }
        return false;
    }

    bool MoveOppositeDirection()
    {
        return horizontalInput == -facingDirection;
    }

    void ChangeState(PlayerState newState)
    {
        state = newState;
        rb.gravityScale = gravityScale;
        if (newState != PlayerState.LedgeHang)
            ledgeDetected = false;
    }

    int CheckFacingDirection()
    {
        if (state == PlayerState.Dash || state == PlayerState.Wavedash)
            return facingDirection;

        if (horizontalInput > 0)        return 1;
        else if (horizontalInput < 0)   return -1;
        
        return facingDirection;
    }

    bool CheckWall(Bounds bounds)
    {
        Vector2 origin = bounds.center;
        Vector2 dir = new Vector2(facingDirection, 0f);
        var hit = Physics2D.Raycast(origin, dir, wallCheck, GroundLayer);
        if (hit.collider != null) wallPosition = hit.point;
        Debug.DrawRay(origin, dir * wallCheck, Color.magenta);
        return hit.collider != null;
    }

    bool CheckWallDuringAttack(out RaycastHit2D hit)
    {
        RaycastHit2D hit1 = Physics2D.CircleCast(transform.position, wallCheckDistance, Vector2.one * 0.001f, 0.001f, GroundLayer);
        RaycastHit2D hit2 = Physics2D.CircleCast(transform.position + Vector3.down * secondSphereCastOffset, wallCheckDistance, Vector2.one * 0.001f, 0.001f, GroundLayer);

        if (hit1)
        {
            hit = hit1;
            return true;
        }
        if (hit2)
        {
            hit = hit2;
            return true;
        }
        hit = hit1;
        return false;
    }

    bool CheckLedge(Bounds bounds)
    {
        Vector2 origin = bounds.center;
        Vector2 dir = new Vector2(facingDirection, 0f);

        Vector2 lowerOrigin = origin + Vector2.up * ledgeCheckLower;
        RaycastHit2D lowerHit = Physics2D.Raycast(lowerOrigin, dir, ledgeCheckHorizontal, GroundLayer);

        Vector2 upperOrigin = origin + Vector2.up * ledgeCheckUpper;
        RaycastHit2D upperHit = Physics2D.Raycast(upperOrigin, dir, ledgeCheckHorizontal, GroundLayer);

        Debug.DrawRay(lowerOrigin, dir * ledgeCheckHorizontal, lowerHit.collider == null ? Color.red : Color.green);
        Debug.DrawRay(upperOrigin, dir * ledgeCheckHorizontal, upperHit.collider == null ? Color.red : Color.green);

        // near ledge if lower hits but upper doesn't
        return lowerHit.collider != null && upperHit.collider == null;
    }

    bool CheckGrounded(Bounds bounds)
    {
        // BoxCast downward a small distance to check for ground
        Vector2 boxCenter = bounds.center;
        Vector2 boxSize = bounds.size;
        RaycastHit2D hit = Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.down, groundedBuffer, GroundLayer);

        // draw base debug rays for feet
        Debug.DrawRay(bounds.center + new Vector3(bounds.extents.x, 0f), Vector2.down * (bounds.extents.y + groundedBuffer), Color.cyan);
        Debug.DrawRay(bounds.center - new Vector3(bounds.extents.x, 0f), Vector2.down * (bounds.extents.y + groundedBuffer), Color.cyan);

        if (hit.collider == null)
            return false;

        // protect against trigger-only colliders
        if (hit.collider.isTrigger)
            return false;

        // slope: angle between hit.normal and up
        float angle = Vector2.Angle(hit.normal, Vector2.up);
        // if slope angle less than or equal to slopeLimit -> walkable
        if (angle <= slopeLimit)
        {
            Debug.DrawRay(hit.point, hit.normal, Color.yellow);
            return true;
        }
        else
            return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wallCheckDistance);
        Gizmos.DrawWireSphere(transform.position + Vector3.down * secondSphereCastOffset, wallCheckDistance);
    }

}
