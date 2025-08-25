using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)] // выполнить раньше большинства других Update'ов
public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("Панель паузы (может быть активирована)")]
    public GameObject pauseMenuPanel;

    [Header("Ссылка на кнопку 'Пауза'")]
    public UnityEngine.UI.Button pauseButton;

    [Header("Текст текущей задачи")]
    public TextMeshProUGUI taskText;

    private bool isPaused = false;

    void Awake()
    {
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

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    void Start()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(TogglePause);
        }
    }

    void Update()
    {
        // 1) скрытие визуала — принудительно закрываем панель и делаем кнопку неинтерактивной
        if (PauseGuard.IsHiddenVisual)
        {
            // если визуально скрываем — гарантированно закрыть любую открытую панель
            if (isPaused)
            {
                Debug.Log("[UIManager] Pause forcibly closed due to PauseGuard.HideVisual.");
                ClosePause();
            }

            if (pauseButton != null)
                pauseButton.interactable = false;

            // в режиме HideVisual ESC не должен делать ничего
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[UIManager] ESC press ignored — PauseGuard.HideVisual active.");
            }

            return;
        }

        // обычный путь: можно ли вообще открывать паузу сейчас
        bool canOpen = PauseGuard.CanOpenPause();

        if (pauseButton != null)
            pauseButton.interactable = canOpen;

        // Если пауза открыта, но guard запретил — закроем паузу немедленно
        if (!canOpen && isPaused)
        {
            Debug.Log("[UIManager] Pause forcibly closed due to PauseGuard (cutscene/dialogue/input blocked).");
            ClosePause();
        }

        // Перехватываем ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // если пауза уже открыта — всегда закроем её
            if (isPaused)
            {
                ClosePause();
                return;
            }

            // если пауза закрыта — откроем только если можно
            if (canOpen)
            {
                TogglePause();
            }
            else
            {
                Debug.Log("[UIManager] ESC press ignored — PauseGuard blocks opening pause (cutscene/dialogue/input blocked).");
            }
        }
    }

    public void TogglePause()
    {
        // Дополнительная проверка: не дадим открыть паузу, если PauseGuard запрещает
        if (!PauseGuard.CanOpenPause())
        {
            Debug.Log("[UIManager] TogglePause blocked by PauseGuard.");
            // Если пауза уже открыта (маловероятно), закроем её как запасной вариант
            if (isPaused)
                ClosePause();
            return;
        }

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(isPaused);

        Debug.Log($"[UIManager] TogglePause -> isPaused={isPaused}");
    }

    private void ClosePause()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        Debug.Log("[UIManager] ClosePause executed.");
    }

    public void SetTask(string task)
    {
        if (taskText == null)
        {
            Debug.LogWarning("[UIManager] Поле taskText не назначено в Inspector!");
            return;
        }

        if (string.IsNullOrEmpty(task))
        {
            taskText.gameObject.SetActive(false);
        }
        else
        {
            taskText.gameObject.SetActive(true);
            taskText.text = "Задача: " + task;
        }
    }

    public void GoToMainMenu()
    {
        if (!PauseGuard.CanOpenPause())
        {
            Debug.Log("[UIManager] GoToMainMenu blocked by PauseGuard.");
            return;
        }

        ClosePause();
        SceneManager.LoadScene("MainMenu");
    }

    public void HidePauseMenu()
    {
        ClosePause();
    }
}
