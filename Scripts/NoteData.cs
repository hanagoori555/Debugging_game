[System.Serializable]
public class NoteData
{
    public float time;    // Время появления ноты
    public int lane;      // Номер дорожки (0..7)
    public float duration; // Длительность ноты (0 = короткая, >0 = длинная)
}
