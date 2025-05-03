using UnityEngine;

[System.Serializable]
public class DialoguePhase
{
    [Tooltip("ID катсцены, после которой этот блок диалога доступен. Оставьте пустым, чтобы фаза была всегда доступна.")]
    public string requiredCutsceneId = "";

    [Tooltip("Набор строк диалога для этой фазы")]
    public DialogueLine[] dialogueLines;

    [Tooltip("Переключать задачу после завершения этого диалога")]
    public bool advanceTaskOnComplete = false;
}
