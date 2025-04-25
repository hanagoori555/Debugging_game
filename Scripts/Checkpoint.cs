using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
            if (rb != null && GameSaveManager.instance != null)
            {
                Vector2 playerPos = rb.position;
                GameSaveManager.instance.SavePlayerPosition(transform.position);
            }
        }
    }
}
