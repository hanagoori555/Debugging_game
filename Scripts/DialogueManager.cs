using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;
    public static bool IsDialogueActive { get; private set; } = false;

    // Событие: кто-то завершил очередную интеракцию — даём id и текущий totalCount
    public static event Action<string, int> OnInteractionCompleted;

    public GameObject dialogueBox;
    public TMPro.TextMeshProUGUI dialogueText;
    public TMPro.TextMeshProUGUI characterNameText;
    public Image characterAvatarImage;

    private DialogueLine[] dialogueLines;
    private int currentLineIndex;
    private Action onCompleteCallback;
    private static Dictionary<string, int> _interactionCounts = new Dictionary<string, int>();

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

        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (dialogueText != null) dialogueText.text = "";
        if (characterNameText != null) characterNameText.text = "";
        if (characterAvatarImage != null) characterAvatarImage.enabled = false;
    }

    /// <summary>
    /// Запускает диалог и вызывает onComplete после EndDialogue()
    /// </summary>
    public void ShowDialogue(DialogueLine[] lines, Action onComplete = null)
    {
        IsDialogueActive = true;
        PlayerController.InputBlocked = true;

        Debug.Log($"[DialogueManager] ShowDialogue called with {lines?.Length ?? 0} lines");
        dialogueLines = lines;
        currentLineIndex = 0;
        onCompleteCallback = onComplete;

        if (characterAvatarImage != null) characterAvatarImage.enabled = true;
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (dialogueText != null) dialogueText.gameObject.SetActive(true);

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
            return;

        if (currentLineIndex < dialogueLines.Length)
        {
            var line = dialogueLines[currentLineIndex++];
            if (dialogueText != null) dialogueText.text = line.text;
            if (characterNameText != null) characterNameText.text = line.characterName;
            if (line.avatar != null)
            {
                characterAvatarImage.enabled = true;
                characterAvatarImage.sprite = line.avatar;
            }
            else
            {
                characterAvatarImage.enabled = false;
            }
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        IsDialogueActive = false;
        PlayerController.InputBlocked = false;

        Debug.Log("[DialogueManager] EndDialogue called");
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (dialogueText != null) dialogueText.gameObject.SetActive(false);
        if (characterNameText != null) characterNameText.text = "";
        if (characterAvatarImage != null) { characterAvatarImage.sprite = null; characterAvatarImage.enabled = false; }

        Debug.Log($"[DialogueManager] Диалог завершён, вызываем колбэк.");
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

    // -----------------------------
    // Interaction counting API
    // -----------------------------

    /// <summary>
    /// Увеличить счётчик интеракции с id на 1 и уведомить слушателей.
    /// </summary>
    public static void MarkInteractionCompleted(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!_interactionCounts.ContainsKey(id)) _interactionCounts[id] = 0;
        _interactionCounts[id]++;
        Debug.Log($"[DialogueManager] Interaction marked completed: '{id}', total={_interactionCounts[id]}");
        OnInteractionCompleted?.Invoke(id, _interactionCounts[id]);
    }

    public static bool HasCompletedInteraction(string id, int requiredCount = 1)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (!_interactionCounts.ContainsKey(id)) return false;
        return _interactionCounts[id] >= requiredCount;
    }

    public static void ResetInteraction(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_interactionCounts.ContainsKey(id)) _interactionCounts.Remove(id);
    }
}
