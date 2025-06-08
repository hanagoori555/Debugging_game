using UnityEngine;

/// <summary>
/// Этот скрипт нужно прикрепить к префабу говорящего персонажа (2D-спрайт или 3D-модель).
/// Когда объект появляется в сцене (Instantiate), он в Start() сразу берёт автодиалоги 
/// из DialogueCatalog и запускает их через DialogueManager. 
/// По завершении автодиалога самоуничтожается.
/// </summary>
public class AutoDialogSpeaker : MonoBehaviour
{
    private void Start()
    {
        // 1. Получаем массив авто-диалогов для текущего stateId
        DialogueLine[] autoLines = DialogueCatalog.instance.GetAutoDialogueForCurrentState();

        // 2. Если диалогов нет — просто убиваем себя
        if (autoLines == null || autoLines.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        // 3. Запускаем показ диалога и передаём колбэк, чтобы удалить модель после завершения
        DialogueManager.instance.ShowDialogue(autoLines, OnDialogueFinished);
    }

    private void OnDialogueFinished()
    {
        // Когда диалог завершён, удаляем этот GameObject
        Destroy(gameObject);
    }
}
