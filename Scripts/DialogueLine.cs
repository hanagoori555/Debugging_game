using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string characterName;
    public Sprite avatar;             // если null — аватар не показываем
    public string text;
    public Sprite background;         // если не null — используем как фон
}
