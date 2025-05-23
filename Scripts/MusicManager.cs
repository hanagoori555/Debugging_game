using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Аудиоклипы для сцен")]
    public AudioClip defaultClip;       // на случай, если для сцены не назначено
    public SceneMusicEntry[] entries;   // карта: имя сцены → нужный клип

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ищем, есть ли под эту сцену отдельный клип
        var entry = System.Array.Find(entries, e => e.sceneName == scene.name);
        var clip = entry != null && entry.clip != null
                   ? entry.clip
                   : defaultClip;

        if (clip == null) return;

        if (src.clip == clip && src.isPlaying) return;

        src.clip = clip;
        src.Play();
    }
}

[System.Serializable]
public class SceneMusicEntry
{
    public string sceneName;
    public AudioClip clip;
}
