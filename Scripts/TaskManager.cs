using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-50)]
public class TaskManager : MonoBehaviour
{
    public static TaskManager instance;

    [Header("Список игровых задач (будут показываться по порядку)")]
    public string[] tasks;
    [Header("Нужна ли катсцена для задачи")]
    public bool[] playCutsceneForTask;  // строго такой же размер, как tasks
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

    public bool ShouldPlayCutscene()
    => playCutsceneForTask != null
       && currentIndex < playCutsceneForTask.Length
       && playCutsceneForTask[currentIndex];

    /// <summary>
    /// Переключиться на следующую задачу (если есть) и запустить катсцену по флагу.
    /// </summary>
    public void NextTask()
    {
        // 1) Увеличиваем индекс
        currentIndex = Mathf.Min(currentIndex + 1, tasks.Length - 1);

        // 2) Сохраняем прогресс
        GameSaveManager.instance.SaveCurrentTask(currentIndex);

        // 3) Обновляем UI
        UpdateTaskUI();

        // 4) При каждом переходе обновляем состояние DialogueCatalog
        DialogueCatalog.instance.RefreshState();

        // 5) Если для новой задачи нужна катсцена — запускаем её
        if (ShouldPlayCutscene())
        {
            var controller = FindObjectOfType<CutsceneController>();
            if (controller != null)
                controller.StartCutsceneForCurrentState();
        }
    }

    /// <summary>
    /// Сброс всех задач (для новой игры)
    /// </summary>
    public void ResetTasks()
    {
        currentIndex = 0;
        GameSaveManager.instance.SaveCurrentTask(0);
        UpdateTaskUI();
        DialogueCatalog.instance.RefreshState();
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

    // И геттер
    public int GetCurrentTaskIndex() => currentIndex;
}
