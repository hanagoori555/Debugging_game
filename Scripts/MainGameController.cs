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
        tutorialPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void EndTutorial()
    {
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