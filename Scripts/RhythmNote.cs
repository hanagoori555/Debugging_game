using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    private int laneIndex;
    private float duration;
    private KeyCode key;
    public float fallSpeed = 5f;

    private bool canBeHit = false;
    private bool successfulHit = false;
    private bool hasMissed = false;    // новый флаг
    private float holdTime = 0f;

    // Для затемнения
    private SpriteRenderer _sr;
    private Color _originalColor;
    private Color _darkColor;         // цвет для промаха

    /// <summary>
    /// Инициализация перед спавном
    /// </summary>
    public void Initialize(int lane, float duration, KeyCode key)
    {
        this.laneIndex = lane;
        this.duration = duration;
        this.key = key;

        // Визуальная высота: duration>0 => long note
        float longNoteHeightPerSec = 7.3f;
        float h = (duration > 0f) ? duration * longNoteHeightPerSec : 1f;

        // Масштабируем по Y
        transform.localScale = new Vector3(1f, h, 1f);

        // Чтобы «вырасти» вверх, поднимаем ноту на половину дополнительной высоты
        float extra = h - 1f; // Если h==1 => короткая, extra==0
        transform.position += Vector3.up * (extra * 0.5f);
    }

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
            _originalColor = _sr.color;
            _darkColor = new Color(_originalColor.r * 0.3f,
                           _originalColor.g * 0.3f,
                           _originalColor.b * 0.3f,
                           _originalColor.a);
    }

    void Update()
    {
        // Падение
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

        // для короткой ноты: если ушла вниз и ни разу не зафлашена
        if (!successfulHit && !hasMissed && duration <= 0f && transform.position.y < -10f)
        {
            RegisterAndShowMiss();
            Destroy(gameObject);
        }
    }

    void LateUpdate()
    {
        if (!canBeHit || successfulHit) return;

        // --- Короткая нота ---
        if (duration <= 0f)
        {
            if (Input.GetKeyDown(key))
            {
                successfulHit = true;
                RhythmGameManager.instance?.RegisterHit();
                Destroy(gameObject);
            }
            return;
        }

        // --- Длинная нота ---
        // Начало удержания
        if (Input.GetKeyDown(key) && canBeHit)
        {
            holdTime = 0f;
            if (_sr != null)
                _sr.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.5f);
        }

        // Удерживаем
        if (Input.GetKey(key) && canBeHit)
        {
            holdTime += Time.deltaTime;
            if (holdTime >= duration && !successfulHit)
            {
                successfulHit = true;
                if (_sr != null) _sr.color = _originalColor;
                RhythmGameManager.instance?.RegisterHit();
                Destroy(gameObject);
            }
        }

        // если отпустили раньше срока
        if (Input.GetKeyUp(key) && !successfulHit && !hasMissed)
        {
            RegisterAndShowMiss();
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (c.CompareTag("HitZone"))
            canBeHit = true;
    }

    void OnTriggerExit2D(Collider2D c)
    {
        if (!c.CompareTag("HitZone")) return;

        // Если ещё не сбито и ещё не помечено промахом
        if (!successfulHit && !hasMissed)
        {
            hasMissed = true;
            RhythmGameManager.instance?.RegisterMiss();

            // сразу сменить цвет на «тёмный»
            if (_sr != null)
                _sr.color = _darkColor;
        }
    }
        // Вынес обработку «промаха» в метод
    private void RegisterAndShowMiss()
    {
        hasMissed = true;
        RhythmGameManager.instance?.RegisterMiss();
        if (_sr != null)
            _sr.color = _darkColor;
    }
}
