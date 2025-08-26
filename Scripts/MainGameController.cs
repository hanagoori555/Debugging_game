using UnityEngine;
using UnityEngine.UI;

public class MainGameController : MonoBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Image tutorialImage;
    [SerializeField] private Button closeButton;

    public void StartTutorial()
    {
        // запрещаем паузу и скрываем её визуал (используем уникальный ключ "Tutorial")
        PauseGuard.SetBoth("Tutorial", true);

        // спрячем кнопку паузы и плашку задачи
        UIManager.instance?.SetPauseButtonVisible(false);
        UIManager.instance?.SetTaskPanelVisible(false);

        tutorialPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void EndTutorial()
    {
        // восстановим возможность паузы и визуал
        PauseGuard.SetBoth("Tutorial", false);

        // покажем кнопку паузы и плашку задачи (заодно обновим текст задачи)
        UIManager.instance?.SetPauseButtonVisible(true);
        UIManager.instance?.SetTaskPanelVisible(true);

        // восстановим текст задачи из TaskManager
        UIManager.instance?.SetTask(TaskManager.instance.GetCurrentTaskData()?.description ?? string.Empty);

        tutorialPanel.SetActive(false);
        Time.timeScale = 1f;
        GameSaveManager.instance.SetTutorialCompleted(true);
        TaskManager.instance.NextTask();
    }

    void Start()
    {
        closeButton.onClick.AddListener(EndTutorial);

        // Показываем туториал только если он не пройден
        if (!GameSaveManager.instance.IsTutorialCompleted())
        {
            StartTutorial();
        }
    }
}