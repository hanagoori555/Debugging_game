using SQLite4Unity3d;

[Table("Checkpoint")]
public class CheckpointRecord
{
    [PrimaryKey, NotNull]
    public string PlayerID { get; set; }

    public string SceneName { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }

    public string CompletedCutscenes { get; set; }
    public bool TutorialCompleted { get; set; }

    public int CurrentTaskIndex { get; set; }
}
