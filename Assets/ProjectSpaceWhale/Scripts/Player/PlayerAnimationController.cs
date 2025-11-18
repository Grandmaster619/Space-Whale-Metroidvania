using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Player Movement")]
    [SerializeField] Transform player;
    [SerializeField] Player_Movement playerMovement;
    [SerializeField] float maxPlayerRotation;
    private PlayerState state;
    private int facingDirection;
    Vector3 rotation_target;

    [Header("Behavior Array")]
    [SerializeField] PlayerAnimationBehavior[] behaviors;

    [Header("Speeds")]
    [SerializeField] float rotationSpeed;




    // Update is called once per frame
    void Update()
    {
        state = playerMovement.GetPlayerState();
        facingDirection = playerMovement.GetFacingDirection();

        // Rotate
        rotation_target = player.rotation.eulerAngles;
        rotation_target.y = maxPlayerRotation * facingDirection;
        SlerpRotate(player, Quaternion.Euler(rotation_target), rotationSpeed);

        foreach (var behavior in behaviors)
        {
            // --- State Machine ---
            switch (state)
            {
                case PlayerState.Idle: behavior.Idle(playerMovement); break;
                //case PlayerState.Run: HandleRun(); break;
                //case PlayerState.Jump: HandleJump(); break;
                //case PlayerState.Fall: HandleFall(); break;

                //case PlayerState.Turn: HandleTurn(); break;
                //case PlayerState.Backflip: HandleBackflip(); break;

                //case PlayerState.WallSlide: HandleWallSlide(); break;
                //case PlayerState.WallJump: HandleWallJump(); break;

                //case PlayerState.LedgeHang: HandleLedgeHang(); break;
                //case PlayerState.LedgeClimb: HandleLedgeClimb(); break;

                //case PlayerState.Dash: HandleDash(); break;
                //case PlayerState.Wavedash: HandleWavedash(); break;
                //case PlayerState.Wallrun: HandleWallRun(); break;

                case PlayerState.Attack: behavior.Attack(playerMovement); break;
                default: behavior.Default(playerMovement); break;
            }
        }
    }

    private void SlerpRotate(Transform current, Quaternion target, float speed)
    {
        current.rotation = Quaternion.Slerp(current.rotation, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }
}

public abstract class PlayerAnimationBehavior : MonoBehaviour
{
    public void LinearMove(Transform current, Vector3 target, float speed)
    {
        current.localPosition = Vector3.MoveTowards(current.localPosition, target, speed * Time.deltaTime);
    }

    public abstract void Idle(Player_Movement playerMovement);

    public abstract void Attack(Player_Movement playerMovement);

    public abstract void Default(Player_Movement playerMovement);
}
