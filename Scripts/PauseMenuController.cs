using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuPanel;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // Удаляем Update() который обрабатывал ESC: теперь UIManager делает это.
    // Оставляем API для прямого вызова (например, если куда-то ещё зовут PauseMenuController.PauseGame()).
    public void PauseGame()
    {
        // Если есть UIManager в сцене — делегируем туда (единый источник правды)
        if (UIManager.instance != null)
        {
            UIManager.instance.ForceOpenPause(); // добавим этот метод в UIManager
            return;
        }

        // fallback — если UIManager нет, работаем локально
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (UIManager.instance != null)
        {
            UIManager.instance.ForceClosePause();
            return;
        }

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
