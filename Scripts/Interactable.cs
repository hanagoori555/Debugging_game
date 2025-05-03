using UnityEngine;

public class Interactable : MonoBehaviour
{
    [Header("ID объекта для поиска диалога в JSON")]
    [Tooltip("Должен совпадать с полем objectId в вашем JSON для этой сцены")]
    public string objectId;

    private bool isPlayerNearby = false;

    private void Start()
    {
        // Проверяем, что DialogueCatalog инициализирован
        if (DialogueCatalog.instance == null)
        {
            Debug.LogError($"[Interactable] DialogueCatalog.instance is null. Убедитесь, что на сцене есть DialogueCatalog.");
            enabled = false;
            return;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerNearby = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerNearby = false;
    }

    private void Update()
    {
        // Ждём, пока игрок рядом и нажмёт E
        if (!isPlayerNearby || !Input.GetKeyDown(KeyCode.E))
            return;

        // Запрашиваем диалоговые строки у DialogueCatalog по objectId
        var lines = DialogueCatalog.instance.GetInteractableLines(objectId);

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning($"[Interactable] Для objectId='{objectId}' диалог не найден или пуст.");
            return;
        }

        // Показываем диалог
        DialogueManager.instance.ShowDialogue(lines);

        // По окончании диалога можно переключить задачу, если нужно.
        // Но в этой структуре мы не храним флаг advanceTask в JSON,
        // поэтому вызываем NextTask() вручную где нужно.
        // Например:
        TaskManager.instance.NextTask();
    }
}
