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

    [Header("Task -> Spawn mappings (optional)")]
    public List<SpawnMapping> spawnMappings = new List<SpawnMapping>();

    [Header("Model variant task IDs (configure in inspector)")]
    // Если задача входит в variant1TaskIds -> применяем variant 1
    // Если входит в variant2TaskIds -> применяем variant 2 (приоритетнее)
    public List<int> variant1TaskIds = new List<int>() { 35, 36, 37, 38};
    public List<int> variant2TaskIds = new List<int>() { 39, 40, 41, 42 };

    [Header("Gameplay scenes (used to block cutscenes in menus)")]
    public List<string> gameplayScenes = new List<string> { "School", "Forest", "Home", "DarkWorld", "Zone", "Battle" };

    private UIManager uiManager;
    private int currentIndex = 0;
    private string _previousGameScene;
    private int _currentBattleNumber = 0;
    private bool _isRhythmRunning = false;
    private bool _returningFromRhythm = false;
    private bool _rhythmDone = false;
    private int _subscribedTaskId = -1;
    // флаг: если >=0 — id задачи, для которой единожды надо подавить катсцену
    private int _suppressCutsceneForTaskId = -1;
    // true, если мы только что перешли по SceneExit и ждём загрузки новой сцены
    private bool _returningFromSceneExit = false;
    // true, если SetCurrentTaskIndex(..., isLoading:true) вызван (Continue из меню)
    public bool IsContinueMode { get; private set; } = false;
    // --- new fields for deferred SceneExit teleport ---
    private int _sceneExitInitiatorTaskId = -1;
    private bool _hasDeferredSpawn = false;
    private Vector3 _deferredSpawnPosition = Vector3.zero;

    public event Action<TaskData> OnTaskChanged;
    private HashSet<int> _autoPlayedStates = new HashSet<int>();
    private List<SceneSpawnPoint> _sceneSpawnPoints = new List<SceneSpawnPoint>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadTasksFromJson();
            currentIndex = 0;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private bool IsGameplayScene(string name)
    {
        return gameplayScenes != null && gameplayScenes.Contains(name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[TaskManager] OnSceneLoaded: scene={scene.name}, currentTaskIndex={currentIndex}, currentTaskId={(GetCurrentTaskData()?.id.ToString() ?? "null")}");

        // 1) Если мы только что вернулись из ритма — делаем NextTask и выходим
        if (_returningFromRhythm)
        {
            Debug.Log("[TaskManager] OnSceneLoaded: returning from rhythm -> NextTask()");
            _returningFromRhythm = false;
            PlayerController.InputBlocked = false;
            NextTask();
            return;
        }

        // 2) возврат по выходу из сцены (SceneExit)
        if (_returningFromSceneExit)
        {
            Debug.Log("[TaskManager] OnSceneLoaded: handling returningFromSceneExit");
            _returningFromSceneExit = false;

            int initiatorId = _sceneExitInitiatorTaskId;
            _sceneExitInitiatorTaskId = -1; // очистим

            if (initiatorId != -1)
            {
                Debug.Log($"[TaskManager] Returned from SceneExit initiated by task {initiatorId}. Trying to teleport for initiating task before NextTask.");

                // попробуем найти спавн для задачи-инициатора
                var spawn = GetSpawnForTask(initiatorId, scene.name);

                if (spawn != null)
                {
                    var player = FindFirstObjectByType<PlayerController>();
                    if (player != null)
                    {
                        Debug.Log($"[TaskManager] Teleporting player for initiator task {initiatorId} to {spawn.position}");
                        player.TeleportTo(spawn.position);
                    }
                    else
                    {
                        // игрок ещё не создан — запомним позицию, чтобы применить в PlayerController.Start()
                        Debug.Log($"[TaskManager] Player not found now — deferring teleport to {spawn.position}");
                        _hasDeferredSpawn = true;
                        _deferredSpawnPosition = spawn.position;
                    }
                }
                else
                {
                    Debug.Log($"[TaskManager] No spawn found for initiator task {initiatorId}, scene {scene.name}");
                }
            }

            // теперь переключаемся на следующую задачу (как было задумано)
            NextTask();
            // (!) не return — чтобы подписать новую задачу на этом же кадре
        }

        // Если не игровая сцена — ничего не делаем (чтобы не запускать катсцены/авто-диалоги в меню).
        if (!IsGameplayScene(scene.name))
        {
            Debug.Log($"[TaskManager] Scene '{scene.name}' not a gameplay scene -> skipping task subscription.");
            return;
        }

        // 3) Если загрузилась сама сцена "Battle" — ничего не трогаем
        if (scene.name == "Battle")
        {
            Debug.Log("[TaskManager] Battle scene loaded -> skip normal scene subscription.");
            return;
        }

        // Очистим старые регистрационные спавнпоинты и позвольм новым зарегистрироваться
        ClearSceneSpawnPoints();

        // Подстраховка: зарегистрировать уже существующие SceneSpawnPoint'ы в сцене
        foreach (var sceneSpawn in UnityEngine.Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.InstanceID))
        {
            RegisterSceneSpawnPoint(sceneSpawn);
        }

        // Обычная загрузка игровой сцены: UI, обновление таска и т.д.
        uiManager = FindFirstObjectByType<UIManager>();
        UpdateTaskUI();

        // ищем объект Spawn_Default
        var defaultSp = GameObject.Find("Spawn_Default");
        if (defaultSp != null) defaultSpawnPoint = defaultSp.transform;

        // Если мы сейчас в ContinueMode, то SetCurrentTaskIndex(...) уже подписал задачу в suppressAuto режиме.
        // Не подписываем заново, чтобы не снять suppressAuto/не запустить автосцену повторно.
        if (IsContinueMode)
        {
            Debug.Log("[TaskManager] OnSceneLoaded: ContinueMode active -> skipping SubscribeToTask (already handled).");
        }
        else
        {
            SubscribeToTask(GetCurrentTaskData());
        }
    }

    void Start()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        UpdateTaskUI();
    }

    void OnDestroy()
    {
        HandleUnsubscribeAll();
    }

    // helper для сброса флага извне (например, из PlayerController после применения чекпоинта)
    public void ConsumeContinueMode()
    {
        IsContinueMode = false;
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
        if (task == null) return;

        // Защита от повторной подписки
        if (_subscribedTaskId == task.id)
        {
            Debug.Log($"[TaskManager] SubscribeToTask: already subscribed to task {task.id}, skipping.");
            return;
        }

        // special: reset rhythm flags for those tasks
        if (task.id == 31 || task.id == 40)
            _rhythmDone = false;

        // если мы в режиме Continue (isLoading) — НЕ дергаем авто/катсцены/ритм,
        // а только делаем "минимальную" подписку, чтобы игрок мог взаимодействовать.
        if (suppressAuto)
        {
            Debug.Log($"[TaskManager] SubscribeToTask(suppressAuto=true) for task {task.id}: minimal subscription only.");

            // tutorial (id==0) — не запускать при Continue
            if (task.id == 0)
            {
                Debug.Log("[TaskManager] Suppressing tutorial on Continue.");
                return; // не помечаем как подписанное
            }

            // минимальная подписка: только те триггеры, которые должны сработать в момент Continue
            switch (task.triggerType)
            {
                case "Interact":
                    Interactable.OnAnyInteract += OnInteractTrigger;
                    break;
                case "SceneExit":
                    SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
                    break;
                case "Rhythm":
                    Debug.Log($"[TaskManager] Suppressing Rhythm start for task {task.id} during Continue.");
                    break;
                case "Auto":
                case "SceneAuto":
                case "BackgroundTransition":
                    Debug.Log($"[TaskManager] Suppressing automatic trigger '{task.triggerType}' for task {task.id} during Continue.");
                    break;
                default:
                    Debug.Log($"[TaskManager] Unknown trigger '{task.triggerType}' for task {task.id} in suppressAuto mode.");
                    break;
            }

            // пометить, что для этой задачи выполнена минимальная подписка
            _subscribedTaskId = task.id;
            return;
        }

        // --- обычный путь (не continue) ---

        // tutorial
        if (task.id == 0)
        {
            _subscribedTaskId = task.id;
            FindFirstObjectByType<MainGameController>()?.StartTutorial();
            return;
        }

        // Для задач с катсценой — запускаем катсцену (если сцена игровая, проверка внутри StartCutsceneForTask)
        if (task.hasCutscene)
        {
            // Помечаем, что начали обработку этой задачи (чтобы не дублировать)
            _subscribedTaskId = task.id;

            StartCutsceneForTask(task, () => {
                Debug.Log($"[TaskManager] Cutscene finished for task {task.id}");

                // Подписываемся на OnAnyInteract только после завершения катсцены (как и было)
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
            // Для задач без катсцены — стандартная подписка
            _subscribedTaskId = task.id;
            SubscribeToTaskTrigger(task);
        }
    }

    private void StartCutsceneForTask(TaskData task, Action onCutsceneComplete)
    {
        // 0) если для этой задачи задано одноразовое подавление — сработаем как в suppressAuto:
        if (_suppressCutsceneForTaskId == task.id)
        {
            Debug.Log($"[TaskManager] Suppressing cutscene for task {task.id} due to continue suppression (one-shot).");
            // очистим флаг (одна подмена)
            _suppressCutsceneForTaskId = -1;

            // ведём себя как при suppressAuto: минимально подписываемся в зависимости от типа триггера
            if (task.triggerType == "Interact")
                Interactable.OnAnyInteract += OnInteractTrigger;
            else if (task.triggerType == "SceneExit")
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
            // Для Auto/SceneAuto/BackgroundTransition — ничего не делаем (они будут запускаться через TriggerAutoAfterLoad/TryRunAuto...)
            return;
        }

        // Если мы в режиме Continue, НЕ запускаем катсцены автоматически.
        // Вместо этого делаем минимальную подписку (как при suppressAuto).
        if (IsContinueMode)
        {
            Debug.Log($"[TaskManager] Skipping cutscene for task {task.id} because ContinueMode is active.");
            if (task.triggerType == "Interact")
                Interactable.OnAnyInteract += OnInteractTrigger;
            else if (task.triggerType == "SceneExit")
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
            // Если это Auto и был cutscene — не продвигаем индекс автоматически на Continue.
            return;
        }

        // Не позволяем стартовать катсцену в меню/на не-игровой сцене.
        var active = SceneManager.GetActiveScene().name;
        if (!IsGameplayScene(active))
        {
            Debug.Log($"[TaskManager] Not starting cutscene for task {task.id} because current scene '{active}' is not a gameplay scene.");
            if (task.triggerType == "Interact")
                Interactable.OnAnyInteract += OnInteractTrigger;
            else if (task.triggerType == "SceneExit")
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
            return;
        }

        if (CutsceneController.instance == null)
        {
            Debug.LogWarning($"[TaskManager] CutsceneController.instance == null, cannot start cutscene for task {task.id}");
            onCutsceneComplete?.Invoke();
            return;
        }

        Debug.Log($"[TaskManager] Starting cutscene for task {task.id}");
        CutsceneController.instance.StartCutsceneForCurrentState(() => {
            Debug.Log($"[TaskManager] Cutscene finished for task {task.id}");
            onCutsceneComplete?.Invoke();
        });
    }


    private void SubscribeToTaskTrigger(TaskData task)
    {
        // Защита от срабатывания триггеров при ContinueMode
        if (IsContinueMode)
        {
            Debug.Log($"[TaskManager] SubscribeToTaskTrigger: skipping automatic triggers for task {task.id} because ContinueMode active.");
            // Но подписываем минимально интеракт/sceneExit, чтобы игрок мог взаимодействовать
            if (task.triggerType == "Interact")
                Interactable.OnAnyInteract += OnInteractTrigger;
            else if (task.triggerType == "SceneExit")
                SceneExitDetector.OnSceneExit += OnSceneExitTrigger;
            return;
        }

        switch (task.triggerType)
        {
            case "Auto":
                StartCoroutine(AutoTriggerCoroutine(task));
                break;

            case "Interact":
                if (task.id == 9)
                {
                    ComputerInterface.OnCorrectCommandEntered += OnConsoleAccepted;
                    FindFirstObjectByType<ComputerInterface>()?.Show();
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
        Debug.Log($"[TaskManager] OnSceneLoaded_MovePlayer: scene={scene.name}, currentTask={GetCurrentTaskIndex()}");

        // Continue/Checkpoint handling
        if (IsContinueMode && GameSaveManager.instance != null && GameSaveManager.instance.HasCheckpoint())
        {
            string savedScene = GameSaveManager.instance.GetSavedScene();
            if (savedScene == scene.name)
            {
                Vector2 pos = GameSaveManager.instance.LoadCheckpointPosition();
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    Debug.Log($"[TaskManager] MovePlayer (Continue mode) → teleporting to checkpoint pos {pos}");
                    player.TeleportTo(pos);
                }
                else
                {
                    Debug.LogWarning("[TaskManager] MovePlayer: player not found to teleport to checkpoint");
                }

                ConsumeContinueMode();
                SceneManager.sceneLoaded -= OnSceneLoaded_MovePlayer;
                return;
            }
        }

        // Основной путь: пробуем найти spawn, привязанный к текущей задаче
        var currentTask = GetCurrentTaskData();
        Transform spawn = null;
        if (currentTask != null)
        {
            spawn = GetSpawnForTask(currentTask.id, scene.name);
        }

        if (spawn != null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Debug.Log($"[TaskManager] MovePlayer: teleporting player to spawn for task {GetCurrentTaskIndex()} at {spawn.position}");
                player.TeleportTo(spawn.position);
            }
            else
            {
                Debug.LogWarning("[TaskManager] MovePlayer: player not found to teleport");
            }
            SceneManager.sceneLoaded -= OnSceneLoaded_MovePlayer;
            return;
        }

        // не нашли подходящий spawn — fallback к тегу / default уже выполнялся в GetSpawnForTask,
        // но мы не хотим телепортировать в generic spawn автоматически при загрузке,
        // если явно не задано ничего для текущей задачи. --> поэтому здесь просто отписываемся.
        Debug.Log($"[TaskManager] MovePlayer: no task-specific spawn found for task {currentTask?.id}. No auto-teleport on scene load.");
        SceneManager.sceneLoaded -= OnSceneLoaded_MovePlayer;
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

        // Сбрасываем пометку подписки, если это та же задача
        if (task != null && _subscribedTaskId == task.id)
            _subscribedTaskId = -1;
    }

    private IEnumerator AutoTriggerCoroutine(TaskData task)
    {
        Debug.Log($"[TaskManager] ➤ AutoTriggerCoroutine START for task {task.id}, hasCutscene={task.hasCutscene}");
        yield return null;

        // Если мы в режиме Continue — не запускаем авто-обработку, чтобы она не продвинула задачи
        if (IsContinueMode)
        {
            Debug.Log($"[TaskManager] ➤ AutoTrigger skipped for task {task.id} because ContinueMode is active.");
            yield break;
        }

        if (_autoPlayedStates.Contains(task.id))
        {
            Debug.Log($"[TaskManager] ➤ AutoTrigger skipped: already played for task {task.id}");
            yield break;
        }
        _autoPlayedStates.Add(task.id);

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

    /// <summary>
    /// Попытаться запустить авто-поведение для текущей задачи (Auto / SceneAuto).
    /// Вызывается после того, как мы применили continue и сняли IsContinueMode.
    /// </summary>
    public void TryRunAutoForCurrentTaskAfterContinue()
    {
        var task = GetCurrentTaskData();
        if (task == null) return;

        // Если задача имеет катсцену — мы НЕ хотим её стартовать при Continue
        if (task.hasCutscene)
        {
            Debug.Log($"[TaskManager] TryRunAutoForCurrentTaskAfterContinue: current task {task.id} hasCutscene -> skip auto-run (cutscene suppressed).");
            return;
        }

        if (task.triggerType == "Auto")
        {
            StartCoroutine(AutoTriggerCoroutine(task));
        }
        else if (task.triggerType == "SceneAuto")
        {
            StartCoroutine(AutoSceneTransition(task.triggerParam));
        }
        else
        {
            Debug.Log($"[TaskManager] TryRunAutoForCurrentTaskAfterContinue: nothing to auto-run for triggerType='{task.triggerType}'");
        }
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

        Debug.Log($"[TaskManager] OnInteractTrigger received id='{objectId}'. CurrentIndex={GetCurrentTaskIndex()} taskId={(task != null ? task.id : -1)} triggerType={(task != null ? task.triggerType : "null")} triggerParam={(task != null ? task.triggerParam : "null")}");

        // 1) Если текущая задача явно ожидает этот интеракт -> поведение как раньше (показ диалога и NextTask)
        if (task != null && task.triggerType == "Interact" && task.triggerParam == objectId)
        {
            Debug.Log($"[TaskManager] OnInteractTrigger called for id='{objectId}', task={task.id}");
            var lines = DialogueCatalog.instance.GetInteractableLines(objectId);
            if (lines.Length > 0)
            {
                DialogueManager.instance.ShowDialogue(lines, () =>
                {
                    // помечаем, что игрок с этим интерактивом поговорил
                    DialogueManager.MarkInteractionCompleted(objectId);

                    PlayerController.InputBlocked = false;
                    NextTask();
                });
            }
            else
            {
                // если диалога нет — всё равно пометим и продвинем задачу
                DialogueManager.MarkInteractionCompleted(objectId);
                NextTask();
            }

            return;
        }

        // 2) Если текущая задача НЕ ожидает этот объект — попытаться показать опциональный интеракт (без NextTask)
        var optionalLines = DialogueCatalog.instance.GetInteractableLines(objectId);
        if (optionalLines != null && optionalLines.Length > 0)
        {
            Debug.Log($"[TaskManager] Showing optional interact dialogue for '{objectId}' (not part of current task).");
            DialogueManager.instance.ShowDialogue(optionalLines, () =>
            {
                // Для опциональных интерактов мы обычно НЕ продвигаем задачу.
                // Можно пометить как "просмотренное" взаимодействие, если нужно:
                DialogueManager.MarkInteractionCompleted(objectId);

                PlayerController.InputBlocked = false;
                // НЕ вызываем NextTask()
            });
            return;
        }

        // 3) Ничего не найдено — игнорируем (или логируем)
        Debug.Log($"[TaskManager] OnInteractTrigger: no dialogue found for '{objectId}' and it's not expected by current task.");
    }


    private void OnSceneExitTrigger(string sceneName)
    {
        // отписаться, чтобы не дергаться дважды
        SceneExitDetector.OnSceneExit -= OnSceneExitTrigger;

        Debug.Log($"[TaskManager] SceneExit → loading '{sceneName}'");

        // запомним id задачи, которая инициировала переход — чтобы потом телепортнуть по её спавну
        _sceneExitInitiatorTaskId = GetCurrentTaskData()?.id ?? -1;

        // ставим флаг, что на следующей загрузке надо сделать NextTask
        _returningFromSceneExit = true;
        SceneManager.LoadScene(sceneName);
    }

    private void OnBackgroundTransition()
    {
        // отписываемся
        StageManager.OnBackgroundTransition -= OnBackgroundTransition;

        // 1) разблокируем ввод
        var player = FindFirstObjectByType<PlayerController>();
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

        // применяем модель для новой задачи
        ApplyModelVariantForTask(GetCurrentTaskData());

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
        if (index == currentIndex)
        {
            Debug.Log($"[TaskManager] SetCurrentTaskIndex called with same index {index} — refresh only.");
            if (isLoading) { _autoPlayedStates.Clear(); IsContinueMode = true; }
            DialogueCatalog.instance.RefreshState();
            UpdateTaskUI();
            SubscribeToTask(GetCurrentTaskData(), suppressAuto: isLoading);
            return;
        }
        // 2) Сбрасываем предыдущие подписки...
        UnsubscribeFromTask(GetCurrentTaskData());
        // 3) Устанавливаем
        currentIndex = index;

        if (isLoading)
        {
            Debug.Log($"[TaskManager] ContinueGame: Clearing auto-play states and enabling ContinueMode");
            _autoPlayedStates.Clear();
            IsContinueMode = true;
            _subscribedTaskId = -1; // явно очистить
        }
        else
        {
            IsContinueMode = false;
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

        // применяем модель (важно при загрузке из Continue/SetCurrentTaskIndex)
        ApplyModelVariantForTask(GetCurrentTaskData());
    }

    public bool HasSpawnForTask(int taskId)
    {
        return spawnMappings.Any(s => s.taskId == taskId);
    }

    public Transform GetSpawnForTask(int taskId, string sceneName = null)
    {
        Debug.Log($"[TaskManager] GetSpawnForTask called: taskId={taskId}, sceneName={sceneName}");

        // debug: show spawnMappings
        if (spawnMappings != null && spawnMappings.Count > 0)
        {
            Debug.Log($"[TaskManager] spawnMappings.Count = {spawnMappings.Count}");
            for (int i = 0; i < spawnMappings.Count; i++)
            {
                var m = spawnMappings[i];
                Debug.Log($"  mapping[{i}]: taskId={m.taskId}, sceneName='{m.sceneName}', spawnObjectName='{m.spawnObjectName}'");
            }
        }
        else
        {
            Debug.Log("[TaskManager] spawnMappings empty");
        }

        // 1) mapping task+scene (priority #1)
        if (!string.IsNullOrEmpty(sceneName))
        {
            var byBoth = spawnMappings.FirstOrDefault(s => s.taskId == taskId
                                                           && !string.IsNullOrEmpty(s.sceneName)
                                                           && s.sceneName == sceneName);
            if (byBoth != null)
            {
                Debug.Log($"[TaskManager] Found mapping (task+scene) for task {taskId} -> '{byBoth.spawnObjectName}'");
                if (!string.IsNullOrEmpty(byBoth.spawnObjectName))
                {
                    var go = GameObject.Find(byBoth.spawnObjectName);
                    if (go != null)
                    {
                        Debug.Log($"[TaskManager] Using GameObject '{byBoth.spawnObjectName}' from mapping(task+scene) at {go.transform.position}");
                        return go.transform;
                    }
                    Debug.LogWarning($"[TaskManager] mapping(task+scene) exists but GameObject '{byBoth.spawnObjectName}' not found in scene '{sceneName}' (name mismatch or inactive)");
                }
                else
                {
                    Debug.LogWarning($"[TaskManager] mapping(task+scene) for task {taskId} has empty spawnObjectName");
                }
            }
        }

        // 2) mapping task-only (priority #2)
        var byTask = spawnMappings.FirstOrDefault(s => s.taskId == taskId && string.IsNullOrEmpty(s.sceneName));
        if (byTask != null)
        {
            Debug.Log($"[TaskManager] Found mapping (task-only) for task {taskId} -> '{byTask.spawnObjectName}'");
            if (!string.IsNullOrEmpty(byTask.spawnObjectName))
            {
                var go2 = GameObject.Find(byTask.spawnObjectName);
                if (go2 != null)
                {
                    Debug.Log($"[TaskManager] Using GameObject '{byTask.spawnObjectName}' from mapping(task-only) at {go2.transform.position}");
                    return go2.transform;
                }
                Debug.LogWarning($"[TaskManager] mapping(task-only) exists but GameObject '{byTask.spawnObjectName}' not found in scene '{sceneName}'");
            }
            else
            {
                Debug.LogWarning($"[TaskManager] mapping(task-only) for task {taskId} has empty spawnObjectName");
            }
        }

        // 3) SceneSpawnPoint компоненты, зарегистрированные в этой сцене (priority #3)
        if (_sceneSpawnPoints != null && _sceneSpawnPoints.Count > 0)
        {
            Debug.Log($"[TaskManager] _sceneSpawnPoints.Count = {_sceneSpawnPoints.Count}");
            foreach (var sp in _sceneSpawnPoints)
            {
                if (sp == null) continue;
                Debug.Log($"  SceneSpawnPoint: name='{sp.gameObject.name}', taskId={sp.taskId}, sceneName='{sp.sceneName}', pos={sp.transform.position}, active={sp.gameObject.activeSelf}");
            }

            var scenePoint = _sceneSpawnPoints
                .FirstOrDefault(s => s != null
                                     && s.taskId == taskId
                                     && (string.IsNullOrEmpty(s.sceneName) || s.sceneName == sceneName)
                                     && s.gameObject.activeInHierarchy);
            if (scenePoint != null)
            {
                Debug.Log($"[TaskManager] Using SceneSpawnPoint component for task {taskId} -> '{scenePoint.gameObject.name}' at {scenePoint.transform.position}");
                return scenePoint.transform;
            }
        }
        else
        {
            Debug.Log("[TaskManager] _sceneSpawnPoints empty");
        }

        // 4) fallback: объекты с тегом SpawnPoint (берём первый и логируем все)
        var spObjs = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spObjs != null && spObjs.Length > 0)
        {
            Debug.Log($"[TaskManager] Found {spObjs.Length} GameObject(s) with tag 'SpawnPoint':");
            for (int i = 0; i < spObjs.Length; i++)
                Debug.Log($"  [{i}] name='{spObjs[i].name}', pos={spObjs[i].transform.position}, activeSelf={spObjs[i].activeSelf}");
            Debug.Log($"[TaskManager] Using scene SpawnPoint tag (first) at {spObjs[0].transform.position}");
            return spObjs[0].transform;
        }
        else
        {
            Debug.Log("[TaskManager] No GameObject with tag 'SpawnPoint' found in scene");
        }

        // 5) last resort — defaultSpawnPoint field
        if (defaultSpawnPoint != null)
        {
            Debug.Log($"[TaskManager] Using defaultSpawnPoint field at {defaultSpawnPoint.position}");
            return defaultSpawnPoint;
        }

        Debug.Log($"[TaskManager] No spawn found for task {taskId}, scene {sceneName}");
        return null;
    }


    // helper to avoid possible null exceptions when printing _sceneSpawnPoints
    private IEnumerable<SceneSpawnPoint> _scene_spawn_points_safe()
    {
        return _sceneSpawnPoints ?? Enumerable.Empty<SceneSpawnPoint>();
    }

    // Позволяет PlayerController применить отложенный телепорт (если он существует)
    public void TryApplyDeferredSpawnToPlayer(PlayerController player)
    {
        if (!_hasDeferredSpawn || player == null) return;
        Debug.Log($"[TaskManager] Applying deferred spawn to player at {_deferredSpawnPosition}");
        player.TeleportTo(_deferredSpawnPosition);
        _hasDeferredSpawn = false;
    }

    public Transform GetSpawnForCurrentTask()
    {
        var t = GetCurrentTaskData();
        if (t == null) return null;
        return GetSpawnForTask(t.id, SceneManager.GetActiveScene().name);
    }

    public TaskData GetCurrentTaskData() => tasks.Length > 0 ? tasks[currentIndex] : null;

    /// <summary>
    /// Возвращает модельный вариант для задачи:
    /// 0 = default, 1 = variant1, 2 = variant2.
    /// variant2 имеет приоритет перед variant1.
    /// </summary>
    public int GetModelVariantForTask(TaskData task)
    {
        if (task == null) return 0;
        if (variant2TaskIds != null && variant2TaskIds.Contains(task.id)) return 2;
        if (variant1TaskIds != null && variant1TaskIds.Contains(task.id)) return 1;
        return 0;
    }

    // старый метод оставим для совместимости (если где-то зовётся)
    public bool ShouldUseAltModel(TaskData task)
    {
        return GetModelVariantForTask(task) > 0;
    }
    public void ApplyModelVariantForTask(TaskData task)
    {
        int variant = GetModelVariantForTask(task);
        if (PlayerController.instance != null)
        {
            PlayerController.instance.SetModelVariant(variant);
        }
        else
        {
            Debug.Log($"[TaskManager] PlayerController.instance == null; ApplyModelVariant deferred (variant={variant}). Player will set in Start().");
        }
    }


    public void RegisterSceneSpawnPoint(SceneSpawnPoint sp)
    {
        if (sp == null) return;
        if (!_sceneSpawnPoints.Contains(sp))
        {
            _sceneSpawnPoints.Add(sp);
            Debug.Log($"[TaskManager] RegisterSceneSpawnPoint: registered '{sp.gameObject.name}' taskId={sp.taskId} sceneName='{sp.sceneName}'");
        }
    }

    public void UnregisterSceneSpawnPoint(SceneSpawnPoint sp)
    {
        if (sp == null) return;
        if (_sceneSpawnPoints.Contains(sp))
        {
            _sceneSpawnPoints.Remove(sp);
            Debug.Log($"[TaskManager] UnregisterSceneSpawnPoint: unregistered '{sp.gameObject.name}'");
        }
    }

    // helper: очистка списка при смене сцены (чтобы не держать старые точки)
    private void ClearSceneSpawnPoints()
    {
        if (_sceneSpawnPoints != null && _sceneSpawnPoints.Count > 0)
            Debug.Log($"[TaskManager] ClearSceneSpawnPoints: clearing {_sceneSpawnPoints.Count} entries");
        _sceneSpawnPoints.Clear();
    }

    /// <summary>
    /// Установить подавление катсцены для текущей задачи (один раз).
    /// Вызывается внешним кодом сразу после SetCurrentTaskIndex(..., isLoading:true)
    /// перед снятием IsContinueMode.
    /// </summary>
    public void SuppressCutsceneForCurrentTaskOnce()
    {
        var t = GetCurrentTaskData();
        if (t != null)
        {
            _suppressCutsceneForTaskId = t.id;
            Debug.Log($"[TaskManager] SuppressCutsceneForCurrentTaskOnce: will suppress cutscene for task {_suppressCutsceneForTaskId}");
        }
    }


    // Замена метода _scene_spawn_points_safe() — сделаем понятное имя:
    private IEnumerable<SceneSpawnPoint> GetSceneSpawnPointsSafe()
    {
        return _sceneSpawnPoints ?? Enumerable.Empty<SceneSpawnPoint>();
    }

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

[Serializable]
public class SpawnMapping
{
    public int taskId;            // задача, для которой использовать этот spawn
    public string sceneName;      // опционально: ограничиваем конкретной сценой
    public string spawnObjectName; // имя GameObject в сцене (например "Spawn_Task13")
}
