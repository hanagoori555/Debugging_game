using UnityEngine;

[CreateAssetMenu(fileName = "NewNoteChart", menuName = "RhythmGame/NoteChart")]
public class NoteChart : ScriptableObject
{
    public NoteData[] notes;
}
