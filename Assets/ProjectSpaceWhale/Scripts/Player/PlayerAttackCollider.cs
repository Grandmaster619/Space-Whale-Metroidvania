using FMOD;
using UnityEngine;

public class PlayerAttackCollider : MonoBehaviour
{
    [SerializeField] Player_Movement player_Movement;
    [SerializeField] Transform CenterTarget;
    [SerializeField] float radius;
    CircleCollider2D Collider;

    private void Start()
    {
        Collider = GetComponent<CircleCollider2D>();
        Collider.radius = radius;
    }

    public void UpdatePosition()
    {
        gameObject.transform.position = new Vector3(CenterTarget.position.x, CenterTarget.position.y, 0f);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Wall"))
        {
            player_Movement.StruckWallWithAttack();
        }
    }
}
