using System;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    // Статическое событие, которое выдаёт все интерактивные объекты
    public static event Action<string> OnAnyInteract;

    [Header("ID объекта (в JSON)")]
    public string objectId;

    private bool isPlayerNearby;

    void OnTriggerEnter2D(Collider2D c)
    {
        Debug.Log($"[Interactable] OnTriggerEnter id={objectId}, this.enabled={enabled}, objActive={gameObject.activeSelf}, collider.name={c.gameObject.name}, collider.tag={c.gameObject.tag}");
        // Только помечаем как рядом, если вошёл именно игрок
        isPlayerNearby = c.CompareTag("Player");
        Debug.Log($"[Interactable] -> isPlayerNearby set to {isPlayerNearby} for id={objectId}");
    }

    void OnTriggerExit2D(Collider2D c)
    {
        Debug.Log($"[Interactable] OnTriggerExit id={objectId}, collider.name={c.gameObject.name}, collider.tag={c.gameObject.tag}");
        isPlayerNearby = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[Interactable] Key E pressed while isPlayerNearby={isPlayerNearby} for id={objectId}");
        }

        if (!isPlayerNearby || !Input.GetKeyDown(KeyCode.E))
            return;

        if (string.IsNullOrEmpty(objectId))
        {
            Debug.LogWarning($"[Interactable] Interaction attempted but objectId empty on {gameObject.name}");
            return;
        }

        // Доп.информация: какой сейчас таск (может помочь в диагностике)
        var curTask = TaskManager.instance?.GetCurrentTaskData();
        if (curTask != null)
            Debug.Log($"[Interactable] CurrentTask id={curTask.id} triggerType={curTask.triggerType} triggerParam={curTask.triggerParam}");

        // Правильный способ узнать число подписчиков (внутри класса, где объявлено событие)
        var handlers = OnAnyInteract;
        int listeners = handlers != null ? handlers.GetInvocationList().Length : 0;
        Debug.Log($"[Interactable] Invoking OnAnyInteract for id={objectId}, listeners={listeners}");

        // Вызов события
        OnAnyInteract?.Invoke(objectId);
    }

    // Утилита: вернуть число подписчиков (нужно для внешнего логирования/диагностики)
    public static int GetOnAnyInteractSubscriberCount()
    {
        var h = OnAnyInteract;
        return h != null ? h.GetInvocationList().Length : 0;
    }
}
