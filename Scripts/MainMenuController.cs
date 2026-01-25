using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // вверху файла

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "School";
    private Vector2 _loadedPos;
    public static event Action<string, Vector2, int> OnContinueGame;

    [Header("UI (optional)")]
    public Button continueButton;

    void Start()
    {
        RefreshContinueButton();
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null) return;
        bool ok = IsValidCheckpoint();
        continueButton.interactable = ok;
    }

    private bool IsValidCheckpoint()
    {
        if (GameSaveManager.instance == null) return false;
        if (!GameSaveManager.instance.HasCheckpoint()) return false;

        // Защита на случай, если "сохранение" — это MainMenu или пустая строка
        string savedScene = GameSaveManager.instance.GetSavedScene();
        if (string.IsNullOrEmpty(savedScene)) return false;
        if (savedScene == "MainMenu" || savedScene == "Bootstrap") return false;

        return true;
    }

    public void NewGame()
    {
        if (UIManager.instance != null)
            UIManager.instance.HidePauseMenu();

        if (GameSaveManager.instance != null)
        {
            GameSaveManager.instance.ClearAllData();   // Сброс всех сохранений
            TaskManager.instance?.ResetTasks();
            // Сбрасываем туториал
            GameSaveManager.instance.SetTutorialCompleted(false);
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        Debug.Log("ContinueGame() pressed");
        if (UIManager.instance != null)
            UIManager.instance.HidePauseMenu();

        // Блокируем действие, если чекпоинт невалиден
        if (!IsValidCheckpoint())
        {
            Debug.Log("[MainMenu] No valid checkpoint -> Continue ignored.");
            RefreshContinueButton(); // Подчистим состояние UI
            return;
        }

        // Есть валидный чекпоинт — читаем параметры и загружаем сцену
        string scene = GameSaveManager.instance.GetSavedScene();
        Vector2 pos = GameSaveManager.instance.LoadCheckpointPosition();
        int task = GameSaveManager.instance.LoadCurrentTask();

        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogWarning("[MainMenu] Saved scene is empty -> Continue ignored.");
            RefreshContinueButton();
            return;
        }

        Debug.Log($"[MainMenu] Continue → scene='{scene}', pos={pos}, task={task}");

        OnContinueGame?.Invoke(scene, pos, task);
        SceneManager.LoadScene(scene);
    }

    public void ExitGame()
    {
        // UnityEditor.EditorApplication.isPlaying = false;
        Application.Quit();
    }
}
