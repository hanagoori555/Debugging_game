using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEditor.PlayerSettings;

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "School";
    private Vector2 _loadedPos;
    public static event Action<string, Vector2, int> OnContinueGame;

    public void NewGame()
    {
        if (UIManager.instance != null)
            UIManager.instance.HidePauseMenu();

        if (GameSaveManager.instance != null)
        {
            GameSaveManager.instance.ClearAllData();   // сброс всех сохранений
            TaskManager.instance.ResetTasks();
            // Сбрасываем туториал:
            GameSaveManager.instance.SetTutorialCompleted(false);
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        Debug.Log("ContinueGame() pressed");
        if (UIManager.instance != null)
            UIManager.instance.HidePauseMenu();
        if (GameSaveManager.instance != null && GameSaveManager.instance.HasCheckpoint())
        {
            _loadedPos = GameSaveManager.instance.LoadCheckpointPosition();

            // 1) получаем сохранённые данные
            Debug.Log("  → Has checkpoint, loading saved game");
            string scene = GameSaveManager.instance.GetSavedScene();
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning("Saved scene empty → starting NewGame()");
                NewGame();
                return;
            }
            Vector2 pos = GameSaveManager.instance.LoadCheckpointPosition();
            int task = GameSaveManager.instance.LoadCurrentTask();

            Debug.Log($"[MainMenu] Continue → scene='{scene}', pos={pos}, task={task}");

            // 2) триггерим событие для BootstrapController
            OnContinueGame?.Invoke(scene, pos, task);

            // 3) грузим сцену
            SceneManager.LoadScene(scene);
        }
        else
        {
            Debug.Log("  → No checkpoint, falling back to NewGame()");
            NewGame();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // восстанавливаем задачу:
        int savedTask = GameSaveManager.instance.LoadCurrentTask();
        Debug.Log($"[MainMenu] Restoring task index = {savedTask}");
        TaskManager.instance.SetCurrentTaskIndex(savedTask, true);

        // телепортим игрока
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
            player.TeleportTo(_loadedPos);

        // отписываемся
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void ExitGame()
    {
        UnityEditor.EditorApplication.isPlaying = false;
        //Application.Quit();
    }
}
