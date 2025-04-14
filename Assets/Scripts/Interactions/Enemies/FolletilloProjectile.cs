using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FolletilloProjectile : MonoBehaviour
{
    private Rigidbody2D _rb;
    private int _damage;
    private float _speed;
    private bool _initialized;

    [SerializeField] private float lifetime = 5f; // Tiempo en segundos antes de autodestruirse si no choca

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        // Asegurarse de que el collider sea un Trigger para detectar colisiones sin física de empuje.
        GetComponent<Collider2D>().isTrigger = true;
    }

    /// <summary>
    /// Configura el proyectil después de ser instanciado.
    /// </summary>
    /// <param name="direction">La dirección normalizada en la que viajará el proyectil.</param>
    /// <param name="projectileSpeed">La velocidad del proyectil.</param>
    /// <param name="projectileDamage">El daño que infligirá el proyectil.</param>
    public void Initialize(Vector2 direction, float projectileSpeed, int projectileDamage)
    {
        this._speed = projectileSpeed;
        this._damage = projectileDamage;

        // Orienta el proyectil visualmente si es necesario (asume que el sprite mira hacia la derecha)
        // Si el sprite mira hacia arriba, usa transform.up = direction;
        transform.right = direction;

        // Establece la velocidad
        _rb.linearVelocity = direction.normalized * _speed; // Usamos normalized por seguridad
        _initialized = true;

        // Programa la autodestrucción para evitar proyectiles perdidos infinitamente.
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Ignorar colisiones hasta que el proyectil esté completamente inicializado.
        if (!_initialized) return;

        // Colisión con el Jugador
        if (other.CompareTag("Player"))
        {
            // Debug.Log("Proyectil golpea al jugador");
            // TODO: Instanciar un efecto visual/sonido de impacto aquí.
            // Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            // Intenta obtener el componente que maneja el daño en el objeto golpeado.
            var playerStats = other.GetComponent<PlayerStats>(); // O como se llame tu script de vida/daño del jugador
            if (playerStats != null)
            {
                playerStats.TakeDamage(_damage);
            }
            // Destruir el proyectil al impactar.
            Destroy(gameObject);
        }
        // Colisión con el Escenario (Asegúrate de que el escenario esté en la Layer "Ground")
        else if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
             // Debug.Log("Proyectil choca con escenario");
             // TODO: Instanciar un efecto visual/sonido de impacto aquí.
             // Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);

             // Destruir el proyectil al impactar.
             Destroy(gameObject);
        }
        // Podemos añadir más 'else if' para otros tipos de colisiones (objetos rompibles, etc.)
    }
}