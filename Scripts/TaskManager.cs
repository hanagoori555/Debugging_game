using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-50)]
public class TaskManager : MonoBehaviour
{
    public static TaskManager instance;

    [Header("Auto-Dialogue Character Mappings")]
    public List<CharacterMapping> characterMappings = new List<CharacterMapping>();

    [Header("Default Spawn Point")]
    public Transform defaultSpawnPoint;

    [Header("JSON Tasks File")]
    public TaskData[] tasks;

    private UIManager uiManager;
    private int currentIndex = 0;

    public event Action<TaskData> OnTaskChanged;
    private HashSet<int> _autoPlayedStates = new HashSet<int>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadTasksFromJson();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        uiManager = FindObjectOfType<UIManager>();
        UpdateTaskUI();

        var sp = GameObject.Find("Spawn_Default");
        if (sp != null) defaultSpawnPoint = sp.transform;

        SubscribeToTask(GetCurrentTaskData());
    }

    void Start()
    {
        uiManager = FindObjectOfType<UIManager>();
        UpdateTaskUI();
    }

    void OnDestroy()
    {
        HandleUnsubscribeAll();
    }

    private void HandleUnsubscribeAll()
    {
        Interactable.OnAnyInteract -= OnInteractTrigger;
        SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;
        ComputerInterface.OnCorrectCommandEntered -= OnConsoleAccepted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void SubscribeToTask(TaskData task)
    {
        if (task == null) return;
        Debug.Log($"[TaskManager] Subscribing to task {task.id}: {task.triggerType}");

        if (task.id == 0)
        {
            FindObjectOfType<MainGameController>()?.StartTutorial();
            return;
        }

        // Для задач с катсценой
        if (task.hasCutscene)
        {
            StartCutsceneForTask(task, () => {
                Debug.Log($"[TaskManager] Cutscene finished for task {task.id}");

                // Подписываемся на OnAnyInteract только после завершения катсцены
                if (task.triggerType == "Interact")
                {
                    Interactable.OnAnyInteract += OnInteractTrigger;
                }
                // Для Auto-задач сразу переходим к следующей задаче
                else if (task.triggerType == "Auto")
                {
                    NextTask();
                }
            });
        }
        else
        {
            // Для задач без катсцены стандартная подписка
            SubscribeToTaskTrigger(task);
        }
    }

    private void StartCutsceneForTask(TaskData task, Action onCutsceneComplete)
    {
        Debug.Log($"[TaskManager] Starting cutscene for task {task.id}");
        CutsceneController.instance.StartCutsceneForCurrentState(() => {
            Debug.Log($"[TaskManager] Cutscene finished for task {task.id}");
            onCutsceneComplete?.Invoke();
        });
    }


    private void SubscribeToTaskTrigger(TaskData task)
    {
        switch (task.triggerType)
        {
            case "Auto":
                StartCoroutine(AutoTriggerCoroutine(task));
                break;

            case "Interact":
                if (task.id == 9)
                {
                    ComputerInterface.OnCorrectCommandEntered += OnConsoleAccepted;
                    FindObjectOfType<ComputerInterface>()?.Show("Введите команду, чтобы продолжить…");
                }
                else
                {
                    Interactable.OnAnyInteract += OnInteractTrigger;
                }
                break;

            case "SceneExit":
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
                break;
        }
    }

    private void OnConsoleAccepted()
    {
        Debug.Log("[TaskManager] Console accepted → NextTask()");
        ComputerInterface.OnCorrectCommandEntered -= OnConsoleAccepted;
        NextTask();
    }

    public void UnsubscribeFromTask(TaskData task)
    {
        if (task == null) return;

        switch (task.triggerType)
        {
            case "Interact":
                if (task.id == 9)
                    ComputerInterface.OnCorrectCommandEntered -= OnConsoleAccepted;
                else
                    Interactable.OnAnyInteract -= OnInteractTrigger;
                break;

            case "SceneExit":
                SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;
                break;
        }
    }

    private IEnumerator AutoTriggerCoroutine(TaskData task)
    {
        yield return null;

        if (_autoPlayedStates.Contains(task.id)) yield break;
        _autoPlayedStates.Add(task.id);

        // Если это Auto-задача с катсценой - пропускаем стандартную обработку
        if (task.hasCutscene) yield break;

        // Стандартная обработка для Auto-задач без катсцены
        var autoLines = DialogueCatalog.instance.GetAutoDialogueForCurrentState();
        if (autoLines != null && autoLines.Length > 0)
        {
            SpawnAndShow(autoLines);
            yield break;
        }

        var interactLines = DialogueCatalog.instance.GetInteractableLines(task.triggerParam);
        if (interactLines.Length > 0)
        {
            DialogueManager.instance.ShowDialogue(interactLines, NextTask);
            yield break;
        }

        NextTask();
    }

    private void SpawnAndShow(DialogueLine[] lines)
    {
        // 1) Сразу собираем список уникальных говорящих (кроме “Рассказчик” и пустых)
        var speakerNames = lines
            .Select(l => l.characterName)
            .Where(name => !string.IsNullOrEmpty(name) && name != "Рассказчик")
            .Distinct();

        // 2) Для каждого - пробуем спавнить prefab
        List<GameObject> spawnedCharacters = new List<GameObject>();
        foreach (var speaker in speakerNames)
        {
            var map = characterMappings.FirstOrDefault(m => m.characterName == speaker);
            if (map != null && map.prefab != null)
            {
                Transform spawnAt = map.spawnPoint != null ? map.spawnPoint : defaultSpawnPoint;
                Vector3 pos = spawnAt != null ? spawnAt.position : Vector3.zero;
                var go = Instantiate(map.prefab, pos, Quaternion.identity);
                spawnedCharacters.Add(go);
            }
            else
            {
                Debug.Log($"[TaskManager] Нет prefab для '{speaker}', спавн пропускаем");
            }
        }

        // 3) Запускаем сам диалог
        DialogueManager.instance.ShowDialogue(lines, () =>
        {
            // 4) После окончания — удаляем всё, что спавнили
            foreach (var go in spawnedCharacters)
                if (go != null) Destroy(go);

            NextTask();
        });
    }


    private void OnInteractTrigger(string objectId)
    {
        var task = GetCurrentTaskData();
        if (task == null) return;
        if (task.triggerType != "Interact" || task.triggerParam != objectId)
            return;

        Debug.Log($"[TaskManager] OnInteractTrigger called for id='{objectId}', task={task.id}");

        var lines = DialogueCatalog.instance.GetInteractableLines(objectId);
        if (lines.Length > 0)
            DialogueManager.instance.ShowDialogue(lines, NextTask);
        else
            NextTask();
    }

    private void OnSceneExitTrigger(string sceneName)
    {
        var task = GetCurrentTaskData();
        if (task == null) return;
        if (task.triggerType != "SceneExit" || task.triggerParam != sceneName) return;

        if (task.hasCutscene)
        {
            StartCutsceneForTask(task, () =>
            {
                // After cutscene, load scene and proceed
                SceneManager.LoadScene(sceneName);
                NextTask();
            });
        }
        else
        {
            SceneManager.LoadScene(sceneName);
            NextTask();
        }
    }

    public void NextTask()
    {
        var oldTask = GetCurrentTaskData();
        UnsubscribeFromTask(oldTask);

        currentIndex = Mathf.Min(currentIndex + 1, tasks.Length - 1);
        GameSaveManager.instance.SaveCurrentTask(currentIndex);

        DialogueCatalog.instance.ReloadForActiveScene();

        UpdateTaskUI();
        OnTaskChanged?.Invoke(GetCurrentTaskData());

        SubscribeToTask(GetCurrentTaskData());

        Debug.Log($"[TaskManager] Moved to task {currentIndex}");
    }

    public void ResetTasks()
    {
        var old = GetCurrentTaskData();
        UnsubscribeFromTask(old);

        currentIndex = 0;
        GameSaveManager.instance.SaveCurrentTask(currentIndex);

        DialogueCatalog.instance.ReloadForActiveScene();

        UpdateTaskUI();
        OnTaskChanged?.Invoke(GetCurrentTaskData());

        SubscribeToTask(GetCurrentTaskData());
        _autoPlayedStates.Clear();
    }

    public int GetCurrentTaskIndex() => currentIndex;

    private void UpdateTaskUI()
    {
        uiManager?.SetTask(GetCurrentTaskData()?.description ?? string.Empty);
    }

    private void LoadTasksFromJson()
    {
        var ta = Resources.Load<TextAsset>("Tasks");
        if (ta == null) { tasks = new TaskData[0]; return; }
        tasks = JsonUtility.FromJson<TaskList>(ta.text).tasks;
    }

    public TaskData GetCurrentTaskData() => tasks.Length > 0 ? tasks[currentIndex] : null;
}

[Serializable]
public class CharacterMapping
{
    public string characterName;
    public GameObject prefab;
    public Transform spawnPoint;
}

[Serializable]
public class TaskData
{
    public int id;
    public string description;
    public string triggerType;
    public string triggerParam;
    public bool hasCutscene;
}

[Serializable]
public class TaskList
{
    public TaskData[] tasks;
}