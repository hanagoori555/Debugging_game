using System;

[Serializable]
public class LineData
{
    public string characterName;
    public string avatar;   // имя спрайта в Resources/Sprites/Portraits
    public string text;
}

[Serializable]
public class InteractableData
{
    public string objectId;
    public LineData[] dialogue;
}

[Serializable]
public class CutsceneData
{
    public string cutsceneId;
    public LineData[] dialogue;
    public int interruptAtLine;  // -1 если без интерактива
}

[Serializable]
public class StateData
{
    public int stateId;
    public InteractableData[] interactables;
    public CutsceneData[] cutscenes;
}

[Serializable]
public class SceneDialogueData
{
    public string sceneName;
    public StateData[] states;
}
