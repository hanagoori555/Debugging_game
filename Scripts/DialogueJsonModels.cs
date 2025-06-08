using System;

[Serializable]
public class LineData
{
    public string characterName;
    public string avatar;   // имя спрайта в Resources/Sprites/Portraits
    public string text;
    public string backgroundImage;  // <-- имя файла спрайта в Resources/Backgrounds
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

[System.Serializable]
public class AutoDialogData
{
    public string characterName;
    public string avatar;
    public string text;
    public string backgroundImage;
}

[Serializable]
public class StateData
{
    public int stateId;
    public InteractableData[] interactables;
    public CutsceneData[] cutscenes;
    public AutoDialogData[] autoDialogs;
}

[Serializable]
public class SceneDialogueData
{
    public string sceneName;
    public StateData[] states;
}
