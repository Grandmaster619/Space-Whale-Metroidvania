using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class Hurtbox : MonoBehaviour
{
    public LifeFunction lifeFunction;

    

    private void OnTriggerStay2D(Collider2D collider)
    {
        if (collider.CompareTag("Horn"))
        {
            Debug.Log("STABBED");
            lifeFunction.TakeDamage(WeaponDamage.hornDamagePenetration);
        }
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Horn"))
        {
            Debug.Log("JUST STABBED");
            lifeFunction.TakeDamage(WeaponDamage.hornDamageInitial);
            Physics2D.IgnoreCollision(collider, lifeFunction.gameObject.GetComponent<Collider2D>());
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Horn"))
        {
            Physics2D.IgnoreCollision(collider, lifeFunction.gameObject.GetComponent<Collider2D>(), false);
        }
    }

}
