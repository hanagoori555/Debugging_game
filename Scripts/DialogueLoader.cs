using UnityEngine;

public static class DialogueLoader
{
    public static SceneDialogueData LoadSceneData(string sceneName)
    {
        TextAsset ta = Resources.Load<TextAsset>($"Dialogues/{sceneName}");
        if (ta == null)
        {
            Debug.LogError($"[DialogueLoader] JSON not found for scene {sceneName}");
            return null;
        }
        return JsonUtility.FromJson<SceneDialogueData>(ta.text);
    }
}
