using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 movement;
    private Animator animator;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Если вернулись по чекпоинту и сцена совпадает — позиционируем здесь (дополнительно к MainMenuController)
        if (GameSaveManager.instance != null && GameSaveManager.instance.HasCheckpoint())
        {
            string savedScene = GameSaveManager.instance.GetSavedScene();
            if (savedScene == SceneManager.GetActiveScene().name)
            {
                Vector2 pos = GameSaveManager.instance.LoadCheckpointPosition();
                rb.position = pos;
                Debug.Log($"[Player] Loaded pos from DB: {pos}");
            }
        }
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        if (movement.magnitude > 1) movement.Normalize();

        bool walking = movement.magnitude > 0;
        animator.SetBool("isWalking", walking);
        if (walking)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                animator.SetFloat("Direction", movement.x > 0 ? 3 : 2);
            else
                animator.SetFloat("Direction", movement.y > 0 ? 1 : 0);
        }
    }

    void FixedUpdate()
    {
        Vector2 newPos = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPos);
    }

    public void TeleportTo(Vector2 pos)
    {
        if (rb != null) rb.position = pos;
        else transform.position = pos;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Boundary") ||
            collision.gameObject.CompareTag("Furniture"))
        {
            rb.velocity = Vector2.zero;
        }
    }
}
