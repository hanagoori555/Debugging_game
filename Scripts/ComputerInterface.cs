using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ComputerInterface : MonoBehaviour
{
    [Header("Cutscene IDs")]
    public string preCutsceneId;
    public string postCutsceneId;

    [Header("Scene to load after PostDialogue")]
    public string sceneToLoadAfter = "School";

    [Header("UI: Code Input")]
    public GameObject codePanel;
    public TMP_InputField codeInput;
    public Button executeButton;

    [Header("UI: Dialogue")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public Image portraitImage;

    private enum Phase { PreDialogue, Interaction, PostDialogue, Finished }
    private Phase phase;
    private DialogueLine[] lines;
    private int interruptAt;
    private int currentIndex;

    void Awake()
    {
        // Убедиться, что всё прикреплено
        Debug.Assert(codePanel != null, "Assign codePanel");
        Debug.Assert(codeInput != null, "Assign codeInput");
        Debug.Assert(executeButton != null, "Assign executeButton");
        Debug.Assert(dialoguePanel != null, "Assign dialoguePanel");
        Debug.Assert(dialogueText != null, "Assign dialogueText");
        Debug.Assert(speakerNameText != null, "Assign speakerNameText");
        Debug.Assert(portraitImage != null, "Assign portraitImage");
    }

    void Start()
    {
        executeButton.onClick.AddListener(OnExecute);
        BeginPreDialogue();
    }

    void Update()
    {
        if (phase == Phase.PreDialogue || phase == Phase.PostDialogue)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                AdvanceDialogue();
        }
    }

    private void BeginPreDialogue()
    {
        phase = Phase.PreDialogue;
        codePanel.SetActive(false);
        dialoguePanel.SetActive(true);

        (lines, interruptAt) = DialogueCatalog.instance.GetCutscene(preCutsceneId);
        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning($"Pre-cutscene '{preCutsceneId}' not found or empty.");
            BeginInteraction();
            return;
        }

        currentIndex = 0;
        ShowLine();
    }

    private void BeginInteraction()
    {
        phase = Phase.Interaction;
        dialoguePanel.SetActive(false);
        codePanel.SetActive(true);
        codeInput.text = "";
    }

    private void BeginPostDialogue()
    {
        phase = Phase.PostDialogue;
        codePanel.SetActive(false);
        dialoguePanel.SetActive(true);

        (lines, interruptAt) = DialogueCatalog.instance.GetCutscene(postCutsceneId);
        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning($"Post-cutscene '{postCutsceneId}' not found or empty.");
            FinishSequence();
            return;
        }

        currentIndex = 0;
        ShowLine();
    }

    private void AdvanceDialogue()
    {
        // Прерываем PreDialogue на интерактив
        if (phase == Phase.PreDialogue && currentIndex == interruptAt)
        {
            BeginInteraction();
            return;
        }

        currentIndex++;
        if (currentIndex < lines.Length)
        {
            ShowLine();
        }
        else
        {
            if (phase == Phase.PreDialogue)
                BeginInteraction();
            else if (phase == Phase.PostDialogue)
                FinishSequence();
        }
    }

    private void ShowLine()
    {
        var line = lines[currentIndex];
        dialogueText.text = line.text;
        speakerNameText.text = line.characterName;
        portraitImage.sprite = line.avatar;
    }

    private void OnExecute()
    {
        // Ваш код проверки
        if (codeInput.text.Trim() == "fix;")
            BeginPostDialogue();
        else
            Debug.LogWarning("Ошибка синтаксиса: введите «fix;»");
    }

    private void FinishSequence()
    {
        phase = Phase.Finished;
        codePanel.SetActive(false);
        dialoguePanel.SetActive(false);
        SceneManager.LoadScene(sceneToLoadAfter);
    }
}
