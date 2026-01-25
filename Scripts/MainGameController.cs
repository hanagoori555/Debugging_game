using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainGameController : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Image tutorialImage;
    [SerializeField] private Button tutorialCloseButton;

    [Header("Credits Settings")]
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private Image creditsImage;
    [SerializeField] private Button creditsCloseButton;

    [SerializeField] private Sprite creditsSprite;

    public void StartTutorial()
    {
        PauseGuard.SetBoth("Tutorial", true);
        UIManager.instance?.SetPauseButtonVisible(false);
        UIManager.instance?.SetTaskPanelVisible(false);

        tutorialPanel.SetActive(true);
        Time.timeScale = 0f;
        Debug.Log("[MainGameController] Tutorial shown");
    }

    public void EndTutorial()
    {
        PauseGuard.SetBoth("Tutorial", false);
        UIManager.instance?.SetPauseButtonVisible(true);
        UIManager.instance?.SetTaskPanelVisible(true);
        UIManager.instance?.SetTask(TaskManager.instance.GetCurrentTaskData()?.description ?? string.Empty);

        tutorialPanel.SetActive(false);
        Time.timeScale = 1f;
        GameSaveManager.instance.SetTutorialCompleted(true);
        Debug.Log("[MainGameController] Tutorial closed -> NextTask()");
        TaskManager.instance.NextTask();
    }

    void Start()
    {
        if (tutorialCloseButton != null)
            tutorialCloseButton.onClick.AddListener(EndTutorial);

        if (creditsCloseButton != null)
            creditsCloseButton.onClick.AddListener(EndCredits);

        // Показываем туториал только если он не пройден
        if (!GameSaveManager.instance.IsTutorialCompleted())
        {
            StartTutorial();
        }
    }

    void Update()
    {
        // Закрытие панелей кликом мыши (если активна)
        if (tutorialPanel != null && tutorialPanel.activeSelf)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape))
                EndTutorial();
        }

        if (creditsPanel != null && creditsPanel.activeSelf)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape))
                EndCredits();
        }
    }

    // ------- CREDITS -------
    public void ShowCredits(Sprite spriteOverride = null, bool blockInput = true)
    {
        Debug.Log("[MainGameController] ShowCredits()");

        // Устанавливаем спрайт
        if (creditsImage != null)
        {
            creditsImage.sprite = spriteOverride != null ? spriteOverride : creditsSprite;

            // Растянем Image по всему родительскому RectTransform (как у туториала)
            var rt = creditsImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Немного защиты: белый цвет (чтобы не было затемнения) и сохранить аспект
            creditsImage.color = Color.white;
            creditsImage.preserveAspect = true;
            creditsImage.raycastTarget = true;
        }

        // Спрячем HUD и паузу (аналогично туториалу)
        PauseGuard.SetBoth("Credits", true);
        UIManager.instance?.SetPauseButtonVisible(false);
        UIManager.instance?.SetTaskPanelVisible(false);

        if (creditsPanel != null)
        {
            // Поднимем панель наверх, чтобы точно ничего не перекрыло
            creditsPanel.transform.SetAsLastSibling();
            creditsPanel.SetActive(true);
        }

        if (blockInput)
            Time.timeScale = 0f;
    }

    public void EndCredits()
    {
        Debug.Log("[MainGameController] EndCredits() called");

        PauseGuard.SetBoth("Credits", false);
        UIManager.instance?.SetPauseButtonVisible(true);
        UIManager.instance?.SetTaskPanelVisible(true);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        Time.timeScale = 1f;

        // По закрытию титров — вернуть в MainMenu
        Debug.Log("[MainGameController] Credits closed -> loading MainMenu");
        SceneManager.LoadScene("MainMenu");
    }
}
