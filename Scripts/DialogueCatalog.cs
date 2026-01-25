using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(0)]
public class DialogueCatalog : MonoBehaviour
{
    public static DialogueCatalog instance;
    private SceneDialogueData _sceneData;
    private StateData _currentState;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
        ReloadForActiveScene();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += (_, __) => ReloadForActiveScene();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= (_, __) => ReloadForActiveScene();
    }

    public void ReloadForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        _sceneData = DialogueLoader.LoadSceneData(sceneName);

        if (_sceneData == null)
        {
            Debug.LogWarning($"[DialogueCatalog] No dialogue data for scene {sceneName}");
            _currentState = null;
            return;
        }

        if (TaskManager.instance == null)
        {
            Debug.LogError("[DialogueCatalog] TaskManager.instance is null");
            _currentState = null;
            return;
        }

        int stateId = TaskManager.instance.GetCurrentTaskIndex();

        // Защита от отсутствия состояний
        if (_sceneData.states == null || _sceneData.states.Length == 0)
        {
            Debug.LogWarning($"[DialogueCatalog] No states defined for scene {sceneName}");
            _currentState = null;
            return;
        }

        _currentState = _sceneData.states.FirstOrDefault(s => s.stateId == stateId);

        if (_currentState == null)
        {
            Debug.LogWarning($"[DialogueCatalog] No state {stateId} in scene {sceneName}");
            return;
        }

        Debug.Log($"[DialogueCatalog] ReloadForActiveScene: Loaded state {stateId} for scene {sceneName}");

        // Добавила проверки на null для безопасного логирования
        Debug.Log($"[DialogueCatalog] interactables: {(_currentState.interactables != null ? string.Join(",", _currentState.interactables.Select(i => i.objectId)) : "null")}");
        Debug.Log($"[DialogueCatalog] autoDialogs count: {(_currentState.autoDialogs != null ? _currentState.autoDialogs.Length : 0)}");
        Debug.Log($"[DialogueCatalog] cutscenes: {(_currentState.cutscenes != null ? string.Join(",", _currentState.cutscenes.Select(c => c.cutsceneId)) : "null")}");
    }

    public DialogueLine[] GetAutoDialogueForCurrentState()
    {
        if (_currentState == null || _currentState.autoDialogs == null)
        {
            Debug.LogWarning("[DialogueCatalog] No auto dialogs available");
            return new DialogueLine[0];
        }

        return _currentState.autoDialogs.Select(d => new DialogueLine
        {
            characterName = d.characterName,
            avatar = string.IsNullOrEmpty(d.avatar) ? null : Resources.Load<Sprite>($"Portraits/{d.avatar}"),
            text = d.text,
            background = string.IsNullOrEmpty(d.backgroundImage) ? null : Resources.Load<Sprite>($"Backgrounds/{d.backgroundImage}")
        }).ToArray();
    }

    public DialogueLine[] GetInteractableLines(string objectId)
    {
        Debug.Log($"[DialogueCatalog] GetInteractableLines: state={_currentState?.stateId}, objectId='{objectId}'");
        var entry = _currentState?.interactables
                      .FirstOrDefault(i => i.objectId == objectId);
        if (entry == null)
        {
            Debug.LogWarning($"[DialogueCatalog]  → Не найден объект с ID='{objectId}'");
            return new DialogueLine[0];
        }

        Debug.Log($"[DialogueCatalog]  → Найдены {entry.dialogue.Length} строк(а) для '{objectId}'");
        return ConvertLines(entry.dialogue);
    }

    public (DialogueLine[] lines, int interruptAt) GetCutscene(string cutsceneId)
    {
        var cd = _currentState?.cutscenes
            .FirstOrDefault(c => c.cutsceneId == cutsceneId);
        return (ConvertLines(cd?.dialogue), cd?.interruptAtLine ?? -1);
    }

    private DialogueLine[] ConvertLines(LineData[] arr)
    {
        if (arr == null) return new DialogueLine[0];
        return arr.Select(d => new DialogueLine
        {
            characterName = d.characterName,
            avatar = string.IsNullOrEmpty(d.avatar) ? null : Resources.Load<Sprite>($"Portraits/{d.avatar}"),
            text = d.text,
            background = string.IsNullOrEmpty(d.backgroundImage)
                        ? null
                        : Resources.Load<Sprite>($"Backgrounds/{d.backgroundImage}")
        }).ToArray();
    }

    public string CurrentCutsceneId { get; private set; }

    public (DialogueLine[] lines, int interruptAt) GetCutsceneForCurrentState()
    {
        if (_currentState == null)
        {
            Debug.LogWarning("[DialogueCatalog] GetCutsceneForCurrentState called but _currentState is null");
            return (new DialogueLine[0], -1);
        }

        Debug.Log($"[DialogueCatalog] GetCutsceneForCurrentState: currentStateId={_currentState.stateId}");

        if (_currentState.cutscenes == null || _currentState.cutscenes.Length == 0)
        {
            Debug.LogWarning("[DialogueCatalog] No cutscenes in current state");
            return (new DialogueLine[0], -1);
        }

        int taskId = TaskManager.instance.GetCurrentTaskIndex();
        var cd = _currentState.cutscenes.FirstOrDefault(c => c.cutsceneId == taskId.ToString());

        if (cd == null)
        {
            Debug.LogWarning($"[DialogueCatalog] No cutscene matching taskId={taskId}. Using first available.");
            cd = _currentState.cutscenes.FirstOrDefault();
        }

        if (cd == null)
        {
            Debug.LogWarning("[DialogueCatalog] No cutscenes found at all for current state");
            return (new DialogueLine[0], -1);
        }

        Debug.Log($"[DialogueCatalog] Selected cutsceneId='{cd.cutsceneId}' with {cd.dialogue.Length} lines");
        CurrentCutsceneId = cd.cutsceneId;
        return (ConvertLines(cd.dialogue), cd.interruptAtLine);
    }

    public void RefreshState()
    {
        if (_sceneData == null) return;
        int stateId = TaskManager.instance.GetCurrentTaskIndex();
        _currentState = _sceneData.states.FirstOrDefault(s => s.stateId == stateId);
        Debug.Log($"[DialogueCatalog] Refreshed state to {stateId}");
    }
}
