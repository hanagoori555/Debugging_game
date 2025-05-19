// RhythmNote.cs
using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    private int laneIndex;
    private float duration;
    private KeyCode key;
    public float fallSpeed = 5f;

    private SpriteRenderer sr;
    private BoxCollider2D col;

    private bool canBeHit = false;
    private bool isHolding = false;
    private float holdTime = 0f;
    private bool successfulHit = false;

    /// <summary>
    /// Инициализирует ноту перед спавном: задаёт дорожку, длительность и привязанную клавишу.
    /// </summary>
    public void Initialize(int lane, float duration, KeyCode key)
    {
        this.laneIndex = lane;
        this.duration = duration;
        this.key = key;
    }

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();

        if (sr != null)
            sr.drawMode = SpriteDrawMode.Tiled;

        // Устанавливаем масштаб коллайдера и спрайта по длительности
        SetNoteLength(duration);
    }

    private void Update()
    {
        // Падение ноты
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // Если нота упала слишком низко — уничтожаем
        if (transform.position.y < -10f)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (!canBeHit) return;

        // Короткая нота
        if (duration <= 0f)
        {
            if (Input.GetKeyDown(key))
                Destroy(gameObject);
        }
        else // Длинная нота
        {
            if (Input.GetKeyDown(key))
            {
                isHolding = true;
                holdTime = 0f;
                if (sr != null) sr.color = Color.green;
            }

            if (isHolding && Input.GetKey(key))
            {
                holdTime += Time.deltaTime;
                if (holdTime >= duration && !successfulHit)
                {
                    successfulHit = true;
                    Destroy(gameObject);
                }
            }

            if (Input.GetKeyUp(key))
            {
                if (isHolding && holdTime < duration)
                    Destroy(gameObject);
                isHolding = false;
                if (sr != null) sr.color = Color.white;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("HitZone"))
            canBeHit = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("HitZone") && duration <= 0f)
            Destroy(gameObject);
    }

    /// <summary>
    /// Меняет размер и коллайдер ноты в зависимости от её длительности.
    /// </summary>
    private void SetNoteLength(float dur)
    {
        float height = dur > 0f ? dur * 2.5f : 1f;
        if (sr != null)
            sr.size = new Vector2(sr.size.x, height);
        if (col != null)
        {
            col.size = new Vector2(col.size.x, height);
            col.offset = new Vector2(col.offset.x, height / 2f);
        }
    }
}
