using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "School";
    private Vector2 _loadedPos;

    public void NewGame()
    {
        // Сбрасываем паузу и скрываем панель
        Time.timeScale = 1f;
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

        // Сбрасываем паузу и скрываем панель
        Time.timeScale = 1f;
        if (UIManager.instance != null)
            UIManager.instance.HidePauseMenu();

        if (GameSaveManager.instance != null && GameSaveManager.instance.HasCheckpoint())
        {
            Debug.Log("  → Has checkpoint, loading saved game");
            string scene = GameSaveManager.instance.GetSavedScene();
            if (string.IsNullOrEmpty(scene))
            {
                NewGame();
                return;
            }

            _loadedPos = GameSaveManager.instance.LoadCheckpointPosition();
            SceneManager.sceneLoaded += OnSceneLoaded;
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
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
            player.TeleportTo(_loadedPos);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void ExitGame()
    {
        // Перед выходом из игры тоже сбросим паузу
        Time.timeScale = 1f;
        if (UIManager.instance != null)
            UIManager.instance.HidePauseMenu();
        UnityEditor.EditorApplication.isPlaying = false;
        //Application.Quit();
    }
}
