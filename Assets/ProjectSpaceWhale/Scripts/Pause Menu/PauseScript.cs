using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class PauseScript : MonoBehaviour
{

    void Start()
    {
        gameObject.SetActive(false);
    }

    public void OpenPauseMenu()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        gameObject.SetActive(true);
    }

    public void ClosePauseMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        gameObject.SetActive(false);
    }
}
