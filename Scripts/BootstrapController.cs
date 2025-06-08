using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;  // для GraphicRaycaster

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
        "Home"
    };

    [Header("Путь к префабу игрока в Resources")]
    [SerializeField] private string playerPrefabPath = "Prefabs/Player";

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // сразу грузим меню
        SceneManager.LoadScene("MainMenu");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isGame = gameplayScenes.Contains(scene.name);

        // 1) Включаем/выключаем весь Canvas
        if (gameCanvas != null)
        {
            gameCanvas.enabled = isGame;
            var ray = gameCanvas.GetComponent<GraphicRaycaster>();
            if (ray != null) ray.enabled = isGame;
        }

        if (!isGame)
            return;

        // 2) Спавним игрока
        Vector2 startPos = GameSaveManager.instance.HasCheckpoint()
            ? GameSaveManager.instance.LoadCheckpointPosition()
            : Vector2.zero;

        var prefab = Resources.Load<GameObject>(playerPrefabPath);
        if (prefab != null)
            Instantiate(prefab, startPos, Quaternion.identity);
        else
            Debug.LogError($"[Bootstrap] Player prefab not found at Resources/{playerPrefabPath}");

        // 3) Перезагружаем диалоги и подписываем TaskManager
        DialogueCatalog.instance.ReloadForActiveScene();
        TaskManager.instance.SubscribeToTask(TaskManager.instance.GetCurrentTaskData());
    }
}
