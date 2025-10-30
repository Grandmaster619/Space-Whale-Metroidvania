using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Eyesight : Sensor
{
    [Header("Vision")]
    public float viewRadius = 12f;
    [Range(0f, 360f)] public float fov = 120f;
    public Vector2 eyeOffset = Vector2.zero;
    public float lookDirectionZ = 0f;           // local rotation offset in degrees (applied to transform.right)
    public float turnSpeed = 240f;

    [Header("Stability")]
    public float seenPersistSeconds = 0.25f;

    [Header("Tracking")]
    public int maxTrackedTargets = 3;

    [HideInInspector] public float cosHalfFov;
    [HideInInspector] public Collider2D ownCollider;


    void Start()
    {
        cosHalfFov = Mathf.Cos(0.5f * fov * Mathf.Deg2Rad);

        ownCollider = GetComponent<Collider2D>();

        // register self in VisionManager if on targetMask
        if (ownCollider != null && VisionManager.Instance != null)
        {
            if ((VisionManager.Instance.targetMask.value & (1 << ownCollider.gameObject.layer)) != 0)
                VisionManager.Instance.RegisterTarget(ownCollider);
        }

        VisionManager.Register(this);
    }

    void OnDisable()
    {
        if (ownCollider != null && VisionManager.Instance != null)
            VisionManager.Instance.UnregisterTarget(ownCollider);

        VisionManager.Unregister(this);
    }

    public void GetEye(out Vector2 eyePos, out Vector2 forward)
    {
        eyePos = (Vector2)transform.position + eyeOffset;
        forward = Quaternion.Euler(0f, 0f, lookDirectionZ) * transform.right;
        forward.Normalize();
    }

    public void ChangeLookDirectionToTarget(Vector3 target)
    {
        Vector2 directionToTarget = (target - transform.position).normalized;
        float angle = Vector2.SignedAngle(transform.right, directionToTarget);
        lookDirectionZ = Mathf.MoveTowardsAngle(lookDirectionZ, angle, turnSpeed * Time.deltaTime);

    }

}