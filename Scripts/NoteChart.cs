// NoteChart.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewNoteChart", menuName = "RhythmGame/NoteChart")]
public class NoteChart : ScriptableObject
{
    public AudioClip music;    // трек для чарта
    public NoteData[] notes;   // тайминги и дорожки
}
