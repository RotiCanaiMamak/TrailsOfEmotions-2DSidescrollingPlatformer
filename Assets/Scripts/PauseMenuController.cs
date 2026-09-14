using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button settingsBackButton;

    [Header("Audio Sliders")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider backgroundVolumeSlider;

    private bool showingSettings;

    private void Awake()
    {
        EnsureEventSystem();
        WireControls();
        HidePauseMenu();
    }

    public void ShowPauseMenu()
    {
        EnsureEventSystem();
        WireControls();
        RefreshSliders();

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(true);
        }

        ShowMainPanel();
    }

    public void HidePauseMenu()
    {
        showingSettings = false;

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
        }
    }

    public void ShowMainPanel()
    {
        showingSettings = false;

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void ShowSettingsPanel()
    {
        showingSettings = true;
        RefreshSliders();

        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public bool HandleEscapePressed()
    {
        if (pauseMenuRoot == null || !pauseMenuRoot.activeSelf)
        {
            return false;
        }

        if (showingSettings)
        {
            ShowMainPanel();
            return true;
        }

        GameManager.Instance?.ResumeGame();
        return true;
    }

    private void WireControls()
    {
        WireButton(continueButton, () => GameManager.Instance?.ResumeGame());
        WireButton(homeButton, () => GameManager.Instance?.ReturnHome());
        WireButton(settingsButton, ShowSettingsPanel);
        WireButton(settingsBackButton, ShowMainPanel);

        WireSlider(masterVolumeSlider, value => AudioManager.Instance?.SetMasterVolume(value));
        WireSlider(sfxVolumeSlider, value => AudioManager.Instance?.SetSfxVolume(value));
        WireSlider(backgroundVolumeSlider, value => AudioManager.Instance?.SetBackgroundVolume(value));
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void WireSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(action);
    }

    private void RefreshSliders()
    {
        AudioManager audioManager = AudioManager.Instance;
        SetSliderValue(masterVolumeSlider, audioManager != null ? audioManager.MasterVolume : 1f);
        SetSliderValue(sfxVolumeSlider, audioManager != null ? audioManager.SfxVolume : 1f);
        SetSliderValue(backgroundVolumeSlider, audioManager != null ? audioManager.BackgroundVolume : 1f);
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem =
            EventSystem.current != null
                ? EventSystem.current
                : FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        if (eventSystem != null)
        {
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
    }
}
