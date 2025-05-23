// RhythmNote.cs
using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    private int laneIndex;
    private float duration;
    private KeyCode key;
    public float fallSpeed = 5f;

    private bool canBeHit = false;
    private bool successfulHit = false;
    private float holdTime = 0f;

    // Для затемнения
    private SpriteRenderer _sr;
    private Color _originalColor;

    /// <summary>Инициализация перед спавном</summary>
    public void Initialize(int lane, float duration, KeyCode key)
    {
        this.laneIndex = lane;
        this.duration = duration;
        this.key = key;

        // визуальная высота
        float h = (duration > 0f) ? duration * 3f : 1f;
        transform.localScale = new Vector3(1f, h, 1f);
    }

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
            _originalColor = _sr.color;
    }

    void Update()
    {
        // Падение ноты
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

        // Короткая нота промах
        if (!successfulHit && duration <= 0f && transform.position.y < -10f)
        {
            RhythmGameManager.instance?.RegisterMiss();
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
                // делаем полупрозрачной
                _sr.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.5f);
        }

        // Удержание
        if (Input.GetKey(key) && canBeHit)
        {
            holdTime += Time.deltaTime;
            if (holdTime >= duration && !successfulHit)
            {
                successfulHit = true;
                // восстанавливаем цвет
                if (_sr != null)
                    _sr.color = _originalColor;
                RhythmGameManager.instance?.RegisterHit();
                Destroy(gameObject);
            }
        }

        // Отпускание раньше срока
        if (Input.GetKeyUp(key))
        {
            if (!successfulHit)
                RhythmGameManager.instance?.RegisterMiss();

            // сбросим цвет
            if (_sr != null)
                _sr.color = _originalColor;

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
        if (c.CompareTag("HitZone") && duration > 0f)
            canBeHit = false;
    }
}
