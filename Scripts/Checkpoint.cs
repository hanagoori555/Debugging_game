using System.Collections;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public float activationDelay = 1f;
    private bool isActive = false;

    void Start()
    {
        StartCoroutine(ActivateAfterDelay());
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(activationDelay);
        isActive = true;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive) return;


        if (collision.CompareTag("Player"))
        {
            Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
            if (rb != null && GameSaveManager.instance != null)
            {
                Vector2 playerPos = rb.position;
                GameSaveManager.instance.SavePlayerPosition(transform.position);
                int currentIndex = TaskManager.instance.GetCurrentTaskIndex();
                GameSaveManager.instance.SaveCurrentTask(currentIndex);
            }
        }
    }
}
