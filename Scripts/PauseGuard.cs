using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Универсальная охрана паузы.
/// - IsBlockedOpen  — если true, открывать паузу нельзя (включает Cutscene/Dialogue/InputBlocked)
/// - IsHiddenVisual — если true, визуальная панель паузы полностью скрыта и кнопка отключена
/// 
/// Внешние системы могут ставить причину блокировки по ключу (SetBlockOpen/SetHideVisual).
/// </summary>
public static class PauseGuard
{
    private static HashSet<string> _blockOpenReasons = new HashSet<string>();
    private static HashSet<string> _hideVisualReasons = new HashSet<string>();

    // Если хотя бы одна причина блокирует открытие -> блокируем.
    // Также учитываем твои текущие глобальные индикаторы (Cutscene/Dialogue/InputBlocked)
    public static bool IsBlockedOpen =>
        _blockOpenReasons.Count > 0
        || (CutsceneController.instance != null && CutsceneController.IsCutscenePlaying)
        || DialogueManager.IsDialogueActive
        || PlayerController.InputBlocked;

    // Если хотя бы одна причина — скрываем визуал паузы
    public static bool IsHiddenVisual => _hideVisualReasons.Count > 0;

    // Для обратной совместимости
    public static bool CanOpenPause() => !IsBlockedOpen;

    // Управление причинами
    public static void SetBlockOpen(string key, bool enable)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (enable) _blockOpenReasons.Add(key);
        else _blockOpenReasons.Remove(key);
        Debug.Log($"[PauseGuard] BlockOpen '{key}' -> {IsBlockedOpen}");
    }

    public static void SetHideVisual(string key, bool enable)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (enable) _hideVisualReasons.Add(key);
        else _hideVisualReasons.Remove(key);
        Debug.Log($"[PauseGuard] HideVisual '{key}' -> {IsHiddenVisual}");
    }

    public static void SetBoth(string key, bool enable)
    {
        SetBlockOpen(key, enable);
        SetHideVisual(key, enable);
    }
}
