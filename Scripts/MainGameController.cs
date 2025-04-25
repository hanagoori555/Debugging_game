using UnityEngine;

public class MainGameController : MonoBehaviour
{
    [Header("UI Экран обучения")]
    public GameObject tutorialPanel;

    [Header("Контроллер катсцены")]
    public CutsceneController cutsceneController;

    void Start()
    {
        // Показываем туториал только при Новой игре (нет сохранений) и если обучение ещё не проходили
        if (!GameSaveManager.instance.HasCheckpoint() &&
            !GameSaveManager.instance.IsTutorialCompleted())
        {
            tutorialPanel.SetActive(true);
        }
        else
        {
            // Иначе сразу стартуем катсцену
            cutsceneController.PlayCutscene();
        }
    }

    /// <summary>
    /// Привяжите к кнопке "Продолжить" на tutorialPanel
    /// </summary>
    public void EndTutorial()
    {
        tutorialPanel.SetActive(false);
        GameSaveManager.instance.SetTutorialCompleted(true);

        // Запускаем катсцену после обучения
        cutsceneController.PlayCutscene();
    }
}
