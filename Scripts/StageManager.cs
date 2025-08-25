using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("Фон и список спрайтов")]
    public SpriteRenderer backgroundRenderer;
    public List<Sprite> backgrounds;

    [Header("Точка, куда возвращать игрока после смены фона")]
    public Transform leftSpawnPoint;

    [Header("Список id задач TaskManager, при которых переход разрешён (например 25, 28, 30)")]
    public List<int> allowedTaskIds = new List<int>();

    [Header("(Опционально) требуемый интеракт id — например 'Crystal'")]
    public string requiredInteractionId = "";

    [Header("Сколько раз нужно поговорить с интерактивом (если требуется)")]
    public int requiredInteractionCount = 1;

    public static event Action OnBackgroundTransition;

    private int currentIndex = 0;

    // чтобы не выполнять переход дважды для одной и той же задачи
    private int _lastHandledTaskId = -1;

    // если игрок вошёл в триггер раньше чем выполнил интеракт — отложенный переход
    private bool _pendingTrigger = false;
    private Collider2D _pendingPlayerCollider = null;

    void Start()
    {
        if (backgrounds != null && backgrounds.Count > 0 && backgroundRenderer != null)
            backgroundRenderer.sprite = backgrounds[0];
    }

    void OnEnable()
    {
        // подписываемся на событие завершения интеракта (если DialogueManager предоставляет)
        DialogueManager.OnInteractionCompleted -= OnInteractionCompleted;
        DialogueManager.OnInteractionCompleted += OnInteractionCompleted;
    }

    void OnDisable()
    {
        DialogueManager.OnInteractionCompleted -= OnInteractionCompleted;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var currentTask = TaskManager.instance?.GetCurrentTaskData();
        int currentTaskId = currentTask != null ? currentTask.id : -1;

        Debug.Log($"[StageManager] TriggerEnter. currentTaskId={currentTaskId}, allowed={string.Join(",", allowedTaskIds)}");

        // если текущая задача не разрешает этот переход — блокируем
        if (allowedTaskIds != null && allowedTaskIds.Count > 0)
        {
            if (!allowedTaskIds.Contains(currentTaskId))
            {
                Debug.Log($"[StageManager] Blocked: current task {currentTaskId} not in allowedTaskIds.");
                return;
            }
        }

        // защита: если этот переход уже выполнён для этой же задачи — игнорируем
        if (_lastHandledTaskId == currentTaskId)
        {
            Debug.Log($"[StageManager] Ignoring: transition for task {currentTaskId} was already handled.");
            return;
        }

        // если требуется интеракт — проверяем счетчик в DialogueManager
        if (!string.IsNullOrEmpty(requiredInteractionId))
        {
            if (DialogueManager.HasCompletedInteraction(requiredInteractionId, requiredInteractionCount))
            {
                PerformBackgroundTransition(other, currentTaskId);
                return;
            }
            else
            {
                Debug.Log("[StageManager] Required interaction not completed -> blocking transition and waiting for interaction.");
                _pendingTrigger = true;
                _pendingPlayerCollider = other;
                return;
            }
        }

        // иначе — делаем переход
        PerformBackgroundTransition(other, currentTaskId);
    }

    private void OnInteractionCompleted(string id, int totalCount)
    {
        if (!_pendingTrigger) return;
        if (id != requiredInteractionId) return;

        Debug.Log($"[StageManager] OnInteractionCompleted: id={id} total={totalCount} needed={requiredInteractionCount}");
        if (totalCount >= requiredInteractionCount)
        {
            // выполним отложенный переход
            if (_pendingPlayerCollider != null)
            {
                var currentTask = TaskManager.instance?.GetCurrentTaskData();
                int currentTaskId = currentTask != null ? currentTask.id : -1;
                PerformBackgroundTransition(_pendingPlayerCollider, currentTaskId);
            }

            _pendingTrigger = false;
            _pendingPlayerCollider = null;
        }
    }

    private void PerformBackgroundTransition(Collider2D playerCollider, int currentTaskId)
    {
        if (backgrounds == null || backgrounds.Count == 0)
        {
            Debug.LogWarning("[StageManager] No backgrounds configured.");
            return;
        }

        currentIndex = (currentIndex + 1) % backgrounds.Count;
        if (backgroundRenderer != null)
            backgroundRenderer.sprite = backgrounds[currentIndex];

        if (leftSpawnPoint != null)
            playerCollider.transform.root.position = leftSpawnPoint.position;

        PlayerController.InputBlocked = true;

        // помечаем, что для этой задачи переход уже выполнен (чтобы не повторять)
        _lastHandledTaskId = currentTaskId;

        Debug.Log($"[StageManager] Performed background transition for task {currentTaskId}");

        OnBackgroundTransition?.Invoke();
    }
}
