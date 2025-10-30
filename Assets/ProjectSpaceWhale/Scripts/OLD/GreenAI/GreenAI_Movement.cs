using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GreenAI_Movement : MonoBehaviour
{
    public LayerMask GroundLayer;
    public Transform frontCheck;
    public Transform horn;

    private SpriteRenderer _renderer;
    private BoxCollider2D boxCollider2d;
    [HideInInspector] public Rigidbody2D rb2d;

    [Space]
    [Header("Debug Info")]
    public float airTime; // Time spent in the air
    public float groundTime; // Time spent on the ground
    public bool usedGroundJump = false; // If player jumped off a wall or off the ground
    public bool StandingOnCreature = false;
    public bool isTouchingCreature = false;

    // These variables do not need to be in the inspector but could be needed as public variables
    public int playerDirection = 1; // Direction player is facing with 1 (right) and -1 (left)
    public int jumpsLeft; // Current amount of midair jumps player can perform before touching the ground
    public bool isJumping; // If player is in the air
    public bool isGrounded; // If player is on the ground
    public bool wallSliding; // If player is wall sliding
    public bool inDash = false; // Player is in dash animation
    public bool jumpKeyHeld; // Self explanitory
    public bool dashGroundCheck = true; // If player touched the ground after a dash
    public bool wallJumping = false; // If player is wall jumping
    public bool isTouchingFront; // If player is touching a wall
    public bool touchedGroundFirstFrame; // Used to help update amount of jumps player has

    [Space]
    [Header("Player Movement")]
    public float moveSpeed; // Player movement speed
    public float slopeLimit = 58f; // Maximum slope angle the player considers ground and can jump off of
    [Space]
    [Header("Jumping")]
    public float jumpHeight; // Should be the maximum height the player can jump to by pixels
    public float midairJumpHeight; // Maximum height the player can jump midair
    public float wallJumpHeight; // Maximum height the player can jump off a wall
    public int midairJumpsAmount; // Maximum jumps a player can perform before touching the ground 

    [Space]
    [Header("Dashing")]
    public float dashSpeed; // Player speed while in a dash
    public float dashLength; // How long the player is in a dash
    public float dashCooldown; // How long before the player can perform another dash
    public bool canDash = true; // If player has permission to dash
    [Space]
    [Header("Wall Jump & Wall Slide")]
    public float wallSlidingSpeed; // Speed the player falls when sliding down a wall
    public float wallJumpingDuration; // Length of time player will wall jump (wall jump animation)
    public float wallJumpForce; // Force applied to player to push them off a wall when wall jumping
    public bool canWallJump; // If player has permission to wall slide and wall jump
    [Space]
    [Header("Attacking")]
    public float attackSpeed = 6f; // Speed that attack extends and retracts

    [Space]
    // All variables below should not be changed
    private float jumpForce; // Base amount of velocity when the player jumps
    private float extraHeightText = 0.1f; // Used for ground check box collider
    private float time = 0.0f; // Variable for IEnumerator methods
    private float frontCheckOffset = 0.3f;
    

    private float moveInput; // Player inputed left or right
    private bool dashInput; // Player inputed a dash
    private bool jumpInputDown; // Player inputed a jump
    private bool jumpInputUp;
    private bool attackInput = false;

    // Start is called before the first frame update
    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        jumpForce = CalculateJumpForce(Physics2D.gravity.magnitude, jumpHeight);
        boxCollider2d = transform.GetComponent<BoxCollider2D>();
        _renderer = GetComponent<SpriteRenderer>();
        
    }

    // Update is called once per frame
    public void AIMove(float move, bool dash, bool jumpDown, bool jumpUp, bool attack)
    {
        moveInput = move;
        dashInput = dash;
        jumpInputDown = jumpDown;
        jumpInputUp = jumpUp;
        attackInput = attack;
        //Debug.Log(moveInput);

        if (Time.deltaTime == 0)
            return;
        
        if (!isGrounded && !wallSliding)
        {
            Invoke("ResetTouchedGround", 0.1f);
        }

        if (!inDash)
        {

            //Left and right movement
            Move();

            // Player orientation (if they are facing left or right)
            Flip(moveInput);

            isTouchingFront = Physics2D.OverlapCircle(frontCheck.position, 0.15f, GroundLayer);
            if (canWallJump)
            {
                // Wall sliding and wall jumping
                WallSlide();
            }

            if (jumpInputDown)
            {
                // Player jumping
                Jump();
            }
            else if (jumpInputUp)
            {
                jumpKeyHeld = false;
            }

            if (dashInput && canDash && dashGroundCheck)
            {
                // Dashing
                StartCoroutine(Dash());
            }

            // Attacking
            Attack();

        }

        if (!isGrounded)
            airTime += Time.deltaTime;
        else
            groundTime += Time.deltaTime;

        CheckIfGrounded();

    }

    private void FixedUpdate()
    {
        if (!inDash && isJumping)
        {
            if (!jumpKeyHeld && !wallSliding && rb2d.linearVelocity.y > 0)
            {
                // Jumping counter force (if player releases jump button early)
                //rb2d.AddForce(transform.up * counterJumpForce * rb2d.mass);
                rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
            }
        }
    }

    public void Move()
    {
        if (!wallJumping)
        {
            rb2d.linearVelocity = new Vector2(moveInput * moveSpeed, rb2d.linearVelocity.y);
        }
    }

    private void Attack()
    {
        Vector2 MaxScale = new Vector2(horn.localScale.x, 3.0f * playerDirection);
        Vector3 MaxPosition = new Vector3(0.52f * playerDirection, 0f, 0.17f);
        Vector2 MinScale = new Vector2(horn.localScale.x, 0.5f * playerDirection);
        Vector3 MinPosition = new Vector3(0f, 0f, 0.17f);

        if (attackInput)
        {
            horn.localScale = Vector2.Lerp(horn.localScale, MaxScale, attackSpeed * Time.deltaTime);
            horn.localPosition = Vector3.Lerp(horn.localPosition, MaxPosition, attackSpeed * Time.deltaTime);
        }
        else
        {
            horn.localScale = Vector2.Lerp(horn.localScale, MinScale, attackSpeed * Time.deltaTime);
            horn.localPosition = Vector3.Lerp(horn.localPosition, MinPosition, attackSpeed * Time.deltaTime);
        }

        if (Mathf.Abs(horn.localScale.y) < 0.6f && !attackInput)
            horn.gameObject.SetActive(false);
        else horn.gameObject.SetActive(true);
    }

    public IEnumerator Dash()
    {
        canDash = false;
        dashGroundCheck = false;
        inDash = true;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        yield return new WaitForEndOfFrame();
        //initial dash
        do
        {
            if (rb2d.linearVelocity != null)
            {
                rb2d.linearVelocity = new Vector2(playerDirection * dashSpeed, 0);
            }

            time += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        } while (time < dashLength);

        rb2d.bodyType = RigidbodyType2D.Dynamic;
        inDash = false;

        //dash cooldown
        do
        {
            time += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        } while (time < dashCooldown);

        canDash = true;
        time = 0.0f;
    }

    public void Flip(float direction)
    {
        if (direction > 0)
        {
            // Player is moving right
            playerDirection = 1;
            _renderer.flipX = false;
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            frontCheck.position = new Vector2(boxCollider2d.bounds.center.x + frontCheckOffset, frontCheck.position.y);
        }
        if (direction < 0)
        {
            // Player is moving left
            playerDirection = -1;
            _renderer.flipX = true;
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            frontCheck.position = new Vector2(boxCollider2d.bounds.center.x - frontCheckOffset, frontCheck.position.y);
        }
        if (direction == 0 && isGrounded && !isJumping && !StandingOnCreature)
        {
            Invoke("Idle", 0.1f);
        }
    }

    private void Idle()
    {
        if (moveInput == 0 && isGrounded && !isJumping && !inDash && !isTouchingCreature)
        {

            rb2d.linearVelocity = Vector2.zero;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    public void Jump()
    {
        jumpKeyHeld = true;
        isJumping = false;
        if ((!usedGroundJump || jumpsLeft > 0) && jumpsLeft >= 0)
        {
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            isJumping = true;
            if (wallSliding) // Player jumps on a wall
            {
                rb2d.AddForce(-1 * wallJumpForce * moveSpeed * playerDirection * Vector2.right, ForceMode2D.Impulse);
                wallJumping = true;
                Invoke("setWallJumpingToFalse", wallJumpingDuration);

                jumpForce = CalculateJumpForce(Physics2D.gravity.magnitude, wallJumpHeight);
                rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
                rb2d.AddForce(Vector2.up * jumpForce * rb2d.mass, ForceMode2D.Impulse);
                usedGroundJump = true;
            }
            else if (isGrounded) // Player jumps on the ground
            {
                jumpForce = CalculateJumpForce(Physics2D.gravity.magnitude, jumpHeight);
                rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
                rb2d.AddForce(Vector2.up * jumpForce * rb2d.mass, ForceMode2D.Impulse);
                usedGroundJump = true;
            }
            else // Player jumps midair
            {
                jumpForce = CalculateJumpForce(Physics2D.gravity.magnitude, midairJumpHeight);
                rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, 0f);
                rb2d.AddForce(Vector2.up * jumpForce * rb2d.mass, ForceMode2D.Impulse);
                jumpsLeft--;
            }
            //Debug.Log(jumpsLeft + " " + usedGroundJump);
        }
    }

    public void WallSlide()
    {
        if (isTouchingFront && !isGrounded && moveInput != 0 && rb2d.linearVelocity.y < 0)
        {
            if (!touchedGroundFirstFrame)
            {
                TouchedWall();
            }
            touchedGroundFirstFrame = true;
            wallSliding = true;
        }
        else
        {
            //Invoke("setWallSlidingToFalse", 0.1f);
            // Possible Fix: add a slight amount of leniency when turning around and jumping off a wall so it isn't counted as a midair jump
            wallSliding = false;
        }

        if (wallSliding)
        {
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, Mathf.Clamp(rb2d.linearVelocity.y, -wallSlidingSpeed, float.MaxValue));
        }
    }
    private void setWallJumpingToFalse()
    {
        wallJumping = false;
    }

    public void CheckIfGrounded()
    {
        if (StandingOnCreature)
        {
            TouchedGround();
            isJumping = false;
            return;
        }

        RaycastHit2D raycastHit = Physics2D.BoxCast(boxCollider2d.bounds.center, boxCollider2d.bounds.size, 0f, Vector2.down, extraHeightText, GroundLayer);
        Color rayColor;
        float myAngle = 90 - (Vector3.Angle(raycastHit.normal, Vector3.up));
        if (raycastHit.collider != null || (myAngle < 5.0f))
        {
            Debug.DrawRay(raycastHit.point, raycastHit.normal, Color.yellow);
            //Debug.Log(myAngle);

            if (System.Math.Abs(myAngle - 90f) < slopeLimit)
            {
                // The ground the player is standing on is solid ground that doesn't exceed the slope limit
                rayColor = Color.green;
                
                if (!touchedGroundFirstFrame)
                {
                    TouchedGround();
                }
                touchedGroundFirstFrame = true;
                dashGroundCheck = true;
                if (rb2d.linearVelocity.y > 0)
                {
                    isJumping = true;
                }
            }
            else
            {
                // Ground is too steep to stand on
                rayColor = Color.red;
                isGrounded = false;
                groundTime = 0.0f;
                rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x * -2f, -5f);
                isJumping = true;
                if (!wallSliding)
                {
                    usedGroundJump = true;
                }
            }

        }
        else
        {
            // Player is not touching any ground (meaning they should be airborne)
            rayColor = Color.red;
            isGrounded = false;
            groundTime = 0.0f;
            isJumping = true;
            if (!wallSliding)
            {
                usedGroundJump = true;
            }
        }
        Debug.DrawRay(boxCollider2d.bounds.center + new Vector3(boxCollider2d.bounds.extents.x, 0), Vector2.down * (boxCollider2d.bounds.extents.y + extraHeightText), rayColor);
        Debug.DrawRay(boxCollider2d.bounds.center - new Vector3(boxCollider2d.bounds.extents.x, 0), Vector2.down * (boxCollider2d.bounds.extents.y + extraHeightText), rayColor);
        Debug.DrawRay(boxCollider2d.bounds.center - new Vector3(boxCollider2d.bounds.extents.x, boxCollider2d.bounds.extents.y + extraHeightText), Vector2.right * (boxCollider2d.bounds.extents.x + extraHeightText), rayColor);

    }

    public void TouchedGround()
    {
        jumpsLeft = midairJumpsAmount;
        usedGroundJump = false;
        isGrounded = true;
        isJumping = false;
        airTime = 0.0f;
    }

    public void TouchedWall()
    {
        jumpsLeft = midairJumpsAmount;
        usedGroundJump = false;
        dashGroundCheck = true;
        airTime = 0.0f;
    }

    public void ResetTouchedGround()
    {
        touchedGroundFirstFrame = false;
    }

    public static float CalculateJumpForce(float gravityStrength, float jumpHeight)
    {
        return Mathf.Sqrt(2 * gravityStrength * jumpHeight);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("CreatureHead"))
        {
            rb2d.bodyType = RigidbodyType2D.Dynamic;
            isTouchingCreature = true;
            float y_distance = collision.collider.transform.position.y - transform.position.y;
            if (y_distance < 0.0f)
            {
                StandingOnCreature = true;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("CreatureHead"))
        {
            StandingOnCreature = false;
            isTouchingCreature = false;
        }
    }
}
