using UnityEngine;

public class UIRootController : MonoBehaviour
{
    void Awake()
    {
        // Этот GameObject и все его дочерние объекты
        // не будут удаляться при загрузке новой сцены
        DontDestroyOnLoad(gameObject);
    }
}
