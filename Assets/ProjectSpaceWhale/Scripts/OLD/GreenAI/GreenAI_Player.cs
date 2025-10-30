using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GreenAI_Player : MonoBehaviour
{
    public bool SetDirectControl;

    GreenAI_Movement movement;
    float moveInput;
    bool dashInput = false;
    bool jumpInputDown = false;
    bool jumpInputUp = false;
    bool attackInput = false;


    // Start is called before the first frame update
    void Start()
    {
        movement = GetComponent<GreenAI_Movement>();
        SetDirectControl = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            SetDirectControl = !SetDirectControl;
        }

        if (SetDirectControl)
        {
            moveInput = Input.GetAxisRaw("Horizontal");
            dashInput = Input.GetKey(KeyCode.LeftShift);
            jumpInputDown = Input.GetButtonDown("Jump");
            jumpInputUp = Input.GetButtonUp("Jump");

            attackInput = Input.GetMouseButton(0);
            //Debug.Log(moveInput);
        }

        movement.AIMove(moveInput, dashInput, jumpInputDown, jumpInputUp, attackInput);

    }
}

