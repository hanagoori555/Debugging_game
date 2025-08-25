using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    void Awake()
    {
        // Помечаем этот объект (и всё его содержимое) как "не уничтожать при загрузке сцены"
        DontDestroyOnLoad(gameObject);
    }

    public GameObject pauseMenuPanel;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        // если панель уже открыта — всегда резюмируем
        if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
        {
            ResumeGame();
            return;
        }

        // если визуально скрывается — не позволяем открыть
        if (PauseGuard.IsHiddenVisual)
        {
            Debug.Log("[PauseMenuController] ESC ignored - PauseGuard.HideVisual active.");
            return;
        }

        // пауза закрыта -> откроем только если PauseGuard разрешает
        if (PauseGuard.CanOpenPause())
        {
            PauseGame();
        }
        else
        {
            Debug.Log("[PauseMenuController] ESC ignored - PauseGuard blocks opening pause (cutscene/dialogue/input blocked).");
        }
    }


    public void PauseGame()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
