using UnityEngine;

public class PlayerTails : PlayerAnimationBehavior
{
    [Header("Tail Chains")]
    [SerializeField] Transform[] TailArray = new Transform[4];
    private Vector3[] starting_pos_array = new Vector3[4];
    private Vector3[] random_pos_array = new Vector3[4];
    [SerializeField] float random_pos_radius = 0.5f;

    [Header("Speeds")]
    [SerializeField] float tailIdleSpeed;
    [SerializeField] float tailAttackSpeed = 0.3f;
    [SerializeField] float tailReturnSpeed = 0.2f;

    [Header("Attack colliders")]
    [SerializeField] PlayerAttackCollider[] attackColliders = new PlayerAttackCollider[4];


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < TailArray.Length; i++)
        {
            starting_pos_array[i] = TailArray[i].localPosition;
            random_pos_array[i] = starting_pos_array[i];
        }
    }

    public override void Default(Player_Movement playerMovement)
    {
        Idle(playerMovement);
    }

    public override void Idle(Player_Movement playerMovement)
    {
        for (int i = 0; i < TailArray.Length; i++)
        {
            TailInIdle(i);
        }
    }

    public override void Attack(Player_Movement playerMovement)
    {
        float attackCooldown = playerMovement.GetAttackCooldown();
        float attackTimer = playerMovement.GetAttackTimer();
        int tailIndex = playerMovement.GetTailIndex();

        for (int i = 0; i < TailArray.Length; i++)
        {
            if (i == tailIndex && attackTimer > 0f)
            {
                attackColliders[i].UpdatePosition();
                LinearMove(TailArray[tailIndex], playerMovement.GetAttackPosition(), tailAttackSpeed);
            }
            else
            {
                TailInIdle(i);
            }
        }

    }

    private void TailInIdle(int i)
    {
        if (Vector3.Distance(TailArray[i].localPosition, starting_pos_array[i]) > random_pos_radius)
        {
            LinearMove(TailArray[i], starting_pos_array[i], tailReturnSpeed);
        }
        else
        {
            LinearMove(TailArray[i], random_pos_array[i], tailIdleSpeed);
            if (TailArray[i].localPosition == random_pos_array[i])
            {
                random_pos_array[i] = Extra_Random.RandomPointInSphere(starting_pos_array[i], random_pos_radius);
            }
        }      
    }
}
