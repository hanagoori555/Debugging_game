using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CutsceneController : MonoBehaviour
{
    public static CutsceneController instance;
    public static bool IsCutscenePlaying { get; private set; } = false;

    [Header("UI элементы")]
    public GameObject cutscenePanel;
    public Image backgroundImage;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public Image portraitImage;

    [Header("Настройки")]
    public bool disablePlayer = true;

    private PlayerController playerController;
    private DialogueLine[] lines;
    private int currentIndex;
    private int interruptAt;
    private bool isPlaying;

    // Колбэк завершения
    private System.Action onCompleteCallback;

    void Awake()
    {
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

        if (cutscenePanel != null) cutscenePanel.SetActive(false);
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerController = p.GetComponent<PlayerController>();
    }

    public void StartCutsceneForCurrentState(System.Action onComplete = null)
    {
        IsCutscenePlaying = true;
        PlayerController.InputBlocked = true;

        this.onCompleteCallback = onComplete;

        var tuple = DialogueCatalog.instance.GetCutsceneForCurrentState();
        lines = tuple.lines;
        interruptAt = tuple.interruptAt;

        if (lines == null || lines.Length == 0)
        {
            IsCutscenePlaying = false;
            PlayerController.InputBlocked = false;
            onCompleteCallback?.Invoke();
            return;
        }

        if (disablePlayer && playerController != null)
            playerController.enabled = false;

        if (lines[0].background != null)
        {
            backgroundImage.sprite = lines[0].background;
            backgroundImage.gameObject.SetActive(true);
        }
        else
        {
            backgroundImage.gameObject.SetActive(false); // Скрываем если нет картинки
        }

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
        if (currentIndex >= lines.Length)
        {
            EndCutscene();
            return;
        }

        var L = lines[currentIndex];
        dialogueText.text = L.text;
        speakerNameText.text = L.characterName;

        if (L.avatar != null)
        {
            portraitImage.gameObject.SetActive(true);
            portraitImage.sprite = L.avatar;
        }
        else
        {
            portraitImage.gameObject.SetActive(false);
        }

        if (L.background != null)
        {
            backgroundImage.sprite = L.background;
            backgroundImage.gameObject.SetActive(true);
        }
        else
        {
            backgroundImage.gameObject.SetActive(false);
        }

        currentIndex++;
    }

    private void EndCutscene()
    {
        IsCutscenePlaying = false;
        PlayerController.InputBlocked = false;

        Debug.Log("[CutsceneController] EndCutscene");
        isPlaying = false;
        cutscenePanel.SetActive(false);

        if (disablePlayer && playerController != null)
            playerController.enabled = true;

        onCompleteCallback?.Invoke();
        onCompleteCallback = null;
    }
}