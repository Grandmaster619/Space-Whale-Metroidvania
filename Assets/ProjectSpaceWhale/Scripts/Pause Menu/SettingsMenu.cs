using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Text senseText;
    [SerializeField] private Text brightText;

    void Start()
    {
        mouseSensitivitySlider.value = Settings.GetInstance().GetMouseSensitivity();
        senseText.text = "" + mouseSensitivitySlider.value;
        brightnessSlider.value = Settings.GetInstance().GetMouseSensitivity(); ;
        brightText.text = "" + brightnessSlider.value;

        mouseSensitivitySlider.onValueChanged.AddListener((f) => ChangeMouseSensitivity(f));
        brightnessSlider.onValueChanged.AddListener((f) => ChangeBrightness(f));

        gameObject.SetActive(false);
    }

    public void OpenSettingsMenu()
    {
        Time.timeScale = 0f;
        gameObject.SetActive(true);
    }

    public void CloseSettingsMenu()
    {
        gameObject.SetActive(false);
    }

    private void ChangeMouseSensitivity(float value)
    {
        Settings.GetInstance().SetMouseSensitivity(value);
        senseText.text = "" + mouseSensitivitySlider.value;
    }

    private void ChangeBrightness(float value)
    {
        Settings.GetInstance().SetBrightness(value);
        brightText.text = "" + brightnessSlider.value;
    }
}
