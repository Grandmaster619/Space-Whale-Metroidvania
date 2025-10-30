using Pathfinding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LifeFunction : MonoBehaviour
{
    [Header("Health")]
    public bool isAlive = true;
    public int maxHealth = 1000;
    public int currentHealth;
    public float regenDowntime = 3;
    public float regenerationFrequency = 0.8f;
    [Space]
    [Header("Immunity")]
    public float immunityCooldown = 0.5f;
    [Space]
    [Header("Player Color")]
    public float colorSpeedChangeDead;
    public float colorSpeedChangeAlive;
    public Color healthyColor, deadColor;

    public float colorTime = 0;
    private float regeneration_downtime_timer = 0;
    private float regeneration_frequency_timer = 0;
    private float immunity_cooldown_timer = 0;


    //public GameObject DeathScreen;



    // Start is called before the first frame update
    void Start()
    {
        currentHealth = maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        if (!isAlive)
            return;

        // Immunity frame cooldown after taking damage
        if (immunity_cooldown_timer > 0)
        {
            immunity_cooldown_timer -= Time.deltaTime;
        }

        // Passive Regeneration
        if (regeneration_downtime_timer > 0)
        {
            regeneration_downtime_timer -= Time.deltaTime;
        }
        else if (regeneration_frequency_timer > 0)
        {
            regeneration_frequency_timer -= Time.deltaTime;
        }
        else
        {
            Heal(1);
            regeneration_frequency_timer = regenerationFrequency;
        }

        // Detection if player should die
        if (currentHealth <= 0)
        {
            PlayerDied();
        }

        if (!isAlive)
        {
            ChangeColor(colorSpeedChangeDead);
        }
        else if (HealthToColor(healthyColor.r, deadColor.r) < (healthyColor.r - (healthyColor.r - deadColor.r) * colorTime))
        {

            ChangeColor(colorSpeedChangeAlive);
        }
        else
        {
            ChangeColor(-colorSpeedChangeAlive);
        }
    }

    public void TakeDamage(int amount)
    {
        // Play damage sound
        if (immunity_cooldown_timer <= 0)
        {
            currentHealth -= amount;
            regeneration_downtime_timer = regenDowntime;
            immunity_cooldown_timer = immunityCooldown;
        }
    }


    public void Heal(int amount)
    {

        // TODO: Play heal sound
        currentHealth += amount;

        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
    }

    public void PlayerDied()
    {
        isAlive = false;
        Debug.Log("Player had Died!");
    }

    public void ChangeColor(float speed)
    {
/*        float colorValue_r = HealthToColor(healthyColor.r, deadColor.r);
        float colorValue_g = HealthToColor(healthyColor.g, deadColor.g);
        float colorValue_b = HealthToColor(healthyColor.b, deadColor.b);
        Color newColor = new Color(colorValue_r, colorValue_g, colorValue_b, 255);*/
        colorTime += Time.deltaTime * speed;
        gameObject.GetComponent<SpriteRenderer>().color = Color.Lerp(healthyColor, deadColor, colorTime);
    }

    public float HealthToColor(float healthy, float dead)
    {
        return currentHealth * (healthy - dead) / maxHealth + dead;
    }
}
