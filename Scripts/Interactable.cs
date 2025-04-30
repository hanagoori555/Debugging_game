using UnityEngine;

[System.Serializable]
public class DialoguePhase
{
    [Tooltip("Номер катсцены, после которой этот блок диалога доступен (0 — всегда)")]
    public int requiredCutscene1;

    [Tooltip("Набор строк диалога для этой фазы")]
    public DialogueLine[] dialogueLines;

    [Tooltip("Сменить задачу после завершения этого диалога")]
    public bool advanceTaskOnComplete = false;
}

public class Interactable : MonoBehaviour
{
    [Header("Фазы диалога объекта")]
    public DialoguePhase[] phases;

    private bool isPlayerNearby = false;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerNearby = true;
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            isPlayerNearby = false;
    }

    void Update()
    {
        if (!isPlayerNearby || !Input.GetKeyDown(KeyCode.E))
            return;

        // Проверяем, завершена ли первая катсцена
        bool cut1 = GameSaveManager.instance != null && GameSaveManager.instance.IsCutscene1Completed();

        // Выбираем самую подходящую фазу
        DialoguePhase chosen = null;
        foreach (var ph in phases)
        {
            if (ph.requiredCutscene1 <= (cut1 ? 1 : 0))
                chosen = ph;
            else
                break;
        }

        if (chosen != null && chosen.dialogueLines != null && chosen.dialogueLines.Length > 0)
        {
            DialogueManager.instance.ShowDialogue(chosen.dialogueLines);

            // Смена задачи, если требовалось
            if (chosen.advanceTaskOnComplete)
                TaskManager.instance?.NextTask();
        }
        else
        {
            Debug.LogWarning($"[Interactable] У объекта {name} нет активных фаз диалога");
        }
    }
}