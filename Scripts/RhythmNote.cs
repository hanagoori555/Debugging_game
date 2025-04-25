using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    public int laneIndex;            // Номер дорожки (0..7)
    public float duration;           // Длительность ноты (0 для короткой, >0 для длинной)
    public float fallSpeed = 5f;     // Скорость падения ноты

    private SpriteRenderer sr;
    private BoxCollider2D col;

    private bool canBeHit = false;   // Нота находится в зоне попадания (HitZone)
    private bool isHolding = false;  // Игрок удерживает нужную клавишу
    private float holdTime = 0f;     // Счетчик времени удержания
    private bool successfulHit = false; // Флаг, что длинная нота была успешно удержана

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();

        if (sr == null)
        {
            Debug.LogError("SpriteRenderer не найден на ноте!", gameObject);
            return;
        }
        if (col == null)
        {
            Debug.LogError("BoxCollider2D не найден на ноте!", gameObject);
            return;
        }

        // Включаем режим Tiled для растягивания спрайта
        sr.drawMode = SpriteDrawMode.Tiled;
        Debug.Log("SpriteRenderer найден, drawMode установлен в Tiled", gameObject);

        // Устанавливаем высоту спрайта и коллайдера в зависимости от длительности
        SetNoteLength(duration);
    }

    private void Update()
    {
        // Падение ноты вниз
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // Если нота опустилась значительно ниже экрана, считаем её пропущенной
        if (transform.position.y < -10f)
        {
            Debug.LogWarning($"Note missed! Удаляем {gameObject.name} (ниже границы)", gameObject);
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        // Если нота не в зоне, ничего не обрабатываем
        if (!canBeHit) return;

        if (RhythmGameManager.instance != null)
        {
            KeyCode neededKey = RhythmGameManager.instance.laneKeys[laneIndex];

            // Обработка короткой ноты (duration == 0)
            if (duration <= 0f)
            {
                if (Input.GetKeyDown(neededKey))
                {
                    Debug.Log($"Short note hit in lane {laneIndex}!", gameObject);
                    Destroy(gameObject);
                }
            }
            else // Обработка длинной ноты
            {
                if (Input.GetKeyDown(neededKey))
                {
                    isHolding = true;
                    holdTime = 0f;
                    sr.color = Color.green; // визуальный индикатор начала удержания
                    Debug.Log($"Начато удержание длинной ноты в lane {laneIndex}", gameObject);
                }

                if (isHolding && Input.GetKey(neededKey))
                {
                    holdTime += Time.deltaTime;
                    if (holdTime >= duration && !successfulHit)
                    {
                        Debug.Log($"Successfully held note in lane {laneIndex} for {duration} sec!", gameObject);
                        successfulHit = true;
                        Destroy(gameObject);
                    }
                }

                if (Input.GetKeyUp(neededKey))
                {
                    if (isHolding && holdTime < duration)
                    {
                        Debug.LogWarning($"Note missed due to short hold time in lane {laneIndex} (удержано {holdTime:F2} сек)", gameObject);
                        Destroy(gameObject);
                    }
                    isHolding = false;
                    sr.color = Color.white;
                }
            }
        }
    }

    // Устанавливает размер ноты в зависимости от длительности.
    public void SetNoteLength(float duration)
    {
        this.duration = duration;
        // Для коротких нот (duration == 0) задаём минимальную высоту 1f
        float noteHeight = (duration > 0f) ? (duration * 2.5f) : 1f;

        if (sr != null)
        {
            // Устанавливаем новую высоту, оставляя ширину неизменной.
            sr.size = new Vector2(sr.size.x, noteHeight);
            Debug.Log($"Размер спрайта изменён: {sr.size}", gameObject);
        }
        if (col != null)
        {
            col.size = new Vector2(col.size.x, noteHeight);
            col.offset = new Vector2(col.offset.x, noteHeight / 2f);
            Debug.Log($"Размер коллайдера изменён: {col.size}, Offset: {col.offset}", gameObject);
        }
    }

    // Срабатывает, когда нота входит в зону попадания (HitZone должен иметь Is Trigger и тег "HitZone")
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("HitZone"))
        {
            canBeHit = true;
            Debug.Log($"Note entered HitZone ({gameObject.name})", gameObject);
        }
    }

    // Срабатывает, когда нота выходит из зоны попадания
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("HitZone"))
        {
            // Для коротких нот, немедленно сбрасываем возможность попадания и уничтожаем ноту, если не была нажата
            if (duration <= 0f)
            {
                canBeHit = false;
                Debug.Log($"Short note exited HitZone ({gameObject.name})", gameObject);
                Destroy(gameObject);
            }
            else
            {
                // Для длинных нот, если игрок не удерживает клавишу, то засчитываем как пропущенную
                if (!isHolding)
                {
                    canBeHit = false;
                    Debug.LogWarning($"Long note missed due to leaving HitZone too early in lane {laneIndex}!", gameObject);
                    Destroy(gameObject);
                }
                else
                {
                    // Если клавиша удерживается — допускаем продолжение удержания
                    Debug.Log($"Long note in lane {laneIndex} still held despite exiting HitZone", gameObject);
                }
            }
        }
    }
}
