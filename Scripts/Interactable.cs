using UnityEngine;

public class Interactable : MonoBehaviour
{
    [Header("ID объекта (в JSON)")]
    public string objectId;

    private bool isPlayerNearby;
    private bool hasTriggered = false;

    void OnTriggerEnter2D(Collider2D c)
        => isPlayerNearby = c.CompareTag("Player");

    void OnTriggerExit2D(Collider2D c)
        => isPlayerNearby = false;

    void Update()
    {
        if (!isPlayerNearby || !Input.GetKeyDown(KeyCode.E))
            return;

        var lines = DialogueCatalog.instance.GetInteractableLines(objectId);
        if (lines == null || lines.Length == 0)
            return;

        // Показываем всегда
        DialogueManager.instance.ShowDialogue(lines);

        // Меняем задачу лишь один раз
        if (!hasTriggered)
        {
            TaskManager.instance.NextTask();
            DialogueCatalog.instance.RefreshState();
            hasTriggered = true;
        }
    }
}
