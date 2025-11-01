using System;
using System.Collections;
using UnityEngine;

public class Player_Movement : MonoBehaviour
{
    // ------------ Enums -------------------------
    private enum PlayerState
    {
        Idle,
        Run,
        Jump,
        Fall,
        Turn,
        WallSlide,
        WallJump,
        LedgeHang,
        LedgeClimb,
        Backflip,
        Dash
    }

    // ------------- Inspector Fields -------------------------
    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float airMoveSpeed = 4.5f;
    [SerializeField] float jumpForce = 14f;
    [SerializeField] float wallSlideSpeed = 2.5f;
    [SerializeField] float turnBoost = 1.25f;

    [SerializeField] float wallJumpHorizontalBoost = 10f;
    [SerializeField] float wallJumpVerticalBoost = 12f;
    [SerializeField] float wallJumpControlLock = 0.15f;

    [SerializeField] float ledgeJumpForce = 12f;

    [SerializeField] float groundFriction = 5f;

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

    [SerializeField] float ledgeClimbUpTime = 0.3f;
    [SerializeField] Vector2 ledgeClimbOffset = new Vector2(0.5f, 1.2f); // forward, up
    [SerializeField] AnimationCurve ledgeClimbCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Dash Settings")]
    [SerializeField] bool dashUnlocked = true;
    [SerializeField] float initialDashSpeed = 15f;
    [SerializeField] float runningDashSpeed = 5;
    [SerializeField] float dashDuration = 0.25f;

    

    // -------------- Components -------------------------
    private Rigidbody2D rb;
    private BoxCollider2D col;


    // --------------- State Data ---------------------------
    private PlayerState state = PlayerState.Idle;
    private float horizontalInput;
    private float verticalInput;
    private bool dashPressedInput;
    private bool dashHeldInput;
    private int facingDirection;

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

    // --------------- Random ---------------------------
    Vector3 wallPosition;

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

        // record grounded time for coyote
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            usedDash = false;
        }

        // --- State Machine (physics-tied) ---
        switch (state)
        {
            case PlayerState.Idle: HandleIdle(); break;
            case PlayerState.Run: HandleRun(); break;
            case PlayerState.Jump: HandleJump(); break;
            case PlayerState.Fall: HandleFall(); break;
            //case PlayerState.Turn: HandleTurn(); break;
            case PlayerState.WallSlide: HandleWallSlide(); break;
            case PlayerState.WallJump: HandleWallJump(); break;
            case PlayerState.LedgeHang: HandleLedgeHang(); break;
            case PlayerState.LedgeClimb: HandleLedgeClimb(); break;
            //case PlayerState.Backflip: HandleBackflip(); break;
            case PlayerState.Dash: HandleDash(); break;
        }

        MomentumHandler();
    }

    

    void HandleIdle()
    {
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            ChangeState(PlayerState.Run);
            return;
        }

        if (TryJump())
        {
            Jump();
            ChangeState(PlayerState.Jump);
            return;
        }

        if (!isGrounded)
            ChangeState(PlayerState.Fall);
    }

    void HandleRun()
    {
        if (Mathf.Abs(horizontalInput) < 0.1f)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        if (Mathf.Sign(horizontalInput) != facingDirection)
        {
            //ChangeState(PlayerState.Turn);
            //return;
        }

        if (TryJump())
        {
            Jump();
            ChangeState(PlayerState.Jump);
            return;
        }

        if (!isGrounded)
            ChangeState(PlayerState.Fall);
    }

    void HandleTurn()
    {
        facingDirection *= -1;

        rb.linearVelocity = new Vector2(turnBoost * moveSpeed * facingDirection, rb.linearVelocity.y);

        if (TryJump())
        {
            Jump(backflip: true);
            ChangeState(PlayerState.Backflip);
            return;
        }

        if (Mathf.Abs(horizontalInput) > 0.1f)
            ChangeState(PlayerState.Run);
    }

    void HandleJump()
    {
        // start falling
        if (rb.linearVelocity.y < 0)
            ChangeState(PlayerState.Fall);

        // jump cancelled
        if (!jumpHeld && rb.linearVelocity.y > 0)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    void HandleFall()
    {
        if (isGrounded)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        if (onWall && horizontalInput != 0)
        {
            ChangeState(PlayerState.WallSlide);
            return;
        }

        if (TryJump())
        {
            Jump();
            ChangeState(PlayerState.Jump);
            return;
        }

        if (nearLedge && !ledgeDetected && verticalInput != -1)
        {
            SetLedgeHang();
        }
    }

    void HandleWallSlide()
    {
        
        if (rb.linearVelocity.y < -wallSlideSpeed)
            rb.linearVelocity = new Vector2(0, -wallSlideSpeed);

        if (!onWall)
        {
            ChangeState(PlayerState.Fall);
            return;
        }

        if (!isGrounded && jumpPressedWithBuffer)
        {
            // Flip facing direction
            facingDirection *= -1;

            // Push off the wall
            rb.linearVelocity = new Vector2(facingDirection * wallJumpHorizontalBoost, wallJumpVerticalBoost);

            // Temporarily disable air control
            wallJumpLockTimer = Time.time + wallJumpControlLock;

            ChangeState(PlayerState.WallJump);
        }

        if (nearLedge && !ledgeDetected)
        {
            SetLedgeHang();
        }

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
            rb.linearVelocity = new Vector2(0, ledgeJumpForce);
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
            rb.linearVelocity = new Vector2(0, ledgeJumpForce);
            ledgeDetected = false;
            ChangeState(PlayerState.Jump);
            return;
        }
    }

    void HandleBackflip()
    {
        if (rb.linearVelocity.y < 0)
            ChangeState(PlayerState.Fall);
    }

    void HandleDash()
    {
        dashTimer -= Time.deltaTime;

        if (dashTimer <= 0f)
        {
            if (dashDirection.y < 0)
                rb.linearVelocity = new Vector2(facingDirection * moveSpeed, -1 * moveSpeed);
            else
                rb.linearVelocity = new Vector2(facingDirection * moveSpeed, 1f);

            if (isGrounded)
                ChangeState(PlayerState.Idle);
            else
                ChangeState(PlayerState.Fall);
        }
    }

    void MomentumHandler()
    {
        // Disable under certain conditions
        if (state == PlayerState.WallJump && Time.time < wallJumpLockTimer)
            return;
        if (state == PlayerState.LedgeHang || state == PlayerState.Dash)
            return;

        bool grounded = (state == PlayerState.Idle || state == PlayerState.Run || state == PlayerState.Turn);
        float targetSpeed = grounded ? (dashHeldInput ? runningDashSpeed : moveSpeed) : airMoveSpeed;
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

        // Apply the initial boost
        Vector2 inputDir = new Vector2(horizontalInput, verticalInput);
        if (inputDir == Vector2.zero)
            inputDir = new Vector2(facingDirection, 0f); // default forward dash
        dashDirection = inputDir.normalized;
        rb.linearVelocity = dashDirection * initialDashSpeed;

    }

    void Jump(bool backflip = false)
    {
        float horizontalCarry = Mathf.Abs(rb.linearVelocity.x) > moveSpeed ? rb.linearVelocity.x : 0f;

        rb.linearVelocity = new Vector2(horizontalCarry, jumpForce);
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

    void ChangeState(PlayerState newState)
    {
        state = newState;
        rb.gravityScale = gravityScale;
        if (newState != PlayerState.LedgeHang)
            ledgeDetected = false;
    }

    private int CheckFacingDirection()
    {
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



}
