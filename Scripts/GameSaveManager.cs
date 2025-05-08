using UnityEngine;
using SQLite4Unity3d;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Менеджер сохранений: хранит положение игрока, флаги катсцен и обучения в SQLite.
/// Требует наличие модели CheckpointRecord со свойствами:
///   string PlayerID;
///   string SceneName;
///   float PosX, PosY;
///   bool CutsceneCompleted;
///   bool TutorialCompleted;
/// </summary>
public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager instance;
    private const string PlayerId = "Player";

    void Awake()
    {
        // Инициализируем БД (путь задаёт ваш Database.Init())
        Database.Init();

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    //=======================================
    // Позиция игрока
    //=======================================

    /// <summary>
    /// Сохраняет позицию игрока без изменения флагов.
    /// </summary>
    public void SavePlayerPosition(Vector2 position)
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId)
                      ?? new CheckpointRecord { PlayerID = PlayerId };

            rec.SceneName = SceneManager.GetActiveScene().name;
            rec.PosX = position.x;
            rec.PosY = position.y;

            conn.InsertOrReplace(rec);
        }

        Debug.Log($"[GameSaveManager] SavePlayerPosition → Scene={SceneManager.GetActiveScene().name}, Pos=({position.x}, {position.y})");
    }

    /// <summary>
    /// Возвращает сохранённую позицию игрока (или (0,0), если нет).
    /// </summary>
    public Vector2 LoadCheckpointPosition()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId);
            if (rec != null)
                return new Vector2(rec.PosX, rec.PosY);
        }
        return Vector2.zero;
    }

    //=======================================
    // Флаги катсцены
    //=======================================

    public void SetCutsceneCompleted(string id)
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite);
        var rec = conn.Find<CheckpointRecord>(PlayerId)
               ?? new CheckpointRecord { PlayerID = PlayerId };
        // разбиваем старый CSV
        var list = new List<string>();
        if (!string.IsNullOrEmpty(rec.CompletedCutscenes))
            list = rec.CompletedCutscenes.Split(',').ToList();
        // если ещё нет — добавляем
        if (!list.Contains(id))
            list.Add(id);
        rec.CompletedCutscenes = string.Join(",", list);
        // сохраняем
        conn.InsertOrReplace(rec);
        conn.Close();
    }

    public bool IsCutsceneCompleted(string id)
    {
        var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly);
        var rec = conn.Find<CheckpointRecord>(PlayerId);
        conn.Close();
        if (rec == null || string.IsNullOrEmpty(rec.CompletedCutscenes))
            return false;
        return rec.CompletedCutscenes.Split(',').Contains(id);
    }

    //=======================================
    // Флаги обучения (Tutorial)
    //=======================================

    /// <summary>
    /// Устанавливает или сбрасывает флаг, что обучение (tutorial) пройдено.
    /// </summary>
    public void SetTutorialCompleted(bool value)
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId)
                      ?? new CheckpointRecord { PlayerID = PlayerId };

            rec.TutorialCompleted = value;
            if (string.IsNullOrEmpty(rec.SceneName))
                rec.SceneName = SceneManager.GetActiveScene().name;

            conn.InsertOrReplace(rec);
        }

        Debug.Log($"[GameSaveManager] SetTutorialCompleted = {value}");
    }

    /// <summary>
    /// Проверяет, было ли пройдено обучение.
    /// </summary>
    public bool IsTutorialCompleted()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId);
            return rec != null && rec.TutorialCompleted;
        }
    }

    //=======================================
    // Общее сохранение / загрузка сцены
    //=======================================

    /// <summary>
    /// Есть ли вообще сохранённая запись?
    /// </summary>
    public bool HasCheckpoint()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly))
        {
            return conn.Find<CheckpointRecord>(PlayerId) != null;
        }
    }

    /// <summary>
    /// Возвращает имя сохранённой сцены (или пустую строку).
    /// </summary>
    public string GetSavedScene()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadOnly))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId);
            return rec != null ? rec.SceneName : "";
        }
    }

    /// <summary>
    /// Полностью удаляет чекпоинт и все флаги.
    /// </summary>
    public void DeleteCheckpoint()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite))
        {
            conn.Delete<CheckpointRecord>(PlayerId);
        }
        Debug.Log("[GameSaveManager] DeleteCheckpoint");
    }

    public void ClearAllData()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite))
        {
            // Удаляем все записи о текущем PlayerID
            conn.Delete<CheckpointRecord>(PlayerId);
        }
        Debug.Log("[GameSaveManager] ClearAllData()");
    }
    public void SaveCurrentTask(int index)
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId)
                      ?? new CheckpointRecord { PlayerID = PlayerId };
            rec.CurrentTaskIndex = index;
            conn.InsertOrReplace(rec);
        }
    }
    public int LoadCurrentTask()
    {
        using (var conn = new SQLiteConnection(Database.DbPath, SQLiteOpenFlags.ReadWrite))
        {
            var rec = conn.Find<CheckpointRecord>(PlayerId);
            return rec != null ? rec.CurrentTaskIndex : 0;
        }
    }
}
