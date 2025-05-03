using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CutsceneController : MonoBehaviour
{
    [Header("UI")]
    public GameObject cutscenePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public Image portraitImage;

    [Header("Катсцена ID")]
    public string cutsceneId;

    [Header("Отключать движение игрока?")]
    public bool disablePlayer = true;

    private PlayerController playerController;
    private DialogueLine[] lines;
    private int currentIndex;
    private bool isPlaying;

    void Start()
    {
        // 1) Проверяем, что DialogueCatalog инициализирован
        if (DialogueCatalog.instance == null)
        {
            Debug.LogError($"[{nameof(CutsceneController)}] Нет DialogueCatalog.instance в сцене!");
            enabled = false;
            return;
        }

        // 2) Проверяем, что все UI‑поля заполнены
        if (cutscenePanel == null || dialogueText == null || speakerNameText == null || portraitImage == null)
        {
            Debug.LogError($"[{nameof(CutsceneController)}] Не назначены все UI‑элементы в инспекторе!");
            enabled = false;
            return;
        }

        // 3) Отключаем игрока (если нужно)
        if (disablePlayer)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerController = p.GetComponent<PlayerController>();
        }

        // 4) Загружаем диалоги
        var (cutLines, interruptAt) = DialogueCatalog.instance.GetCutscene(cutsceneId);
        if (cutLines == null || cutLines.Length == 0)
        {
            Debug.LogWarning($"[{nameof(CutsceneController)}] Катсцена '{cutsceneId}' в сцене '{SceneManager.GetActiveScene().name}' пуста или не найдена.");
            enabled = false;
            return;
        }

        lines = cutLines;
        PlayCutscene();
    }

    public void PlayCutscene()
    {
        // Проверяем общий флаг пройденности
        if (GameSaveManager.instance != null && GameSaveManager.instance.IsCutsceneCompleted(cutsceneId))
        {
            enabled = false;
            return;
        }

        if (disablePlayer && playerController != null)
            playerController.enabled = false;

        currentIndex = 0;
        isPlaying = true;
        cutscenePanel.SetActive(true);
        ShowNextLine();
    }

    void Update()
    {
        if (!isPlaying) return;
        if (Input.GetKeyDown(KeyCode.Space))
            ShowNextLine();
    }

    private void ShowNextLine()
    {
        // Дополнительно проверяем, что lines не null
        if (lines == null)
        {
            EndCutscene();
            return;
        }

        if (currentIndex < lines.Length)
        {
            var L = lines[currentIndex++];
            dialogueText.text = L.text;
            speakerNameText.text = L.characterName;
            portraitImage.sprite = L.avatar;
        }
        else
        {
            EndCutscene();
        }
    }

    private void EndCutscene()
    {
        isPlaying = false;
        cutscenePanel.SetActive(false);

        if (disablePlayer && playerController != null)
            playerController.enabled = true;

        GameSaveManager.instance.SetCutsceneCompleted(cutsceneId);
        enabled = false;
    }
}
