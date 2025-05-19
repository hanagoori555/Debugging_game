using System;
using System.Collections.Generic;
using UnityEngine;

public class RhythmGameManager : MonoBehaviour
{
    public static RhythmGameManager instance;

    [Header("JSON Chart files (Resources/Charts)")]
    public string[] chartJsonNames = { "Сhart1", "Сhart2" };

    [Header("Global UI to disable")]
    public GameObject bootstrapUIRoot;

    [Header("AudioSource for playback")]
    public AudioSource audioSource;

    [Header("Note prefab & spawn points")]
    public GameObject notePrefab;
    public Transform[] spawnPoints;

    [Header("Lane key bindings")]
    public KeyCode[] laneKeys = new KeyCode[8] {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R,
        KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P
    };

    // Внутренние данные
    private Dictionary<int, ChartData> _charts;
    private ChartData _activeChart;
    private float _songTime;
    private int _nextNoteIndex;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        LoadAllCharts();
    }

    void Start()
    {
        // Сразу входим в первый ритм-бой
        EnterRhythmMode(1);
    }

    // Загрузка всех JSON-чанrтов в словарь
    private void LoadAllCharts()
    {
        _charts = new Dictionary<int, ChartData>();
        foreach (var name in chartJsonNames)
        {
            Debug.Log($"Looking for Resources/Charts/{name} in project");
            var ta = Resources.Load<TextAsset>($"Charts/{name}");
            if (ta == null)
            {
                Debug.LogError($"[RhythmGameManager] JSON not found: Charts/{name}.json");
                continue;
            }
            try
            {
                var chart = JsonUtility.FromJson<ChartData>(ta.text);
                _charts[chart.battleNumber] = chart;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RhythmGameManager] Error parsing {name}.json: {e.Message}");
            }
        }
        Debug.Log($"[RhythmGameManager] Loaded {_charts.Count} charts from JSON.");
    }

    /// <summary>
    /// Войти в режим ритм-игры: отключить UI, выбрать chart и track, запустить тайминг.
    /// </summary>
    public void EnterRhythmMode(int battleNumber)
    {
        if (!_charts.TryGetValue(battleNumber, out var chart))
        {
            Debug.LogError($"[RhythmGameManager] Chart #{battleNumber} not loaded!");
            return;
        }
        _activeChart = chart;
        _nextNoteIndex = 0;
        _songTime = 0f;

        // Скрыть UI
        if (bootstrapUIRoot != null)
            bootstrapUIRoot.SetActive(false);

        // Загрузить и проиграть трек
        var clip = Resources.Load<AudioClip>(chart.musicPath);
        if (clip == null)
            Debug.LogError($"[RhythmGameManager] AudioClip not found at '{chart.musicPath}'");
        else
        {
            audioSource.clip = clip;
            audioSource.time = 0f;
            audioSource.Play();
        }

        Debug.Log($"[RhythmGameManager] Enter RhythmMode #{battleNumber}, chart has {chart.notes.Length} notes.");
    }

    /// <summary>
    /// Выйти из режима ритм-игры: вернуть UI, остановить трек.
    /// </summary>
    public void ExitRhythmMode()
    {
        if (bootstrapUIRoot != null)
            bootstrapUIRoot.SetActive(true);
        audioSource.Stop();
        _activeChart = null;
        Debug.Log("[RhythmGameManager] Exit RhythmMode");
    }

    void Update()
    {
        if (_activeChart == null) return;

        // Обновляем текущее время песни
        _songTime = audioSource.isPlaying
            ? audioSource.time
            : _songTime + Time.deltaTime;

        // Спавним ноты, если пора
        while (_nextNoteIndex < _activeChart.notes.Length
               && _songTime >= _activeChart.notes[_nextNoteIndex].time)
        {
            SpawnNote(_activeChart.notes[_nextNoteIndex]);
            _nextNoteIndex++;
        }

        // Когда весь чарт проигран — выходим
        if (_nextNoteIndex >= _activeChart.notes.Length)
            ExitRhythmMode();
    }

    private void SpawnNote(NoteData data)
    {
        if (data.lane < 0 || data.lane >= spawnPoints.Length)
            return;

        var go = Instantiate(notePrefab, spawnPoints[data.lane].position, Quaternion.identity);
        var note = go.GetComponent<RhythmNote>();
        note.Initialize(data.lane, data.duration, laneKeys[data.lane]);
    }
}
