using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("Фон и список спрайтов")]
    public SpriteRenderer backgroundRenderer;
    public List<Sprite> backgrounds;

    [Header("Точка, куда возвращать игрока после смены фона")]
    public Transform leftSpawnPoint;

    [Header("Тег правого триггера конца сцены")]
    public string stageEndTag = "StageEnd";

    /// <summary>
    /// Вызывается, как только фон сменился и игрок телепортирован.
    /// </summary>
    public static event Action OnBackgroundTransition;

    private int currentIndex = 0;

    void Start()
    {
        if (backgrounds != null && backgrounds.Count > 0)
            backgroundRenderer.sprite = backgrounds[0];
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 1) Сменить фон и телепорт
        currentIndex = (currentIndex + 1) % backgrounds.Count;
        backgroundRenderer.sprite = backgrounds[currentIndex];
        other.transform.root.position = leftSpawnPoint.position;

        // 2) Блокируем движение
        PlayerController.InputBlocked = true;

        // 3) Уведомляем TaskManager
        OnBackgroundTransition?.Invoke();
    }
}
