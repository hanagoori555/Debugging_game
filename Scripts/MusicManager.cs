using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Аудиоклипы для сцен")]
    public AudioClip defaultClip;
    public SceneMusicEntry[] entries;

    private AudioSource src;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void StopMusic()
    {
        if (src.isPlaying)
        {
            Debug.Log("[MusicManager] Stopping background music");
            src.Stop();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ищем, есть ли под эту сцену отдельный клип
        var entry = System.Array.Find(entries, e => e.sceneName == scene.name);

        AudioClip newClip = null;
        if (entry != null && entry.clip != null)
        {
            newClip = entry.clip;
            Debug.Log($"[MusicManager] Scene '{scene.name}' has assigned clip -> switching to it.");
        }
        else if (defaultClip != null)
        {
            newClip = defaultClip;
            Debug.Log($"[MusicManager] Scene '{scene.name}' has no assigned clip -> using default clip.");
        }
        else
        {
            // НИ ОДНОЙ МУЗЫКИ — хотим тишину: остановить текущую и очистить clip
            if (src.isPlaying)
            {
                Debug.Log($"[MusicManager] Scene '{scene.name}' has no assigned clip and defaultClip is null -> stopping music.");
                src.Stop();
            }
            src.clip = null;
            return;
        }

        // Если новый клип тот же, что и сейчас и уже играет — ничего не делаем
        if (src.clip == newClip && src.isPlaying)
            return;

        // Применяем и запускаем
        src.clip = newClip;
        src.Play();
    }
}

[System.Serializable]
public class SceneMusicEntry
{
    public string sceneName;
    public AudioClip clip;
}
