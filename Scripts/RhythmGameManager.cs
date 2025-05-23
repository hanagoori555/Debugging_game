// RhythmGameManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RhythmGameManager : MonoBehaviour
{
    public static RhythmGameManager instance;

    [Header("JSON Chart files (Resources/Charts)")]
    public string[] chartJsonNames = { "Chart1", "Chart2" };

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

    [Header("UI Counters (Canvas)")]
    public TextMeshProUGUI hitCounterText;
    public TextMeshProUGUI missCounterText;

    private Dictionary<int, ChartData> _charts;
    private ChartData _activeChart;
    private float _songTime;
    private int _nextNoteIndex;
    private bool _isEnding;

    private int _hits;
    private int _misses;

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
        LoadAllCharts();
    }

    void Start()
    {
        UpdateCountersUI();
        EnterRhythmMode(1);
    }

    private void LoadAllCharts()
    {
        _charts = new Dictionary<int, ChartData>();
        foreach (var name in chartJsonNames)
        {
            var ta = Resources.Load<TextAsset>($"Charts/{name}");
            if (ta == null)
            {
                Debug.LogError($"[RGM] JSON not found: Charts/{name}.json");
                continue;
            }
            try
            {
                var chart = JsonUtility.FromJson<ChartData>(ta.text);
                _charts[chart.battleNumber] = chart;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RGM] Error parsing {name}.json: {e.Message}");
            }
        }
    }

    public void EnterRhythmMode(int battleNumber)
    {
        if (!_charts.TryGetValue(battleNumber, out var chart))
        {
            Debug.LogError($"[RGM] Chart #{battleNumber} not loaded!");
            return;
        }

        _activeChart = chart;
        _nextNoteIndex = 0;
        _songTime = 0f;
        _isEnding = false;
        _hits = _misses = 0;
        UpdateCountersUI();

        var clip = Resources.Load<AudioClip>(chart.musicPath);
        if (clip == null)
            Debug.LogError($"[RGM] AudioClip not found at '{chart.musicPath}'");
        else
        {
            audioSource.clip = clip;
            audioSource.time = 0f;
            audioSource.Play();
        }
    }

    public void ExitRhythmMode()
    {
        audioSource.Stop();
        _activeChart = null;
    }

    void Update()
    {
        if (_activeChart == null || _isEnding) return;

        _songTime = audioSource.isPlaying ? audioSource.time : _songTime + Time.deltaTime;

        while (_nextNoteIndex < _activeChart.notes.Length &&
               _songTime >= _activeChart.notes[_nextNoteIndex].time)
        {
            SpawnNote(_activeChart.notes[_nextNoteIndex]);
            _nextNoteIndex++;
        }

        if (_nextNoteIndex >= _activeChart.notes.Length && !_isEnding)
        {
            _isEnding = true;
            StartCoroutine(WaitAndExit());
        }
    }

    private IEnumerator WaitAndExit()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        yield return new WaitForSeconds(0.5f);
        ExitRhythmMode();
    }

    private void SpawnNote(NoteData data)
    {
        if (data.lane < 0 || data.lane >= spawnPoints.Length) return;

        var go = Instantiate(notePrefab, spawnPoints[data.lane].position, Quaternion.identity);
        var note = go.GetComponent<RhythmNote>();
        note.Initialize(data.lane, data.duration, laneKeys[data.lane]);
    }

    public void RegisterHit()
    {
        _hits++;
        UpdateCountersUI();
    }

    public void RegisterMiss()
    {
        _misses++;
        UpdateCountersUI();
    }

    private void UpdateCountersUI()
    {
        if (hitCounterText != null)
            hitCounterText.text = $"Hits: {_hits}";
        if (missCounterText != null)
            missCounterText.text = $"Misses: {_misses}";
    }
}
