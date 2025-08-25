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
    public AnimatorOverrideController altOverrideController2;

    // --- persistent state between rebinds / model swaps ---
    private float _savedDirection = 0f;
    private bool _hasSavedDirection = false;
    private bool _savedIsWalking = false;
    private bool _hasSavedIsWalking = false;
    private Vector3 _savedPosition = Vector3.zero;
    private bool _hasSavedPosition = false;

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
                int variant = TaskManager.instance.GetModelVariantForTask(task); // 0/1/2
                SetModelVariant(variant);
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

    /// <summary>
    /// Сохранить текущую позицию и параметры аниматора (Direction/isWalking) во временные поля.
    /// Вызвать перед операциями, которые могут перепривязать/пересоздать аниматор или перезаписать позицию.
    /// </summary>
    public void SavePersistentState()
    {
        if (animator != null)
        {
            // Если параметров нет — Get... вернёт 0/false — это ок
            _savedDirection = animator.GetFloat("Direction");
            _hasSavedDirection = true;
            _savedIsWalking = animator.GetBool("isWalking");
            _hasSavedIsWalking = true;
        }
        _savedPosition = transform.position;
        _hasSavedPosition = true;
        Debug.Log($"[PlayerController] SavePersistentState dir={_savedDirection} walking={_savedIsWalking} pos={_savedPosition}");
    }

    /// <summary>
    /// Восстановить ранее сохранённые параметры (если были сохранены).
    /// </summary>
    public void RestorePersistentState()
    {
        if (animator != null)
        {
            if (_hasSavedDirection)
            {
                animator.SetFloat("Direction", _savedDirection);
            }
            if (_hasSavedIsWalking)
            {
                animator.SetBool("isWalking", _savedIsWalking);
            }
        }
        if (_hasSavedPosition)
        {
            if (rb != null) rb.position = _savedPosition;
            else transform.position = _savedPosition;
        }

        _hasSavedDirection = false;
        _hasSavedIsWalking = false;
        _hasSavedPosition = false;
        Debug.Log("[PlayerController] RestorePersistentState applied");
    }


    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Boundary") ||
            collision.gameObject.CompareTag("Furniture"))
        {
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }

    public void SetModelVariant(int variant)
    {
        if (animator == null)
        {
            Debug.LogWarning("[PlayerController] animator == null, cannot switch controller");
            return;
        }

        // Сохраним текущие параметры аниматора, чтобы не потерять направление/позу
        float prevDirection = animator.GetFloat("Direction");
        bool prevIsWalking = animator.GetBool("isWalking");

        switch (variant)
        {
            case 0:
                if (defaultController != null)
                {
                    animator.runtimeAnimatorController = defaultController;
                    Debug.Log("[PlayerController] Applied default controller (variant 0)");
                }
                else
                {
                    Debug.LogWarning("[PlayerController] defaultController is null - cannot restore default model");
                }
                break;

            case 1:
                if (altOverrideController != null)
                {
                    animator.runtimeAnimatorController = altOverrideController;
                    Debug.Log("[PlayerController] Applied alt override controller (variant 1)");
                }
                else
                {
                    Debug.LogWarning("[PlayerController] altOverrideController is null - cannot apply variant 1");
                }
                break;

            case 2:
                if (altOverrideController2 != null)
                {
                    animator.runtimeAnimatorController = altOverrideController2;
                    Debug.Log("[PlayerController] Applied alt override controller 2 (variant 2)");
                }
                else
                {
                    Debug.LogWarning("[PlayerController] altOverrideController2 is null - cannot apply variant 2");
                }
                break;

            default:
                Debug.LogWarning($"[PlayerController] Unknown variant {variant} - keeping current controller");
                break;
        }

        // Rebind сбрасывает состояние аниматора — сразу восстановим нужные параметры
        animator.Rebind();
        animator.Update(0f);

        // Восстанавливаем то, что было (Direction / isWalking)
        animator.SetFloat("Direction", prevDirection);
        animator.SetBool("isWalking", prevIsWalking);
    }


    /// Сохраняем совместимость
    public void UseAltController(bool useAlt)
    {
        SetModelVariant(useAlt ? 1 : 0);
    }
}
