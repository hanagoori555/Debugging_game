using UnityEngine;

public class MainGameController : MonoBehaviour
{
    [Header("UI Экран обучения")]
    public GameObject tutorialPanel;

    [Header("Контроллер катсцены")]
    public CutsceneController cutsceneController;

    void Start()
    {
        bool has = GameSaveManager.instance.HasCheckpoint();
        bool tut = GameSaveManager.instance.IsTutorialCompleted();
        Debug.Log($"[MainGameController] HasCheckpoint={has}, TutorialCompleted={tut}");
        if (!tut)
        {
            tutorialPanel.SetActive(true);
            return;
        }
        TryPlayCutscene();
    }


    public void EndTutorial()
    {
        tutorialPanel.SetActive(false);
        GameSaveManager.instance.SetTutorialCompleted(true);
        TryPlayCutscene();
    }

    private void TryPlayCutscene()
    {
        if (TaskManager.instance.ShouldPlayCutscene())
            cutsceneController.StartCutsceneForCurrentState();
    }
}
