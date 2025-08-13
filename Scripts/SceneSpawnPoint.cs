using UnityEngine;

public class SceneSpawnPoint : MonoBehaviour
{
    [Tooltip("ID задачи, для которой используется этот спавн")]
    public int taskId = 0;

    [Tooltip("Опционально ограничить сценой — если пусто, применяется в любой сцене")]
    public string sceneName = "";

    // Регистрируемся на Awake/Start у TaskManager (если он уже есть)
    void Awake()
    {
        Debug.Log($"[SceneSpawnPoint] Awake registering '{gameObject.name}' taskId={taskId} sceneName='{sceneName}' active={gameObject.activeSelf}");
        if (TaskManager.instance != null)
            TaskManager.instance.RegisterSceneSpawnPoint(this);
    }

    // Удобство: при выключении/уничтожении - отписаться
    void OnDestroy()
    {
        if (TaskManager.instance != null)
            TaskManager.instance.UnregisterSceneSpawnPoint(this);
    }
}
