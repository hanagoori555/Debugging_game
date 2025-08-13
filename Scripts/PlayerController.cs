using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance;

    [Header("Movement")]
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 movement;
    private Animator animator;
    public static bool InputBlocked = false;

    [Header("Animator / Model variants")]
    // Если не заполнено в инспекторе, defaultController заполняется из animator.runtimeAnimatorController на Start()
    public RuntimeAnimatorController defaultController;
    public AnimatorOverrideController altOverrideController;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("[PlayerController] Animator not found on player!");
        }
        else
        {
            // если defaultController не прописан вручную — возьмём текущий runtime controller
            if (defaultController == null)
            {
                defaultController = animator.runtimeAnimatorController;
                Debug.Log("[PlayerController] defaultController auto-assigned from animator.runtimeAnimatorController");
            }

            // При старте проверим TaskManager и применим нужную модель (на случай Continue/перехода)
            if (TaskManager.instance != null)
            {
                var task = TaskManager.instance.GetCurrentTaskData();
                bool useAlt = TaskManager.instance.ShouldUseAltModel(task);
                UseAltController(useAlt);
            }
        }

        // ---- Teleport logic: если есть TaskManager — он сам управляет спавном/телепортом.
        // ---- Если TaskManager отсутствует (напр. тестовая сцена), делаем локальный fallback:
        if (TaskManager.instance == null)
        {
            bool teleportedByCheckpoint = false;

            // 1) Попробуем чекпоинт (если он есть)
            if (GameSaveManager.instance != null && GameSaveManager.instance.HasCheckpoint())
            {
                string savedScene = GameSaveManager.instance.GetSavedScene();
                if (savedScene == SceneManager.GetActiveScene().name)
                {
                    Vector2 pos = GameSaveManager.instance.LoadCheckpointPosition();
                    if (rb != null) rb.position = pos;
                    else transform.position = pos;
                    Debug.Log($"[Player] (fallback) loaded pos from DB: {pos}");
                    teleportedByCheckpoint = true;
                }
            }

            // 2) Если чекпоинта нет — используем SpawnPoint по тегу (если есть)
            if (!teleportedByCheckpoint)
            {
                var spGo = GameObject.FindWithTag("SpawnPoint");
                if (spGo != null)
                {
                    if (rb != null) rb.position = spGo.transform.position;
                    else transform.position = spGo.transform.position;
                    Debug.Log($"[Player] (fallback) using scene SpawnPoint tag at {spGo.transform.position}");
                }
                else
                {
                    Debug.LogWarning("[Player] (fallback) no SpawnPoint tag and no TaskManager — player position left as in scene");
                }
            }
        }
        else
        {
            // Есть TaskManager — он сам переместит игрока в OnSceneLoaded_MovePlayer.
            // Если TaskManager ранее отложил спавн (при SceneExit), применим его прямо сейчас.
            Debug.Log("[Player] TaskManager present -> deferring spawn/teleport to TaskManager");
            TaskManager.instance.TryApplyDeferredSpawnToPlayer(this);
        }
    }

    void Update()
    {
        if (InputBlocked)
        {
            // гасим остаточное движение и анимацию
            if (rb != null) rb.velocity = Vector2.zero;
            movement = Vector2.zero;
            if (animator != null) animator.SetBool("isWalking", false);
            return;
        }

        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        if (movement.magnitude > 1) movement.Normalize();

        bool walking = movement.magnitude > 0;
        if (animator != null) animator.SetBool("isWalking", walking);
        if (walking && animator != null)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                animator.SetFloat("Direction", movement.x > 0 ? 3 : 2);
            else
                animator.SetFloat("Direction", movement.y > 0 ? 1 : 0);
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;
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
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }

    /// <summary>
    /// Переключает аниматор на override (альт) или на дефолт.
    /// Вызывать каждый раз, когда нужно сменить визуалку.
    /// </summary>
    public void UseAltController(bool useAlt)
    {
        if (animator == null)
        {
            Debug.LogWarning("[PlayerController] animator == null, cannot switch controller");
            return;
        }

        if (useAlt)
        {
            if (altOverrideController != null)
            {
                animator.runtimeAnimatorController = altOverrideController;
                Debug.Log("[PlayerController] Applied alt override controller");
            }
            else
            {
                Debug.LogWarning("[PlayerController] altOverrideController is null - cannot apply alt model");
            }
        }
        else
        {
            if (defaultController != null)
            {
                animator.runtimeAnimatorController = defaultController;
                Debug.Log("[PlayerController] Restored default controller");
            }
            else
            {
                Debug.LogWarning("[PlayerController] defaultController is null - cannot restore default model");
            }
        }

        // Принудительно "перепривяжем" аниматор, чтобы изменения вступили в силу немедленно
        animator.Rebind();
        animator.Update(0f);
    }
}
