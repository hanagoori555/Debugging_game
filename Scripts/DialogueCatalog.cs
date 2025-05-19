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
        _sceneData = DialogueLoader.LoadSceneData(SceneManager.GetActiveScene().name);
        if (_sceneData == null)
        {
            Debug.LogWarning($"[DialogueCatalog] No dialogue data for scene {SceneManager.GetActiveScene().name}");
            _currentState = null;
            return;
        }

        // Защита: убедимся, что TaskManager.instance и _sceneData.states не null
        if (TaskManager.instance == null)
        {
            Debug.LogError("[DialogueCatalog] TaskManager.instance is null – не загружен ли TaskManager?");
            _currentState = null;
            return;
        }
        if (_sceneData.states == null)
        {
            Debug.LogError($"[DialogueCatalog] _sceneData.states is null для сцены {_sceneData.sceneName}");
            _currentState = null;
            return;
        }

        int stateId = TaskManager.instance.GetCurrentTaskIndex();
        _currentState = _sceneData.states
            .FirstOrDefault(s => s.stateId == stateId);
        Debug.Log($"[DialogueCatalog] interactables: {string.Join(",", _currentState.interactables.Select(i => i.objectId))}");
        Debug.Log($"[DialogueCatalog] cutscenes: {string.Join(",", _currentState.cutscenes.Select(c => c.cutsceneId))}");

        if (_currentState == null)
        {
            Debug.LogWarning($"[DialogueCatalog] No state {stateId} in scene {_sceneData.sceneName}");
        }
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
            avatar = Resources.Load<Sprite>($"Sprites/Portraits/{d.avatar}"),
            text = d.text
        }).ToArray();
    }

    public string CurrentCutsceneId { get; private set; }

    public (DialogueLine[] lines, int interruptAt) GetCutsceneForCurrentState()
    {
        // Узнаём, какой state сейчас
        Debug.Log($"[DialogueCatalog] GetCutsceneForCurrentState: currentStateId={_currentState?.stateId}");

        // Выводим все доступные cutsceneId в этом state
        if (_currentState?.cutscenes != null)
        {
            Debug.Log($"[DialogueCatalog]  Available cutscenes count={_currentState.cutscenes.Length}");
            foreach (var candidate in _currentState.cutscenes)
            {
                Debug.Log($"[DialogueCatalog]   candidate.cutsceneId='{candidate.cutsceneId}', dialogueLines={candidate.dialogue?.Length}");
            }
        }
        else
        {
            Debug.LogWarning("[DialogueCatalog]  No cutscenes array on current state");
        }

        // выбор по id задачи или по triggerParam
        var task = TaskManager.instance.GetCurrentTaskData();
        Debug.Log($"[DialogueCatalog]  Looking for cutscene where cutsceneId == '{task.id}' or '{task.triggerParam}'");
        var cd = _currentState?.cutscenes
                     .FirstOrDefault(c => c.cutsceneId == task.id.ToString()
                                       || c.cutsceneId == task.triggerParam);

        // Логируем результат поиска
        if (cd == null)
        {
            Debug.LogWarning($"[DialogueCatalog]  → No matching cutscene found for state={_currentState.stateId}");
            return (new DialogueLine[0], -1);
        }
        Debug.Log($"[DialogueCatalog]  → Selected cutsceneId='{cd.cutsceneId}' with {cd.dialogue.Length} lines");
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
