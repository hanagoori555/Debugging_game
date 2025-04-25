using UnityEngine;

public class RhythmGameManager : MonoBehaviour
{
    public static RhythmGameManager instance;  // Статическая ссылка для доступа из других скриптов

    public GameObject notePrefab;
    public Transform[] spawnPoints;
    public KeyCode[] laneKeys = new KeyCode[8]
    {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R,
        KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P
    };
    public float spawnInterval = 1.5f;
    private float spawnTimer;
    public NoteChart noteChart;
    private int nextNoteIndex = 0;
    public AudioSource audioSource;
    private float songTime;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // Не обязательно, если не нужно сохранять между сценами:
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (audioSource != null)
            audioSource.Play();

        nextNoteIndex = 0;
        songTime = 0f;
    }

    void Update()
    {
        if (audioSource != null)
            songTime = audioSource.time;
        else
            songTime += Time.deltaTime;

        // Если есть карта нот и ещё есть ноты для спавна:
        if (noteChart != null && nextNoteIndex < noteChart.notes.Length)
        {
            float noteTime = noteChart.notes[nextNoteIndex].time;
            // Если время трека достигло или превысило время появления ноты, спавним её:
            if (songTime >= noteTime)
            {
                SpawnNote(noteChart.notes[nextNoteIndex]);
                nextNoteIndex++;
            }
        }
    }

    void SpawnNote(NoteData data)
    {
        int laneIndex = data.lane;
        if (laneIndex < 0 || laneIndex >= spawnPoints.Length)
        {
            Debug.LogWarning($"Неверный laneIndex = {laneIndex}");
            return;
        }

        Transform spawnPos = spawnPoints[laneIndex];
        GameObject noteObj = Instantiate(notePrefab, spawnPos.position, Quaternion.identity);
        RhythmNote note = noteObj.GetComponent<RhythmNote>();
        if (note != null)
        {
            note.laneIndex = laneIndex;
            note.duration = data.duration;
            note.fallSpeed = 2f; // Установка скорости падения
        }

        Debug.Log($"Нота: time={data.time}, lane={data.lane}, duration={data.duration} (spawned at {songTime:F2})");
    }
}
