using UnityEngine;

public class MainGameController : MonoBehaviour
{
    [Header("UI Экран обучения")]
    public GameObject tutorialPanel;

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
    }


    public void EndTutorial()
    {
        tutorialPanel.SetActive(false);
        GameSaveManager.instance.SetTutorialCompleted(true);
    }
}
