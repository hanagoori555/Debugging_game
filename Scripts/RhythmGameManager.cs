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

    [Header("AudioSource for playback (assign in inspector)")]
    public AudioSource audioSource;

    [Header("SFX for hits (assign in inspector)")]
    public AudioClip hitSfx;
    [Range(0f, 1f)]
    public float hitSfxVolume = 1f;

    // internal sfx source (separate from music audioSource)
    private AudioSource _sfxSource;

    [Header("Note prefab & spawn points (assign all spawn transforms in inspector)")]
    public GameObject notePrefab;
    public Transform[] spawnPoints; // must be assigned: length >= max lane index + 1

    [Header("Lane key bindings")]
    public KeyCode[] laneKeys = new KeyCode[8] {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R,
        KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P
    };

    [Header("UI Counters (Canvas) - assign in inspector")]
    public TextMeshProUGUI hitCounterText;
    public TextMeshProUGUI missCounterText;

    [Header("Battle backgrounds (assign in inspector)")]
    public SpriteRenderer backgroundRenderer;
    public Sprite[] battleBackgrounds;

    [Header("Battle background alpha (0 = transparent, 1 = opaque)")]
    [Range(0f, 1f)]
    public float battleBackgroundAlpha = 0.5f;

    // internals
    private Dictionary<int, ChartData> _charts;
    private ChartData _activeChart;
    private float _songTime;
    private int _nextNoteIndex;
    private bool _isEnding;

    private int _hits;
    private int _misses;

    public event Action OnRhythmFinished;

    // cache to avoid spawning same note multiple times (key=time_lane)
    private HashSet<string> _spawnedNoteKeys = new HashSet<string>();

    void Awake()
    {
        // simple singleton, but NO DontDestroyOnLoad: keep manager scene-local
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Debug.Log("[RGM] Duplicate RhythmGameManager destroyed.");
            Destroy(gameObject);
            return;
        }

        LoadAllCharts();

        // ensure sfx audio source exists (separate from music)
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
        }
    }

    void Start()
    {
        UpdateCountersUI();
        if (backgroundRenderer != null)
            backgroundRenderer.gameObject.SetActive(false);
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

    /// <summary>
    /// Запускает ритм-режим для заданного номера боя.
    /// Требование: все нужные ссылки (audioSource, spawnPoints, prefab, UI, backgroundRenderer) должны быть назначены в инспекторе.
    /// </summary>
    public void EnterRhythmMode(int battleNumber)
    {
        if (_activeChart != null)
        {
            Debug.LogWarning("[RGM] EnterRhythmMode called but a chart is already active — ignoring.");
            return;
        }

        if (!_charts.TryGetValue(battleNumber, out var chart))
        {
            Debug.LogError($"[RGM] Chart #{battleNumber} not loaded!");
            return;
        }

        // Очистка кеша дубликатов на старте боя (важно!)
        _spawnedNoteKeys.Clear();

        _activeChart = chart;
        _nextNoteIndex = 0;
        _songTime = 0f;
        _isEnding = false;
        _hits = _misses = 0;
        UpdateCountersUI();

        ApplyBattleBackground(battleNumber);

        if (audioSource == null)
        {
            Debug.LogError("[RGM] audioSource not assigned in inspector!");
        }
        else
        {
            var clip = Resources.Load<AudioClip>(chart.musicPath);
            if (clip == null)
                Debug.LogError($"[RGM] AudioClip not found at '{chart.musicPath}'");
            else
            {
                audioSource.Stop();
                audioSource.clip = clip;
                audioSource.time = 0f;
                audioSource.Play();
            }
        }
    }

    public void ExitRhythmMode()
    {
        // остановим звук и очистим состояние
        if (audioSource != null) audioSource.Stop();

        _activeChart = null;

        if (backgroundRenderer != null)
            backgroundRenderer.gameObject.SetActive(false);

        OnRhythmFinished?.Invoke();

        // очистка кеша — безопасно
        _spawnedNoteKeys.Clear();
    }

    void Update()
    {
        if (_activeChart == null || _isEnding) return;

        _songTime = (audioSource != null && audioSource.isPlaying) ? audioSource.time : _songTime + Time.deltaTime;

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
        // ждём, пока музыка действительно закончится
        yield return new WaitWhile(() => audioSource != null && audioSource.isPlaying);
        yield return new WaitForSeconds(0.5f);
        ExitRhythmMode();
    }

    private void SpawnNote(NoteData data)
    {
        // key по времени и лейну (формат фиксированный), предотвращает множественные инстансы одной ноты
        var key = $"{data.time:F3}_{data.lane}";
        if (_spawnedNoteKeys.Contains(key))
        {
            Debug.LogWarning($"[RGM] Duplicate spawn suppressed for key={key}");
            return;
        }
        _spawnedNoteKeys.Add(key);

        if (spawnPoints == null || data.lane < 0 || data.lane >= spawnPoints.Length)
        {
            Debug.LogError($"[RGM] Cannot spawn note: invalid spawn point for lane {data.lane}. Check spawnPoints assignment in inspector.");
            return;
        }

        if (spawnPoints[data.lane] == null)
        {
            Debug.LogError($"[RGM] spawnPoints[{data.lane}] is null. Assign all spawn transforms in inspector.");
            return;
        }

        if (notePrefab == null)
        {
            Debug.LogError("[RGM] notePrefab not assigned in inspector!");
            return;
        }

        var go = Instantiate(notePrefab, spawnPoints[data.lane].position, Quaternion.identity);
        go.name = $"Note_lane{data.lane}_time{data.time:F3}";
        var note = go.GetComponent<RhythmNote>();
        if (note == null)
        {
            Debug.LogError("[RGM] Instantiated prefab does not have RhythmNote component!");
            Destroy(go);
            return;
        }
        note.Initialize(data.lane, data.duration, laneKeys[Mathf.Clamp(data.lane, 0, laneKeys.Length - 1)]);
    }

    public void RegisterHit()
    {
        _hits++;
        UpdateCountersUI();

        // воспроизводим отклик SFX — сыграет для коротких нот сразу,
        // для длинных — в момент, когда нота вызывает RegisterHit (в конце удержания)
        if (hitSfx != null)
        {
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
            }
            _sfxSource.PlayOneShot(hitSfx, hitSfxVolume);
        }
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

    private void ApplyBattleBackground(int battleNumber)
    {
        if (backgroundRenderer == null)
        {
            Debug.Log("[RGM] backgroundRenderer is null — skipping background apply");
            return;
        }

        int idx = battleNumber - 1;
        float a = Mathf.Clamp01(battleBackgroundAlpha);

        if (idx >= 0 && idx < battleBackgrounds.Length && battleBackgrounds[idx] != null)
        {
            backgroundRenderer.sprite = battleBackgrounds[idx];
            Color col = backgroundRenderer.color;
            col.r = 1f; col.g = 1f; col.b = 1f; col.a = a;
            backgroundRenderer.color = col;
            backgroundRenderer.gameObject.SetActive(true);
            Debug.Log($"[RGM] Applied background sprite for battle #{battleNumber}");
        }
        else
        {
            backgroundRenderer.sprite = null;
            backgroundRenderer.color = new Color(0f, 0f, 0f, a);
            backgroundRenderer.gameObject.SetActive(true);
            Debug.Log($"[RGM] No sprite for battle #{battleNumber} — using black background");
        }
    }
}
