using UnityEngine;
using SQLite4Unity3d;
using UnityEngine.SceneManagement;

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager instance;
    private const string PlayerId = "Player";

    void Awake()
    {
        // Инициализация базы
        Database.Init();
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // ===== Флаг обучения =====
    /// <summary>
    /// Установить, что обучение пройдено.
    /// </summary>
    public void SetTutorialCompleted(bool value)
    {
        PlayerPrefs.SetInt("TutorialCompleted", value ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[GameSaveManager] TutorialCompleted = {value}");
    }

    /// <summary>
    /// Проверить, пройдено ли обучение.
    /// </summary>
    public bool IsTutorialCompleted()
    {
        return PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
    }

    // ===== Сохранение положения игрока (не затрагивает флаги) =====
    public void SavePlayerPosition(Vector2 position)
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite);
        var rec = conn.Find<CheckpointRecord>(PlayerId)
                  ?? new CheckpointRecord { PlayerID = PlayerId };

        rec.SceneName = SceneManager.GetActiveScene().name;
        rec.PosX = position.x;
        rec.PosY = position.y;
        // сохраняем существующие флаги катсцен
        conn.InsertOrReplace(rec);
        conn.Close();

        Debug.Log($"[GameSaveManager] SavePlayerPosition → Scene={rec.SceneName}, Pos=({rec.PosX},{rec.PosY})");
    }

    // ===== Сохранение чекпоинта + флаг первой катсцены =====
    public void SaveCheckpoint(Vector2 position, bool cutscene1Completed)
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite);
        var existing = conn.Find<CheckpointRecord>(PlayerId);

        var rec = new CheckpointRecord
        {
            PlayerID = PlayerId,
            SceneName = SceneManager.GetActiveScene().name,
            PosX = position.x,
            PosY = position.y,
            Cutscene1 = cutscene1Completed,
            Cutscene2 = existing != null && existing.Cutscene2,
            Cutscene3 = existing != null && existing.Cutscene3
        };

        conn.InsertOrReplace(rec);
        conn.Close();

        Debug.Log($"[GameSaveManager] SaveCheckpoint → Scene={rec.SceneName}, Pos=({rec.PosX},{rec.PosY}), Cut1={rec.Cutscene1}");
    }

    // ===== Удаление чекпоинта (для Новой игры) =====
    public void DeleteCheckpoint()
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite);
        conn.Delete<CheckpointRecord>(PlayerId);
        conn.Close();
        // Сброс флага обучения
        SetTutorialCompleted(false);
        Debug.Log("[GameSaveManager] Checkpoint deleted and tutorial reset");
    }

    public bool HasCheckpoint()
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly);
        var exists = conn.Find<CheckpointRecord>(PlayerId) != null;
        conn.Close();
        return exists;
    }

    public string GetSavedScene()
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly);
        var rec = conn.Find<CheckpointRecord>(PlayerId);
        conn.Close();
        return rec != null ? rec.SceneName : string.Empty;
    }

    public Vector2 LoadCheckpointPosition()
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly);
        var rec = conn.Find<CheckpointRecord>(PlayerId);
        conn.Close();
        if (rec != null) return new Vector2(rec.PosX, rec.PosY);
        return Vector2.zero;
    }

    public void SetCutscene1Completed(bool value)
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite);
        var rec = conn.Find<CheckpointRecord>(PlayerId) ?? new CheckpointRecord { PlayerID = PlayerId };

        rec.Cutscene1 = value;
        if (string.IsNullOrEmpty(rec.SceneName))
            rec.SceneName = SceneManager.GetActiveScene().name;

        conn.InsertOrReplace(rec);
        conn.Close();

        Debug.Log($"[GameSaveManager] SetCutscene1Completed = {value}");
    }

    public bool IsCutscene1Completed()
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly);
        var rec = conn.Find<CheckpointRecord>(PlayerId);
        conn.Close();
        return rec != null && rec.Cutscene1;
    }
}
