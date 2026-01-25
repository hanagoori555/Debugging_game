using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string characterName;
    public Sprite avatar;             // Если null — аватар не показываем
    public string text;
    public Sprite background;         // Если не null — используем как фон
}
