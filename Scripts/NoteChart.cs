// NoteChart.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewNoteChart", menuName = "RhythmGame/NoteChart")]
public class NoteChart : ScriptableObject
{
    public AudioClip music;
    public NoteData[] notes; 
}
