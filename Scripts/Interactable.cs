using System;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    // 1) Статическое событие, которое выдают все интерактивные объекты
    public static event Action<string> OnAnyInteract;

    [Header("ID объекта (в JSON)")]
    public string objectId;

    private bool isPlayerNearby;

    void OnTriggerEnter2D(Collider2D c)
    {
        Debug.Log($"[Interactable] OnTriggerEnter id={objectId}, this.enabled={enabled}, objActive={gameObject.activeSelf}, collider.name={c.gameObject.name}, collider.tag={c.gameObject.tag}");
        // только помечаем как рядом, если вошёл именно Player
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

        Debug.Log($"[Interactable] Invoking OnAnyInteract for id={objectId}");
        OnAnyInteract?.Invoke(objectId);
    }
}