using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager instance;

    public GameObject dialogueBox;      // Панель диалога
    public TMPro.TextMeshProUGUI dialogueText; // Текст реплики
    public TMPro.TextMeshProUGUI characterNameText; // Текст имени персонажа
    public Image characterAvatarImage; // Изображение для аватарки

    private DialogueLine[] dialogueLines;
    private int currentLineIndex;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // Отключаем все элементы интерфейса
        dialogueBox.SetActive(false);
        dialogueText.text = "";
        characterNameText.text = "";
        characterAvatarImage.sprite = null;
        characterAvatarImage.enabled = false; // Отключаем рендер аватарки
    }

    public void ShowDialogue(DialogueLine[] lines)
    {
        dialogueLines = lines;
        currentLineIndex = 0;

        characterAvatarImage.enabled = true;

        // Включаем панель и текст
        dialogueBox.SetActive(true);
        dialogueText.gameObject.SetActive(true);

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (currentLineIndex < dialogueLines.Length)
        {
            DialogueLine currentLine = dialogueLines[currentLineIndex];

            // Устанавливаем текст реплики, имя и аватарку
            dialogueText.text = currentLine.text;
            characterNameText.text = currentLine.characterName;
            characterAvatarImage.sprite = currentLine.avatar;

            currentLineIndex++;
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        // Скрываем панель, текст, имя и аватарку
        dialogueBox.SetActive(false);
        dialogueText.gameObject.SetActive(false);
        characterNameText.text = "";
        characterAvatarImage.sprite = null;
        characterAvatarImage.enabled = false;

        dialogueLines = null;
        currentLineIndex = 0;
    }

    private void Update()
    {
        if (dialogueBox.activeSelf && Input.GetKeyDown(KeyCode.Space))
        {
            DisplayNextLine();
        }
    }
}

[System.Serializable]
public class DialogueLine
{
    public string characterName; // Имя персонажа
    public Sprite avatar;        // Аватарка персонажа
    public string text;          // Текст реплики
}
