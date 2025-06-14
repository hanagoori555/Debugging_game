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
    private string _previousGameScene;
    private int _currentBattleNumber = 0;
    private bool _isRhythmRunning = false;
    private bool _returningFromRhythm = false;
    private bool _rhythmDone = false;
    // true, если мы только что перешли по SceneExit и ждём загрузки новой сцены
    private bool _returningFromSceneExit = false;

    public event Action<TaskData> OnTaskChanged;
    private HashSet<int> _autoPlayedStates = new HashSet<int>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadTasksFromJson();
            // **загружаем сохранённый индекс задачи (если есть)**
            currentIndex = GameSaveManager.instance?.LoadCurrentTask() ?? 0;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1) Если мы только что вернулись из ритма — делаем NextTask и выходим
        if (_returningFromRhythm)
        {
            _returningFromRhythm = false;
            PlayerController.InputBlocked = false;
            NextTask();
            return;
        }

        // 2) возврат по выходу из сцены
        if (_returningFromSceneExit)
        {
            _returningFromSceneExit = false;
            // телепорт и прочая инициализация сцены пройдёт в обычном блоке ниже,
            // но сначала переключаем задачу
            NextTask();
            // (!) не return, чтобы подписать новую задачу на этот же кадр
        }

        // 3) Если загрузилась сама сцена "Battle" — ничего не трогаем
        if (scene.name == "Battle")
            return;

        // Обычная загрузка игровой сцены:
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

    public void HandleUnsubscribeAll()
    {
        Interactable.OnAnyInteract -= OnInteractTrigger;
        SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;
        ComputerInterface.OnCorrectCommandEntered -= OnConsoleAccepted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void SubscribeToTask(TaskData task, bool suppressAuto = false)
    {
        if (task.id == 31 || task.id == 40)
            _rhythmDone = false;
        if (task == null) return;
        if (suppressAuto && task.triggerType == "Auto")
        {
            // только подписка на интеракт или SceneExit дальше
            if (task.triggerType == "Interact")
                Interactable.OnAnyInteract += OnInteractTrigger;
            else if (task.triggerType == "SceneExit")
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
            return;
        }

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
                    FindObjectOfType<ComputerInterface>()?.Show();
                }
                else
                {
                    Interactable.OnAnyInteract += OnInteractTrigger;
                }
                break;

            case "SceneExit":
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
                break;

            case "Rhythm":
                if ((task.id == 32 || task.id == 41) && !_rhythmDone)
                {
                    Debug.Log($"[TaskManager] Subscribing to Rhythm task {task.id}");
                    StartCoroutine(StartRhythmTaskAsync(task));
                }
                else
                {
                    Debug.LogWarning($"[TaskManager] Rhythm task {task.id} skipped - not allowed");
                    NextTask(); // Пропускаем задачу
                }
                break;

            case "SceneAuto":
                // сразу же запускаем переход
                StartCoroutine(AutoSceneTransition(task.triggerParam));
                break;

            case "BackgroundTransition":
                StageManager.OnBackgroundTransition += HandleBackgroundTransition;
                break;
        }
    }

    /// <summary>
    /// Сработает, когда StageManager вызовет OnBackgroundTransition.
    /// Переходит к следующей задаче, но только если это наша текущая BackgroundTransition‑задача.
    /// </summary>
    private void HandleBackgroundTransition()
    {
        // 1) сразу блокируем ходьбу (дублируем блокировку из StageManager)
        PlayerController.InputBlocked = true;

        // 2) отписываемся, чтобы не дергаться дважды
        StageManager.OnBackgroundTransition -= HandleBackgroundTransition;

        // 3) запускаем авто‑диалог (если он есть)
        var lines = DialogueCatalog.instance.GetAutoDialogueForCurrentState();
        if (lines != null && lines.Length > 0)
        {
            DialogueManager.instance.ShowDialogue(lines, () =>
            {
                // после диалога – разблокируем и NextTask
                PlayerController.InputBlocked = false;
                NextTask();
            });
        }
        else
        {
            // если диалога нет – сразу разблокировать и NextTask
            PlayerController.InputBlocked = false;
            NextTask();
        }
    }

    private IEnumerator StartRhythmTaskAsync(TaskData task)
    {
        if((task.id != 32 && task.id != 41) || _rhythmDone)
             yield break;

        Debug.Log($"[TaskManager] Enter StartRhythmTaskAsync for task {task.id}");
        if (_isRhythmRunning)
        {
            Debug.Log("[TaskManager] Rhythm already running, abort");
            yield break;
        }

        // Остановка фоновой музыки
        if (MusicManager.instance != null)
        {
            Debug.Log("[TaskManager] Stopping background music for rhythm task");
            MusicManager.instance.StopMusic();
        }

        // Парсим номер боя
        if (!int.TryParse(task.triggerParam, out int battleNumber))
        {
            Debug.LogError($"[TaskManager] Bad rhythm‑param '{task.triggerParam}'");
            yield break;
        }

        _isRhythmRunning = true;
        _currentBattleNumber = battleNumber;
        _previousGameScene = SceneManager.GetActiveScene().name;

        // Загружаем сцену боя
        Debug.Log($"[TaskManager] Loading Battle scene for battle #{battleNumber}");
        var op = SceneManager.LoadSceneAsync("Battle");
        yield return op;
        yield return null;

        // Подписываемся и запускаем
        Debug.Log("[TaskManager] Battle scene loaded, hooking OnRhythmFinished");
        RhythmGameManager.instance.OnRhythmFinished += OnRhythmFinished;
        RhythmGameManager.instance.EnterRhythmMode(battleNumber);
    }

    private void OnRhythmFinished()
    {
        // Отписка от эвента ритма
        if (RhythmGameManager.instance != null)
            RhythmGameManager.instance.OnRhythmFinished -= OnRhythmFinished;

        _isRhythmRunning = false;
        _rhythmDone = true;

        // Возвращаемся в нужную сцену
        int taskId = GetCurrentTaskIndex();
        if (taskId != 32 && taskId != 41)
        {
            Debug.LogError($"[TaskManager] Invalid rhythm task {taskId} completed!");
            return;
        }
        string returnScene = taskId switch
        {
            32 => "DarkWorld",
            41 => "School",
            _ => _previousGameScene
        };
        Debug.Log($"[TaskManager] Returning to scene '{returnScene}' for task {taskId}");

        // ставим флаг, что следующая загрузка — это возврат из ритма
        _returningFromRhythm = true;
        SceneManager.LoadScene(returnScene);
    }

    //private void OnReturnedFromRhythm(Scene scene, LoadSceneMode mode)
    //{
    //    // Отписываемся
    //    SceneManager.sceneLoaded -= OnReturnedFromRhythm;

    //    // Разблокируем ввод
    //    PlayerController.InputBlocked = false;

    //    // Переходим к следующей задаче **один** раз
    //    NextTask();
    //}


    private IEnumerator AutoSceneTransition(string sceneName)
    {
        yield return null;
        // отписаться и подписаться
        SceneManager.sceneLoaded -= OnSceneLoaded_MovePlayer;
        SceneManager.sceneLoaded += OnSceneLoaded_MovePlayer;

        // запоминаем, что эту задачу мы ещё не завершили
        int autoTaskIndex = currentIndex;

        // загружаем сцену
        SceneManager.LoadScene(sceneName);

        // ждём, пока сцена подгрузится и игрок телепортируется
        // (можем чуть подождать кадр)
        yield return new WaitForEndOfFrame();

        // **теперь** считаем автозадачу выполненной и переключаемся на следующую
        if (currentIndex == autoTaskIndex)
            NextTask();
    }

    private void OnSceneLoaded_MovePlayer(Scene scene, LoadSceneMode mode)
    {
        // 1) Телепортируем игрока
        var sp = GameObject.FindWithTag("SpawnPoint");
        if (sp != null)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null)
                player.TeleportTo(sp.transform.position);
        }
        else
        {
            Debug.LogWarning($"[TaskManager] Не найден SpawnPoint в сцене {scene.name}");
        }

        // 2) Отписываемся, чтобы не ловить это событие повторно
        SceneManager.sceneLoaded -= OnSceneLoaded_MovePlayer;

        // НИКАКОГО NextTask() здесь больше не делаем!
        // (Переключение задачи мы сделаем сразу после запуска перехода — см. ниже)
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
            case "BackgroundTransition":
                StageManager.OnBackgroundTransition -= HandleBackgroundTransition;
                break;
        }
    }

    private IEnumerator AutoTriggerCoroutine(TaskData task)
    {
        Debug.Log($"[TaskManager] ➤ AutoTriggerCoroutine START for task {task.id}, hasCutscene={task.hasCutscene}");
        yield return null;

        if (_autoPlayedStates.Contains(task.id))
        {
            Debug.Log($"[TaskManager] ➤ AutoTrigger skipped: already played for task {task.id}");
            yield break;
        }
        _autoPlayedStates.Add(task.id);

        // Если это Auto-задача с катсценой - пропускаем стандартную обработку
        if (task.hasCutscene)
        {
            Debug.Log($"[TaskManager] ➤ AutoTrigger skipped: hasCutscene for task {task.id}");
            yield break;
        }

        // Стандартная обработка для Auto-задач без катсцены
        var autoLines = DialogueCatalog.instance.GetAutoDialogueForCurrentState();
        Debug.Log($"[TaskManager] ➤ AutoTrigger found {autoLines?.Length ?? 0} autoLines for task {task.id}");
        if (autoLines != null && autoLines.Length > 0)
        {
            SpawnAndShow(autoLines);
            yield break;
        }

        var interactLines = DialogueCatalog.instance.GetInteractableLines(task.triggerParam);
        Debug.Log($"[TaskManager] ➤ AutoTrigger found {interactLines.Length} interactLines for task {task.id}");
        if (interactLines.Length > 0)
        {
            DialogueManager.instance.ShowDialogue(interactLines, NextTask);
            yield break;
        }

        Debug.Log($"[TaskManager] ➤ AutoTrigger: no lines, calling NextTask for task {task.id}");
        NextTask();
    }

    private void SpawnAndShow(DialogueLine[] lines)
    {
        Debug.Log($"[TaskManager] ➤ SpawnAndShow called with {lines.Length} lines");

        // 1) Сразу собираем список уникальных говорящих (кроме “Рассказчик” и пустых)
        var speakerNames = lines
            .Select(l => l.characterName)
            .Where(name => !string.IsNullOrEmpty(name) && name != "Рассказчик")
            .Distinct()
            .ToList();

        Debug.Log($"[TaskManager] ➤ Will spawn characters: {string.Join(", ", speakerNames)}");

        // 2) Для каждого - пробуем спавнить prefab
        List<GameObject> spawnedCharacters = new List<GameObject>();
        foreach (var speaker in speakerNames)
        {
            Debug.Log($"[TaskManager] ➤ Trying to spawn prefab for '{speaker}'");
            var map = characterMappings.FirstOrDefault(m => m.characterName == speaker);
            if (map != null && map.prefab != null)
            {
                Transform spawnAt = map.spawnPoint != null ? map.spawnPoint : defaultSpawnPoint;
                Vector3 pos = spawnAt != null ? spawnAt.position : Vector3.zero;
                var go = Instantiate(map.prefab, pos, Quaternion.identity);
                spawnedCharacters.Add(go);
                Debug.Log($"[TaskManager] ➤ Spawned prefab for '{speaker}' at {pos}");
            }
            else
            {
                Debug.Log($"[TaskManager] ➤ No prefab for '{speaker}', skipping spawn");
            }
        }

        // 3) Запускаем сам диалог
        Debug.Log($"[TaskManager] ➤ Showing dialogue with {lines.Length} lines");
        DialogueManager.instance.ShowDialogue(lines, () =>
        {
            Debug.Log($"[TaskManager] ➤ Dialogue complete, destroying spawned characters and calling NextTask");
            // Снимаем блокировку управления
            PlayerController.InputBlocked = false;

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
            DialogueManager.instance.ShowDialogue(lines, () =>
            {
                PlayerController.InputBlocked = false;
                NextTask();
            });
        else
            NextTask();
    }

    private void OnSceneExitTrigger(string sceneName)
    {
        // отписаться, чтобы не дергаться дважды
        SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;

        Debug.Log($"[TaskManager] SceneExit → loading '{sceneName}'");
        // ставим флаг, что на следующей загрузке надо сделать NextTask
        _returningFromSceneExit = true;
        SceneManager.LoadScene(sceneName);
    }


    private void OnBackgroundTransition()
    {
        // отписываемся
        StageManager.OnBackgroundTransition -= OnBackgroundTransition;

        // 1) разблокируем ввод
        var player = FindObjectOfType<PlayerController>();
        if (player != null) PlayerController.InputBlocked = false;

        // 2) переходим к следующей задаче
        NextTask();
    }


    public void NextTask()
    {
        // если уже на последней задаче — в меню:
        if (currentIndex >= tasks.Length - 1)
        {
            Debug.Log("[TaskManager] All tasks done → back to MainMenu");
            SceneManager.LoadScene("MainMenu");
            return;
        }

        var oldTask = GetCurrentTaskData();
        UnsubscribeFromTask(oldTask);

        currentIndex = Mathf.Min(currentIndex + 1, tasks.Length - 1);
        // GameSaveManager.instance.SaveCurrentTask(currentIndex);

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

    public void UpdateTaskUI()
    {
        uiManager?.SetTask(GetCurrentTaskData()?.description ?? string.Empty);
    }

    private void LoadTasksFromJson()
    {
        var ta = Resources.Load<TextAsset>("Tasks");
        if (ta == null) { tasks = new TaskData[0]; return; }
        tasks = JsonUtility.FromJson<TaskList>(ta.text).tasks;
    }

    /// <summary>
    /// Устанавливает текущую задачу. 
    /// Если isLoading == true, пропускаем стартовые действия (катсцены).
    /// </summary>
    public void SetCurrentTaskIndex(int index, bool isLoading = false)
    {
        // 1) Обрезаем в диапазон
        index = Mathf.Clamp(index, 0, tasks.Length - 1);
        // 2) Сбрасываем предыдущие подписки...
        UnsubscribeFromTask(GetCurrentTaskData());
        // 3) Устанавливаем
        currentIndex = index;

        // Если мы в режиме загрузки из Continue — сбрасываем все автозапуски,
        // чтобы AutoTrigger снова мог прогреться для сохранённых задач
        if (isLoading)
            {
                Debug.Log($"[TaskManager] ContinueGame: Clearing auto‑play states");
                _autoPlayedStates.Clear();
            }

        // Сохраняем только если не в режиме загрузки
        if (!isLoading)
        {
            GameSaveManager.instance.SaveCurrentTask(currentIndex);
        }

        DialogueCatalog.instance.RefreshState();
        UpdateTaskUI();
        // 4) Подписываем заново, но если isLoading — не браним автокатсцены
        SubscribeToTask(GetCurrentTaskData(), suppressAuto: isLoading);
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