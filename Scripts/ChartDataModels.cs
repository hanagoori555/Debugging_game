using System;

[Serializable]
public class NoteData
{
    public float time;
    public int lane;
    public float duration;
}

[Serializable]
public class ChartData
{
    public int battleNumber;
    public string musicPath;   // путь к AudioClip в Resources
    public NoteData[] notes;
}
