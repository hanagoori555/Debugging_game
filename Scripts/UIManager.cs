using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Панель паузы (может быть активирована)")]
    public GameObject pauseMenuPanel; // панель с кнопками Resume/Exit

    [Header("Ссылка на кнопку 'Пауза'")]
    public UnityEngine.UI.Button pauseButton;

    [Header("Текст текущей задачи")]
    public TextMeshProUGUI taskText;

    private bool isPaused = false;

    void Start()
    {
        // Подписываемся на нажатие кнопки
        pauseButton.onClick.AddListener(TogglePause);
    }

    /// <summary>
    /// Включает/выключает паузу
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(isPaused);
    }

    /// <summary>
    /// Вызвать из кода, чтобы обновить текст задачи
    /// </summary>
    public void SetTask(string task)
    {
        if (taskText != null)
            taskText.text = "Задача: " + task;
    }

    /// <summary>
    /// Вызывается при клике на кнопку "Main Menu" в паузе
    /// </summary>
    public void GoToMainMenu()
    {
        // Снимаем паузу
        Time.timeScale = 1f;
        // Загружаем сцену главного меню
        SceneManager.LoadScene("MainMenu");
    }
}
