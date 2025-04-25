using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CutsceneController : MonoBehaviour
{
    [Header("UI элементы катсцены")]
    public GameObject cutscenePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public Image portraitImage;

    [Header("Данные диалога")]
    public string[] dialogues;
    public string[] speakerNames;
    public Sprite[] portraits;

    [Header("Настройка игрока")]
    public GameObject player;
    private PlayerController playerController;

    private int currentDialogueIndex = 0;
    private bool isPlaying = false;

    /// <summary>
    /// Запустить катсцену вручную
    /// </summary>
    public void PlayCutscene()
    {
        // Если уже пройдена, сразу отключаем и уничтожаем
        if (GameSaveManager.instance != null && GameSaveManager.instance.IsCutscene1Completed())
        {
            cutscenePanel.SetActive(false);
            Destroy(gameObject);
            return;
        }

        // Отключаем управление игроком
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.enabled = false;
        }

        // Запускаем katсцену
        isPlaying = true;
        cutscenePanel.SetActive(true);
        currentDialogueIndex = 0;
        ShowDialogue();
    }

    void Update()
    {
        if (!isPlaying) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentDialogueIndex++;
            if (currentDialogueIndex < dialogues.Length)
            {
                ShowDialogue();
            }
            else
            {
                EndCutscene();
            }
        }
    }

    void ShowDialogue()
    {
        dialogueText.text = dialogues[currentDialogueIndex];
        speakerNameText.text = speakerNameText != null ? speakerNames[currentDialogueIndex] : string.Empty;
        portraitImage.sprite = portraits[currentDialogueIndex];
    }

    /// <summary>
    /// Завершение катсцены
    /// </summary>
    public void EndCutscene()
    {
        isPlaying = false;

        if (GameSaveManager.instance != null)
        {
            GameSaveManager.instance.SetCutscene1Completed(true);
            Vector2 pos = player != null
                ? (Vector2)player.transform.position
                : Vector2.zero;
            GameSaveManager.instance.SaveCheckpoint(pos, true);
        }

        cutscenePanel.SetActive(false);
        if (playerController != null)
            playerController.enabled = true;

        Destroy(gameObject);
    }
}
