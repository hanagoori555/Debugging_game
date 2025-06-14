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

        var prefab = Resources.Load<GameObject>(playerPrefabPath);
        if (prefab != null)
        {
            Instantiate(prefab,
                _justLoadedFromMenu ? _loadedPlayerPos : spawnPos,
                Quaternion.identity
            );
        }
        else Debug.LogError($"[Bootstrap] Player prefab not found at Resources/{playerPrefabPath}");

        // → Перезагружаем диалоги
        DialogueCatalog.instance.ReloadForActiveScene();

        // → Подписываем TaskManager: если пришли из ContinueGame, делаем SetCurrentTaskIndex
        if (_justLoadedFromMenu)
        {
            TaskManager.instance.SetCurrentTaskIndex(_loadedTaskIndex, isLoading: true);
            _justLoadedFromMenu = false;

            // Явно запускаем авто-диалоги для текущей задачи
            StartCoroutine(TriggerAutoAfterLoad());
        }
        else
        {
            TaskManager.instance.SubscribeToTask(TaskManager.instance.GetCurrentTaskData());
        }
    }

    private IEnumerator TriggerAutoAfterLoad()
    {
        yield return new WaitForEndOfFrame();
        var currentTask = TaskManager.instance.GetCurrentTaskData();
        if (currentTask != null && currentTask.triggerType == "Auto")
        {
            TaskManager.instance.SubscribeToTask(currentTask);
        }
    }


    void OnDestroy()
    {
        MainMenuController.OnContinueGame -= HandleContinueGame;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
