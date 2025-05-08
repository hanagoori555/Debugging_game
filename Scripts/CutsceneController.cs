using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CutsceneController : MonoBehaviour
{
    [Header("UI элементы катсцены")]
    public GameObject cutscenePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public Image portraitImage;

    [Header("Отключать движение игрока во время катсцены")]
    public bool disablePlayer = true;

    private PlayerController playerController;
    private DialogueLine[] lines;    // строки текущей катсцены
    private int currentIndex;
    private int interruptAt;         // индекс для прерывания
    private bool isPlaying;

    void Awake()
    {
        // Отключаем панель заранее
        if (cutscenePanel != null)
            cutscenePanel.SetActive(false);
    }

    void Start()
    {
        // Проверяем UI и DialogueCatalog
        if (DialogueCatalog.instance == null ||
            cutscenePanel == null ||
            dialogueText == null ||
            speakerNameText == null ||
            portraitImage == null)
        {
            Debug.LogError("[CutsceneController] Не все зависимости назначены!");
            enabled = false;
            return;
        }

        // Получаем управление игроком (если нужно)
        if (disablePlayer)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                playerController = p.GetComponent<PlayerController>();
        }
    }

    /// <summary>
    /// Запуск катсцены для текущего stateId из DialogueCatalog.
    /// </summary>
    public void StartCutsceneForCurrentState()
    {
        // Забираем линии и interrupt
        var tuple = DialogueCatalog.instance.GetCutsceneForCurrentState();
        lines = tuple.lines;
        interruptAt = tuple.interruptAt;

        if (lines == null || lines.Length == 0)
            return;  // ничего не делаем, если нет катсцены

        // Отключаем управление игроком
        if (disablePlayer && playerController != null)
            playerController.enabled = false;

        // Запускаем воспроизведение
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
        if (currentIndex < lines.Length)
        {
            var L = lines[currentIndex++];
            dialogueText.text = L.text;
            speakerNameText.text = L.characterName;
            portraitImage.sprite = L.avatar;

            // Прерывание, если нужно
            if (interruptAt >= 0 && currentIndex == interruptAt)
            {
                EndCutscene();
            }
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

        // Можно здесь пометить флаг завершённой катсцены, если нужно:
        // GameSaveManager.instance.SetCutsceneCompleted(DialogueCatalog.instance.CurrentCutsceneId);
    }
}
