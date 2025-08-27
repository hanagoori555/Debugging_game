using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    private int laneIndex;
    private float duration;
    private KeyCode key;
    public float fallSpeed = 5f;

    private bool canBeHit = false;
    private bool successfulHit = false;
    private bool _handled = false;      // единственный флаг для предотвращения дублей
    private bool isHolding = false;     // удержание для long notes
    private float holdTime = 0f;

    // Для затемнения
    private SpriteRenderer _sr;
    private Color _originalColor;
    private Color _darkColor;         // цвет для промаха

    // Настройка "высоты" для long note
    private const float longNoteHeightPerSec = 7.3f;

    /// <summary>
    /// Инициализация перед спавном
    /// </summary>
    public void Initialize(int lane, float duration, KeyCode key)
    {
        this.laneIndex = lane;
        this.duration = duration;
        this.key = key;

        // Визуальная высота: duration>0 => long note
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
        {
            _originalColor = _sr.color;
            _darkColor = new Color(_originalColor.r * 0.3f,
                                   _originalColor.g * 0.3f,
                                   _originalColor.b * 0.3f,
                                   _originalColor.a);
        }
    }

    void Update()
    {
        if (_handled) return; // если уже обработано — игнорируем всё

        // Падение
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

        // для короткой ноты: если ушла вниз и ни разу не зафлашена
        if (!successfulHit && !canBeHit && duration <= 0f && transform.position.y < -10f)
        {
            // если нота ушла ниже экрана и не была в зоне — считаем промахом (один раз)
            RegisterAndShowMiss();
            return;
        }

        // Обработка ввода (примитивно — опрашиваем Input здесь)
        if (duration <= 0f)
        {
            // короткая нота: реагируем на одножатие, только если сейчас в зоне
            if (canBeHit && Input.GetKeyDown(key))
            {
                RegisterAndShowHit();
            }
            return;
        }

        // --- Длинная нота ---
        // Начало удержания
        if (canBeHit && Input.GetKeyDown(key) && !_handled)
        {
            isHolding = true;
            holdTime = 0f;
            if (_sr != null)
                _sr.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.5f);
        }

        // Удерживаем
        if (isHolding && Input.GetKey(key) && !_handled)
        {
            holdTime += Time.deltaTime;
            if (holdTime >= duration && !successfulHit)
            {
                RegisterAndShowHit();
            }
        }

        // если отпустили раньше срока — промах (если мы были в зоне)
        if (isHolding && Input.GetKeyUp(key) && !_handled)
        {
            isHolding = false;
            // если время удержания было недостаточным — промах
            if (!successfulHit)
                RegisterAndShowMiss();
        }

        // Edge: если нота вышла из зоны во время удержания — считаем промах (OnTriggerExit также это сделает,
        // но OnTriggerExit сработает раньше/позже в зависимости от физики — _handled защитит от дублей)
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (_handled) return;
        if (c.CompareTag("HitZone"))
            canBeHit = true;
    }

    void OnTriggerExit2D(Collider2D c)
    {
        if (_handled) return;
        if (!c.CompareTag("HitZone")) return;

        // Если ещё не сбито и ещё не помечено промахом -> регистрируем промах
        if (!successfulHit)
        {
            RegisterAndShowMiss();
        }
    }

    private void RegisterAndShowHit()
    {
        if (_handled) return;
        _handled = true;
        successfulHit = true;

        RhythmGameManager.instance?.RegisterHit();

        // Визуальный откат/эффект: вернём оригинальный цвет если был затемнён
        if (_sr != null)
            _sr.color = _originalColor;

        // Отключаем коллайдер, чтобы OnTriggerExit не сработал дополнительно
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // можно проиграть звук/эффект здесь

        Destroy(gameObject, 0.05f); // небольшая задержка чтобы успели звуки/эффекты
    }

    private void RegisterAndShowMiss()
    {
        if (_handled) return;
        _handled = true;

        RhythmGameManager.instance?.RegisterMiss();

        if (_sr != null)
            _sr.color = _darkColor;

        // Отключим коллайдер
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Если удержание было — прекратим
        isHolding = false;

        Destroy(gameObject, 0.05f);
    }
}
