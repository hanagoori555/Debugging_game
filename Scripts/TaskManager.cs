using System;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-50)]
public class TaskManager : MonoBehaviour
{
    public static TaskManager instance;
    public TaskData[] tasks;
    private UIManager uiManager;
    private int currentIndex = 0;

    // Событие для внешних подписчиков, если нужно
    public event Action<TaskData> OnTaskChanged;


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadTasksFromJson();
            // подписываемся на загрузку сцен
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // обновляем UIManager и UI
        uiManager = FindObjectOfType<UIManager>();
        UpdateTaskUI();
    }

    void Start()
    {
        uiManager = FindObjectOfType<UIManager>();
        UpdateTaskUI();
    }

    void OnDestroy()
    {
        // Отписка чтобы не было утечек
        Interactable.OnAnyInteract -= HandleInteract;
        SceneExitDetector.OnSceneExit -= HandleSceneExit;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public bool ShouldPlayCutscene()
    {
        // возвращаем флаг hasCutscene у текущего задания
        var task = GetCurrentTaskData();
        return task != null && task.hasCutscene;
    }

    /// <summary>
    /// Подписывается на вызовы именно для этой задачи.
    /// </summary>
    public void SubscribeToTask(TaskData task)
    {
        if (task == null) return;
        switch (task.triggerType)
        {
            case "Interact":
                Debug.Log($"[TaskManager] Subscribing to Interactable '{task.triggerParam}'");
                Interactable.OnAnyInteract += OnInteractTrigger;
                break;
            case "SceneExit":
                Debug.Log($"[TaskManager] Subscribing to SceneExit '{task.triggerParam}'");
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
                break;
        }
    }

    /// <summary>
    /// Убирает подписку, когда задача меняется.
    /// </summary>
    public void UnsubscribeFromTask(TaskData task)
    {
        if (task == null) return;
        switch (task.triggerType)
        {
            case "Interact":
                Debug.Log($"[TaskManager] Unsubscribing from Interactable '{task.triggerParam}'");
                Interactable.OnAnyInteract -= OnInteractTrigger;
                break;
            case "SceneExit":
                Debug.Log($"[TaskManager] Unsubscribing from SceneExit '{task.triggerParam}'");
                SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;
                break;
        }
    }


    private void HandleInteract(string id)
    {
        if (tasks == null || tasks.Length == 0)
            return;   // ещё нет задач

        var task = tasks[currentIndex];
        if (task.triggerType == "Interact" && task.triggerParam == id)
        {
            NextTask();
        }
    }


    private void HandleSceneExit(string sceneName)
    {
        if (tasks == null || tasks.Length == 0)
            return;

        var task = tasks[currentIndex];
        if (task.triggerType == "SceneExit" && task.triggerParam == sceneName)
        {
            NextTask();
            SceneManager.LoadScene(sceneName);
        }
    }


    private void LoadTasksFromJson()
    {
        var ta = Resources.Load<TextAsset>("Tasks");
        if (ta == null)
        {
            Debug.LogError("[TaskManager] Resources/Tasks.json не найден!");
            tasks = new TaskData[0];
            return;
        }

        Debug.Log($"[TaskManager] Raw JSON:\n{ta.text}");

        try
        {
            var wrapper = JsonUtility.FromJson<TaskList>(ta.text);
            tasks = wrapper.tasks;
            Debug.Log($"[TaskManager] Загрузили {tasks.Length} задач из JSON.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TaskManager] Ошибка парсинга JSON: {e.Message}");
            tasks = new TaskData[0];
        }
    }



    public void NextTask()
    {
        // 1) отписываемся от предыдущей задачи
        var oldTask = GetCurrentTaskData();
        UnsubscribeFromTask(oldTask);

        // 2) меняем индекс
        currentIndex = Mathf.Min(currentIndex + 1, tasks.Length - 1);
        GameSaveManager.instance.SaveCurrentTask(currentIndex);
        UpdateTaskUI();
        OnTaskChanged?.Invoke(tasks[currentIndex]);

        // 3) ОБЯЗАТЕЛЬНО обновляем DialogueCatalog
        DialogueCatalog.instance.RefreshState();
        Debug.Log($"[TaskManager] After RefreshState, now state = {TaskManager.instance.GetCurrentTaskIndex()}");

        // 4) подписываемся на новый триггер
        var newTask = GetCurrentTaskData();
        SubscribeToTask(newTask);

        Debug.Log($"[TaskManager] newTask.id={newTask.id}, hasCutscene={newTask.hasCutscene}");
        var ctrl = FindObjectOfType<CutsceneController>();
        Debug.Log($"[TaskManager] CutsceneController found? {ctrl != null}");
        if (newTask.hasCutscene && ctrl != null)
            ctrl.StartCutsceneForCurrentState();
        // 5) если нужно — показываем катсцену
        if (newTask.hasCutscene)
            FindObjectOfType<CutsceneController>()?.StartCutsceneForCurrentState();
    }



    public void ResetTasks()
    {
        var oldTask = GetCurrentTaskData();
        UnsubscribeFromTask(oldTask);

        currentIndex = 0;
        GameSaveManager.instance.SaveCurrentTask(0);
        UpdateTaskUI();
        OnTaskChanged?.Invoke(tasks[currentIndex]);

        // ОБЯЗАТЕЛЬНО сначала обновляем состояние каталога
        DialogueCatalog.instance.RefreshState();

        SubscribeToTask(tasks[currentIndex]);
    }


    public int GetCurrentTaskIndex() => currentIndex;
    public TaskData GetCurrentTaskData() => tasks.Length > 0 ? tasks[currentIndex] : null;

    private void UpdateTaskUI()
    {
        if (tasks == null || tasks.Length == 0)
        {
            Debug.LogWarning("[TaskManager] Нет задач для отображения.");
            return;
        }
        if (currentIndex < 0 || currentIndex >= tasks.Length)
        {
            Debug.LogWarning($"[TaskManager] Некорректный currentIndex={currentIndex}.");
            return;
        }
        uiManager?.SetTask(tasks[currentIndex].description);
    }

    private void SubscribeToCurrentTrigger()
    {
        var task = GetCurrentTaskData();
        if (task == null) return;
        Debug.Log($"[TaskManager] Subscribing to trigger for task {task.id}: {task.triggerType} → {task.triggerParam}");
        switch (task.triggerType)
        {
            case "Interact":
                Interactable.OnAnyInteract += OnInteractTrigger;
                break;
            case "SceneExit":
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
                break;
        }
    }

    private void UnsubscribeFromCurrentTrigger()
    {
        var task = GetCurrentTaskData();
        if (task == null) return;
        Debug.Log($"[TaskManager] Unsubscribing from trigger for task {task.id}: {task.triggerType} → {task.triggerParam}");
        switch (task.triggerType)
        {
            case "Interact":
                Interactable.OnAnyInteract -= OnInteractTrigger;
                break;
            case "SceneExit":
                SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;
                break;
        }
    }

    private void OnInteractTrigger(string objectId)
    {
        Debug.Log($"[TaskManager] OnInteractTrigger fired with id = '{objectId}' (current task index = {currentIndex})");
        var task = GetCurrentTaskData();
        if (task.triggerType != "Interact" || task.triggerParam != objectId)
        {
            Debug.Log($"[TaskManager]   → Ignored (expected {task.triggerParam})");
            return;
        }

        // Получаем диалоги
        var lines = DialogueCatalog.instance.GetInteractableLines(objectId);
        Debug.Log($"[TaskManager]   → Found {lines?.Length ?? 0} dialogue lines for '{objectId}' in state {currentIndex}");
        if (lines == null || lines.Length == 0)
        {
            Debug.Log($"[TaskManager]   → No lines: advancing immediately");
            NextTask();
            return;
        }

        // Показываем с колбэком
        Debug.Log($"[TaskManager]   → Showing dialogue for '{objectId}'");
        DialogueManager.instance.ShowDialogue(lines, () =>
        {
            Debug.Log($"[TaskManager]   → Dialogue complete: advancing task");
            NextTask();
        });
    }

    private void OnSceneExitTrigger(string sceneName)
    {
        Debug.Log($"[TaskManager] OnSceneExitTrigger fired with scene = '{sceneName}' (current task index = {currentIndex})");
        var task = GetCurrentTaskData();
        if (task.triggerType != "SceneExit" || task.triggerParam != sceneName)
        {
            Debug.Log($"[TaskManager]   → Ignored (expected {task.triggerParam})");
            return;
        }

        if (task.hasCutscene)
        {
            Debug.Log($"[TaskManager]   → Showing cutscene before loading '{sceneName}'");
            var (lines, interruptAt) = DialogueCatalog.instance.GetCutsceneForCurrentState();
            Debug.Log($"[TaskManager]   → Cutscene has {lines.Length} lines, interruptAt = {interruptAt}");
            DialogueManager.instance.ShowDialogue(lines, () =>
            {
                Debug.Log($"[TaskManager]   → Cutscene complete: loading scene '{sceneName}' and advancing task");
                SceneManager.LoadScene(sceneName);
                NextTask();
            });
        }
        else
        {
            Debug.Log($"[TaskManager]   → No cutscene: loading scene '{sceneName}' and advancing task");
            SceneManager.LoadScene(sceneName);
            NextTask();
        }
    }

    private void ShowCurrentInteractableDialogue(string objectId, Action onComplete)
    {
        // Получаем диалоги для этого объекта в текущем stateId
        var lines = DialogueCatalog.instance.GetInteractableLines(objectId);
        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning($"[TaskManager] Нет диалога для objectId={objectId} в stateId={currentIndex}");
            onComplete?.Invoke();
            return;
        }
        // Подписываемся на конец показа диалога
        DialogueManager.instance.ShowDialogue(lines, onComplete);
    }

}

[Serializable]
public class TaskData
{
    public int id;
    public string description;
    public string triggerType;    // "Interact" или "SceneExit"
    public string triggerParam;   // objectId или имя сцены
    public bool hasCutscene;
}

[Serializable]
public class TaskList
{
    public TaskData[] tasks;
}
