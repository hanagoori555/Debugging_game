using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;     // <-- 1) singleton-поле

    [Header("Панель паузы (может быть активирована)")]
    public GameObject pauseMenuPanel; // панель с кнопками Resume/Exit

    [Header("Ссылка на кнопку 'Пауза'")]
    public UnityEngine.UI.Button pauseButton;

    [Header("Текст текущей задачи")]
    public TextMeshProUGUI taskText;

    private bool isPaused = false;
    void Awake()
    {
        // <-- 2) и 3) проверка и DontDestroyOnLoad
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // (Опционально) сразу выключить панель
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

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
    /// Вызывается из TaskManager для обновления текста задачи.
    /// Если task == null или пустая строка, скрываем taskText.
    /// </summary>
    public void SetTask(string task)
    {
        if (taskText == null)
        {
            Debug.LogWarning("[UIManager] Поле taskText не назначено в Inspector!");
            return;
        }

        if (string.IsNullOrEmpty(task))
        {
            // Если описание пустое, скрываем текстовую строку
            taskText.gameObject.SetActive(false);
        }
        else
        {
            taskText.gameObject.SetActive(true);
            taskText.text = "Задача: " + task;
        }
    }

    /// <summary>
    /// Вызывается при клике на кнопку "Main Menu" в паузе
    /// </summary>
    public void GoToMainMenu()
    {
        // Снимаем паузу и прячем панель
        isPaused = false;
        Time.timeScale = 1f;
        // Загружаем сцену главного меню
        SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Скрывает панель паузы (без изменения Time.timeScale).
    /// </summary>
    public void HidePauseMenu()
    {
        isPaused = false;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }
}
