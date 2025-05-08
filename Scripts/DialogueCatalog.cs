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

    void ReloadForActiveScene()
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


        if (_currentState == null)
        {
            Debug.LogWarning($"[DialogueCatalog] No state {stateId} in scene {_sceneData.sceneName}");
        }
    }



    public DialogueLine[] GetInteractableLines(string objectId)
    {
        var dat = _currentState?.interactables
            .FirstOrDefault(i => i.objectId == objectId)
            ?.dialogue;
        return ConvertLines(dat);
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
        var cd = _currentState?.cutscenes.FirstOrDefault();
        if (cd == null) return (new DialogueLine[0], -1);
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
