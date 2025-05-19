using System;
using UnityEngine;

public class SceneExitDetector : MonoBehaviour
{
    public static event Action<string> OnSceneExit;
    public string targetScene;
    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        var tm = TaskManager.instance;
        if (tm == null || tm.GetCurrentTaskData() == null)
        {
            col.enabled = false;
            return;
        }

        var task = tm.GetCurrentTaskData();
        // включаем коллайдер, только когда ожидаем именно этот выход
        col.enabled = (task.triggerType == "SceneExit" && task.triggerParam == targetScene);
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag("Player"))
            OnSceneExit?.Invoke(targetScene);
    }
}
