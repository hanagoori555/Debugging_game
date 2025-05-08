using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "School";
    private Vector2 _loadedPos;

    public void NewGame()
    {
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
        if (GameSaveManager.instance != null && GameSaveManager.instance.HasCheckpoint())
        {
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
        else NewGame();
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
        Application.Quit();
    }
}
