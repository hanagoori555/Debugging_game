using UnityEngine;
using SQLite4Unity3d;
using System.IO;

public static class Database
{
    // Полный путь к файлу БД на устройстве
    public static string DbPath => Path.Combine(Application.persistentDataPath, "game.db");

    // Вызывается при старте игры для создания файла и таблицы, если нужно
    public static void Init()
    {
        // Открываем или создаём БД
        var connection = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);
        // Создаём таблицу чекпоинтов по модели CheckpointRecord
        connection.CreateTable<CheckpointRecord>();
        connection.Close();
    }
}

// Модель — будет соответствовать таблице CHECKPOINTS
public class CheckpointRecord
{
    [PrimaryKey]
    public string PlayerID { get; set; }

    public string SceneName { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }

    public bool Cutscene1 { get; set; }
    public bool Cutscene2 { get; set; }
    public bool Cutscene3 { get; set; }
}
