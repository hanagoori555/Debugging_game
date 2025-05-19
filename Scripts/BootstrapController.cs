using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapController : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Сразу идём в начальную сцену:
        SceneManager.LoadScene("MainMenu");
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Если это учебная сцена, создаём игрока
        if (scene.name == "School" || scene.name == "Forest")
        {
            // Спавним игрока как префаб
            var startPos = GameSaveManager.instance.HasCheckpoint()
                ? GameSaveManager.instance.LoadCheckpointPosition()
                : Vector2.zero;
            Instantiate(Resources.Load<GameObject>("Prefabs/Player"),
                        startPos, Quaternion.identity);

            // Меняем каталоги диалогов под новую сцену
            DialogueCatalog.instance.ReloadForActiveScene();
            // Обновляем задачу и, если надо, показываем cutscene
            TaskManager.instance.SubscribeToTask(TaskManager.instance.GetCurrentTaskData());
        }
    }
}
