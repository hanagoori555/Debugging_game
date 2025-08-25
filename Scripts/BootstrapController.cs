using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootstrapController : MonoBehaviour
{
    public static BootstrapController instance;

    [Header("Game Canvas (Pause, TaskText, DialogueBox, CutscenePanel, ComputerInterface и т.п.)")]
    [SerializeField] private Canvas gameCanvas;

    [Header("Сцены, где нужен игрок и HUD")]
    [SerializeField]
    private List<string> gameplayScenes = new List<string>
    {
        "School",
        "Forest",
        "Home",
        "DarkWorld",
        "Zone"
    };

    [Header("Путь к префабу игрока в Resources")]
    [SerializeField] private string playerPrefabPath = "Prefabs/Player";

    // Флаг, что мы только что пришли из ContinueGame
    private bool _justLoadedFromMenu = false;
    // Индекс задачи, восстановленный из БД
    private int _loadedTaskIndex = 0;
    // Позиция игрока из БД
    private Vector2 _loadedPlayerPos = Vector2.zero;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Подписываемся на событие, чтобы засечь ContinueGame:
            MainMenuController.OnContinueGame += HandleContinueGame;

            // Сразу грузим меню
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene("MainMenu");
        }
        else Destroy(gameObject);
    }

    private void HandleContinueGame(string sceneName, Vector2 playerPos, int taskIndex)
    {
        // Сохраняем всё до фактической загрузки сцены
        _justLoadedFromMenu = true;
        _loadedPlayerPos = playerPos;
        _loadedTaskIndex = taskIndex;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isGameScene = gameplayScenes.Contains(scene.name);

        // Скрываем визуал паузы в главном меню (на всякий случай)
        if (scene.name == "MainMenu")
            PauseGuard.SetBoth("MainMenu", true);
        else
            PauseGuard.SetBoth("MainMenu", false);

        // Canvas on/off
        if (gameCanvas != null)
        {
            gameCanvas.enabled = isGameScene;
            var ray = gameCanvas.GetComponent<GraphicRaycaster>();
            if (ray != null) ray.enabled = isGameScene;
        }

        if (!isGameScene)
            return;

        // → Спавним игрока
        Vector2 spawnPos = Vector2.zero;
        var sp = GameObject.FindWithTag("SpawnPoint");
        if (sp != null) spawnPos = sp.transform.position;

        // Если игрок уже есть в сцене — не инстантируем новый (избегаем дубликатов и потери состояния)
        var existingPlayer = FindObjectOfType<PlayerController>();
        if (existingPlayer == null)
        {
            var prefab = Resources.Load<GameObject>(playerPrefabPath);
            if (prefab != null)
            {
                Instantiate(prefab,
                    _justLoadedFromMenu ? _loadedPlayerPos : spawnPos,
                    Quaternion.identity
                );
            }
            else Debug.LogError($"[Bootstrap] Player prefab not found at Resources/{playerPrefabPath}");
        }
        else
        {
            Debug.Log("[Bootstrap] Player already exists -> not instantiating a new one.");
            // Если мы пришли из Continue и у нас есть saved pos — телепортим существующего игрока
            if (_justLoadedFromMenu)
            {
                existingPlayer.TeleportTo(_loadedPlayerPos);
                Debug.Log($"[Bootstrap] Teleported existing player to saved pos {_loadedPlayerPos}");
            }
        }


        // → Перезагружаем диалоги
        DialogueCatalog.instance.ReloadForActiveScene();

        // → Подписываем TaskManager: если пришли из ContinueGame, делаем SetCurrentTaskIndex
        if (_justLoadedFromMenu)
        {
            // 1) Установим таск в режиме загрузки — минимальная подписка внутри TaskManager
            TaskManager.instance.SetCurrentTaskIndex(_loadedTaskIndex, isLoading: true);

            // 2) Просим TaskManager единожды подавить катсцену для текущей задачи
            TaskManager.instance.SuppressCutsceneForCurrentTaskOnce();

            // 3) Теперь можно снять ContinueMode — дальше игра будет обычной.
            TaskManager.instance.ConsumeContinueMode();

            _justLoadedFromMenu = false;

            // 4) Через корутину попросим TaskManager запустить именно авто-поведение для текущей задачи
            //    (автокатсцены будут подавлены одноразово благодаря флагу выше)
            StartCoroutine(TriggerAutoAfterLoad());
        }

        else
        {
            // TaskManager.instance.SubscribeToTask(TaskManager.instance.GetCurrentTaskData());
        }
    }

    private IEnumerator TriggerAutoAfterLoad()
    {
        // ждём кадр, чтобы все объекты успели стартовать
        yield return new WaitForEndOfFrame();

        // защита: если мы в режиме Continue (isLoading) — НЕ запускать Auto-сцены
        if (TaskManager.instance != null && TaskManager.instance.IsContinueMode)
        {
            Debug.Log("[Bootstrap] TriggerAutoAfterLoad: ContinueMode active -> skipping auto triggers.");
            yield break;
        }

        // Просим TaskManager попытаться запустить авто-поведение (Auto / SceneAuto) для текущей задачи
        TaskManager.instance.TryRunAutoForCurrentTaskAfterContinue();
    }

    void OnDestroy()
    {
        MainMenuController.OnContinueGame -= HandleContinueGame;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
