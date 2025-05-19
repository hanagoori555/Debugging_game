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
        => isPlayerNearby = c.CompareTag("Player");

    void OnTriggerExit2D(Collider2D c)
        => isPlayerNearby = false;

    void Update()
    {
        if (!isPlayerNearby || !Input.GetKeyDown(KeyCode.E))
            return;

        // гарантируем, что это нужный объект
        if (string.IsNullOrEmpty(objectId))
            return;

        OnAnyInteract?.Invoke(objectId);
    }
}
