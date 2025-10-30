using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleButtons : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene(1);
    }

    public void Settings()
    {
    }

    public void Credits()
    {
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void TestScenes(int scene)
    {
        SceneManager.LoadScene(scene);
    }
}
