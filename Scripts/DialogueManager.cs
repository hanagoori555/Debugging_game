using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    public GameObject dialogueBox;
    public TMPro.TextMeshProUGUI dialogueText;
    public TMPro.TextMeshProUGUI characterNameText;
    public Image characterAvatarImage;

    private DialogueLine[] dialogueLines;
    private int currentLineIndex;
    private Action onCompleteCallback;  // <-- колбэк на завершение

    private void Awake()
    {
        // singleton + dont destroy
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // инициализация UI
        dialogueBox.SetActive(false);
        dialogueText.text = "";
        characterNameText.text = "";
        characterAvatarImage.enabled = false;
    }

    /// <summary>
    /// Запускает диалог и вызывает onComplete после EndDialogue()
    /// </summary>
    public void ShowDialogue(DialogueLine[] lines, Action onComplete = null)
    {
        Debug.Log($"[DialogueManager] ShowDialogue called with {lines.Length} lines");
        dialogueLines = lines;
        currentLineIndex = 0;
        onCompleteCallback = onComplete;     // сохраняем колбэк

        characterAvatarImage.enabled = true;
        dialogueBox.SetActive(true);
        dialogueText.gameObject.SetActive(true);

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (currentLineIndex < dialogueLines.Length)
        {
            var line = dialogueLines[currentLineIndex++];
            dialogueText.text = line.text;
            characterNameText.text = line.characterName;
            characterAvatarImage.sprite = line.avatar;
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        Debug.Log("[DialogueManager] EndDialogue called");
        dialogueBox.SetActive(false);
        dialogueText.gameObject.SetActive(false);
        characterNameText.text = "";
        characterAvatarImage.sprite = null;
        characterAvatarImage.enabled = false;

        // вызываем колбэк после закрытия последней строки
        onCompleteCallback?.Invoke();
        onCompleteCallback = null;

        dialogueLines = null;
        currentLineIndex = 0;
    }

    private void Update()
    {
        if (dialogueBox == null) { enabled = false; return; }
        if (dialogueBox.activeSelf && Input.GetKeyDown(KeyCode.Space))
            DisplayNextLine();
    }
}
