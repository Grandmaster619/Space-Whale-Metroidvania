using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MenuState
{
    None,
    Pause,
    Settings
}

public class PauseMenu : MonoBehaviour
{
    private static PauseMenu instance;

    [SerializeField] private PauseScript pauseScript;
    //[SerializeField] private SettingsMenu settingsMenu;
    private MenuState menuState;
    private bool canOpenPauseMenu;

    public void Awake()
    {
        if (instance == null)
            instance = this;
        else {
            Debug.Log("An instance of PauseMenu already exists");
            Destroy(gameObject);
        }
    }

    public void Start()
    {
        menuState = MenuState.None;
        canOpenPauseMenu = true;
    }

    void Update()
    {
        if (!canOpenPauseMenu)
            return;

        if (Input.GetKeyDown(KeyCode.Escape)) {
            switch(menuState) {
                case MenuState.None:
                    Pause();
                    break;
                case MenuState.Pause:
                    Resume();
                    break;
                case MenuState.Settings:
                    CloseSettings();
                    break;
            }
        }
    }

    public void Pause()
    {
        menuState = MenuState.Pause;
        pauseScript.OpenPauseMenu();
    }

    public void Resume()
    {
        pauseScript.ClosePauseMenu();
        menuState = MenuState.None;
    }

    public void OpenSettings()
    {
        pauseScript.ClosePauseMenu();
        //settingsMenu.OpenSettingsMenu();
        menuState = MenuState.Settings;
    }

    public void CloseSettings()
    {
        //settingsMenu.CloseSettingsMenu();
        pauseScript.OpenPauseMenu();
        menuState = MenuState.Pause;
    }

    public void ForceCloseAllMenus()
    {
        if (menuState == MenuState.None)
            return;

        if (menuState == MenuState.Pause)
            Resume();
        else {
            CloseSettings();
            Resume();
        }
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("TitleScreen");
    }

    public void Quit()
    {
        Debug.Log("quitting");
        Application.Quit();
    }

    public MenuState GetMenuState() { return menuState; }

    public static PauseMenu GetInstance() { return instance; }
}
