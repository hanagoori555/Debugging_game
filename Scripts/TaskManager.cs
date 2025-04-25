using UnityEngine;
using UnityEngine.SceneManagement;

public class TaskManager : MonoBehaviour
{
    public static TaskManager instance;

    [Header("Список игровых задач (будут показываться по порядку)")]
    public string[] tasks;

    [Header("Ссылка на UIManager для обновления текста задачи")]
    private UIManager uiManager;

    private int currentIndex = 0;

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
        // Подписываемся на событие загрузки сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // На старте ищем UIManager и обновляем UI
        uiManager = FindObjectOfType<UIManager>();
        UpdateTaskUI();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // При каждой загрузке новой сцены обновляем ссылку на UIManager
        uiManager = FindObjectOfType<UIManager>();
        UpdateTaskUI();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Переключиться на следующую задачу (если есть)
    /// </summary>
    public void NextTask()
    {
        if (tasks == null || tasks.Length == 0)
            return;
        currentIndex = Mathf.Min(currentIndex + 1, tasks.Length - 1);
        UpdateTaskUI();
        Debug.Log($"[TaskManager] Переключили на индекс {currentIndex}");
    }

    /// <summary>
    /// Сбросить задачи (для начала новой игры)
    /// </summary>
    public void ResetTasks()
    {
        if (tasks == null || tasks.Length == 0)
            return;
        currentIndex = 0;
        UpdateTaskUI();
        Debug.Log("[TaskManager] Задачи сброшены");
    }

    /// <summary>
    /// Получить текст текущей задачи
    /// </summary>
    public string GetCurrentTask()
    {
        if (tasks == null || tasks.Length == 0)
            return string.Empty;
        return tasks[currentIndex];
    }

    private void UpdateTaskUI()
    {
        if (uiManager != null)
            uiManager.SetTask(GetCurrentTask());
    }
}
