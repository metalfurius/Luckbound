using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))] 
[RequireComponent(typeof(Animator))] 
public class FolletilloLuminoso : BaseDupeEnemy
{
    [Header("Folletillo Settings")]
    [SerializeField] private GameObject projectilePrefab; // Asigna el prefab del proyectil aquí
    [SerializeField] private Transform projectileSpawnPoint; // Punto de origen del proyectil
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private int projectileDamage = 10;
    [SerializeField] private float attackRange = 8f; // Distancia para empezar a atacar
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private int hitsToExplode = 3; // Golpes necesarios para la explosión
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private int explosionDamage = 50;
    [SerializeField] private float chaseSpeed = 3f; // Velocidad al perseguir al jugador

    // --- State Variables ---
    private float _lastAttackTime = -Mathf.Infinity;
    private int _hitsTaken;
    private bool _isExploding;

    // --- Light Stealing ---
    // TODO: Referencia real al sistema de luz del jugador.
    // private PlayerLightController playerLight; // Ejemplo de nombre de script
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _hasStolenLight; // Flag para saber si tiene la luz robada
#pragma warning restore CS0414 // Field is assigned but its value is never used

    // --- Component References ---
    private Animator _animator;

    private static readonly int Inflate = Animator.StringToHash("Inflate");

    private static readonly int Explode1 = Animator.StringToHash("Explode");

    private static readonly int Hit = Animator.StringToHash("Hit");

    private static readonly int Die1 = Animator.StringToHash("Die");

    private static readonly int IsHoldingLight = Animator.StringToHash("IsHoldingLight");

    public void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public override void Start()
    {
        base.Start(); // Ejecuta el Start de Enemy y BaseDupeEnemy

        // Configuración específica del Folletillo
        if (rb != null)
        {
            rb.gravityScale = 0; // Criatura voladora
        }
        else
        {
            Debug.LogError("Rigidbody2D no encontrado en Folletillo Luminoso!", this);
        }

        // Validaciones de configuración esencial
        if (!projectilePrefab)
            Debug.LogError("Projectile Prefab no asignado en Folletillo Luminoso!", this);
        if (!projectileSpawnPoint)
        {
            Debug.LogWarning("Projectile Spawn Point no asignado, usando la posición del Folletillo como fallback.", this);
            projectileSpawnPoint = transform;
        }

        // TODO: Encontrar la referencia al controlador de luz del jugador al inicio.
        // Ejemplo: Buscarlo en el objeto del jugador si playerTarget está disponible.
        // if (playerTarget != null) playerLight = playerTarget.GetComponentInChildren<PlayerLightController>();
        // if (playerLight == null) Debug.LogWarning("No se encontró PlayerLightController en el jugador.");

        // Estado inicial (puede ser Idle o Patrol)
        currentState = EnemyState.Patrol;
    }

    // --- State Handling Overrides ---

    protected override void HandleIdleState()
    {
        // Flotar en el sitio o esperar.
        rb.linearVelocity = Vector2.zero;
        // Podría buscar al jugador periódicamente y cambiar a Chase si lo detecta.
        // CheckForPlayer();
    }

    protected override void HandlePatrolState()
    {
        // Lógica de patrulla. Por ahora, se queda quieto.
        rb.linearVelocity = Vector2.zero;

        // Siempre busca al jugador mientras patrulla
        CheckForPlayer(); // Suponiendo que CheckForPlayer cambia a Chase si lo encuentra
    }

    protected override void HandleChaseState()
    {
        if (!playerTarget || _isExploding)
        {
            // Si pierde al jugador o está explotando, podría volver a patrullar o quedarse idle.
            currentState = EnemyState.Patrol;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Moverse hacia el jugador
        Vector2 direction = (playerTarget.position - transform.position).normalized;
        rb.linearVelocity = direction * chaseSpeed;

        // Opcional: Rotar para mirar al jugador (si el sprite lo requiere)

        // Comprobar si está en rango de ataque
        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (distanceToPlayer <= attackRange)
        {
            currentState = EnemyState.Attack;
            rb.linearVelocity = Vector2.zero; // Detenerse al preparar el ataque
        }
    }

    protected override void HandleAttackState()
    {
        if (!playerTarget || _isExploding)
        {
            currentState = EnemyState.Patrol; // Volver a patrullar si no hay objetivo o está explotando
            return;
        }

        // Comprobar si el jugador se ha alejado demasiado
        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);
        if (distanceToPlayer > attackRange)
        {
            currentState = EnemyState.Chase;
            return;
        }

        // Opcional: Mantenerse mirando al jugador
        // Vector2 direction = (playerTarget.position - transform.position).normalized;
        // float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        // Comprobar cooldown y atacar
        if (Time.time >= _lastAttackTime + attackCooldown)
        {
            Attack();
            _lastAttackTime = Time.time;
        }
    }

    protected override void HandleFleeState()
    {
        // Este enemigo no huye según la descripción.
        // Si cambiara de opinión, aquí iría la lógica para alejarse.
        // Por ahora, simplemente volvemos a un estado más lógico.
        currentState = EnemyState.Patrol;
    }

    protected override void HandleDieState()
    {
        // Este estado principalmente detiene otras lógicas mientras se ejecuta la animación/efecto de muerte/explosión.
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // Evita que siga siendo afectado por la física
        GetComponent<Collider2D>().enabled = false; // Desactiva colisiones
    }

    // --- Core Logic Methods ---

    protected virtual void Attack()
    {
        if (!projectilePrefab || !playerTarget || _isExploding) return;

        // 1. Activar la animación de "inflarse"
        _animator?.SetTrigger(Inflate); // Asegúrate de tener este Trigger en tu Animator Controller

        // 2. Calcular dirección hacia el jugador en el momento del disparo
        Vector2 direction = (playerTarget.position - projectileSpawnPoint.position).normalized;

        // 3. Instanciar el proyectil
        // Usamos la rotación del spawn point por si está orientado de alguna forma.
        var projectileGo = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);

        // 4. Configurar el proyectil usando su script
        var projectileScript = projectileGo.GetComponent<FolletilloProjectile>();
        if (projectileScript)
        {
            projectileScript.Initialize(direction, projectileSpeed, projectileDamage);
        }
        else
        {
            Debug.LogError("El prefab del proyectil no tiene el script FolletilloProjectile!", projectileGo);
            // Fallback por si acaso, aunque Initialize es mejor
            Rigidbody2D projectileRb = projectileGo.GetComponent<Rigidbody2D>();
            if (projectileRb) projectileRb.linearVelocity = direction * projectileSpeed;
            Destroy(projectileGo, 5f); // Autodestrucción básica si no hay script
        }

        // 5. TODO: Intentar robar la luz con cada ataque (o solo al entrar en rango)
        // TryStealLight();

        // Nota: La animación "Inflate" debería volver al estado normal después de un tiempo
        // o tener un evento al final que permita volver a moverse/atacar.
        // O usar una Coroutine aquí para esperar a que termine la animación antes de disparar.
    }

    public override void TakeDamage(int amount)
    {
        if (_isExploding || currentState == EnemyState.Die) return; // Inmune si ya está muriendo/explotando

        // Aplicar resistencia (si existe en la clase base o aquí)
        var actualDamage = amount - resistance; // 'resistance' viene de BaseDupeEnemy?
        if (actualDamage <= 0) actualDamage = 1; // Asegurar que cada golpe cuente

        // Aplicar daño (usando el randomizador si es parte de tu sistema)
        health -= Randomizer.GetRandomizedInt(actualDamage); // Asume que Randomizer existe
        // Debug.Log($"{gameObject.name} recibió daño. Vida restante: {health}");

        // TODO: Feedback visual/sonoro de recibir daño (flash de color, sonido de impacto)
        _animator?.SetTrigger(Hit); // Ejemplo de trigger para feedback

        // Incrementar contador de golpes
        _hitsTaken++;
        // Debug.Log($"{gameObject.name} ha recibido {_hitsTaken}/{hitsToExplode} golpes.");

        // Comprobar condiciones de muerte/explosión
        if (_hitsTaken >= hitsToExplode)
        {
            // Debug.Log($"{gameObject.name} ha recibido suficientes golpes y va a explotar!");
            Explode();
        }
        else if (health <= 0)
        {
            // Debug.Log($"{gameObject.name} se quedó sin vida y muere.");
            Die(); // Muerte normal si no explota por golpes
        }
    }

    protected virtual void Explode()
    {
        if (_isExploding) return; // Prevenir llamadas múltiples
        _isExploding = true;
        currentState = EnemyState.Die; // La explosión es su forma de morir

        // Debug.Log($"{gameObject.name} EXPLOTA!");

        // 1. Activar animación/efecto visual/sonido de explosión
        _animator?.SetTrigger(Explode1); // Asegúrate de tener este Trigger

        // 2. Devolver la luz si la había robado
        RestorePlayerLight();

        // 3. Aplicar daño en área
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                Damageable playerDamageable = hitCollider.GetComponent<Damageable>();
                if (playerDamageable != null)
                {
                    // Debug.Log($"Explosión golpea al jugador con {explosionDamage} de daño.");
                    playerDamageable.TakeDamage(explosionDamage); // Aplicar daño de explosión
                }
                // break; // Descomenta si solo quieres dañar al jugador una vez por explosión
            }
            // TODO: Podrías añadir lógica para dañar otros enemigos o objetos rompibles aquí
        }

        // 4. Destruir el objeto. Es mejor hacerlo después de un pequeño delay
        // para dar tiempo a que la animación/efecto de explosión se vea.
        // Puedes usar una Coroutine o simplemente Destroy con delay.
        Destroy(gameObject, 1.0f); // Ajusta el delay según tu animación/efecto
    }

    protected override void Die()
    {
        // Esta función se llama si muere por perder toda la vida ANTES de explotar por golpes.
        if (_isExploding || currentState == EnemyState.Die) return; // Evitar ejecución si ya está muriendo/explotando

        // Debug.Log($"{gameObject.name} muere de forma normal.");
        currentState = EnemyState.Die;

        // 1. Activar animación/efecto de muerte normal (diferente de la explosión)
        _animator?.SetTrigger(Die1); // Asegúrate de tener este Trigger

        // 2. Devolver la luz si la había robado
        RestorePlayerLight();

        // 3. Destruir el objeto después de un delay para la animación/efecto
        Destroy(gameObject, 1.0f); // Ajusta el delay
    }

    // --- Light Stealing Logic (Placeholders) ---

    protected virtual void TryStealLight()
    {
        // TODO: Implementar la lógica real basada en tu sistema de luz del jugador.
        // Necesitas detectar si el jugador está usando una fuente de luz cerca.
        /* Ejemplo Conceptual:
        if (!_hasStolenLight && playerLight != null && playerLight.IsLightActive()) // Necesitas métodos en tu PlayerLightController
        {
            // Podrías añadir una comprobación de distancia adicional si es necesario
            // if (Vector2.Distance(transform.position, playerTarget.position) < lightStealRange)
            //{
                Debug.Log("Folletillo intenta robar la luz!");
                bool stolen = playerLight.DisableLightSource(); // Método que devuelve true si la desactiva con éxito
                if (stolen)
                {
                    _hasStolenLight = true;
                    // TODO: Añadir feedback visual al Folletillo (¿brilla más?)
                    _animator?.SetBool("IsHoldingLight", true); // Ejemplo con Animator
                }
            //}
        }
        */
    }

    protected virtual void RestorePlayerLight()
    {
        // TODO: Implementar la lógica real para devolver la luz.
        /* Ejemplo Conceptual:
        if (_hasStolenLight && playerLight != null)
        {
            Debug.Log("Folletillo devuelve la luz.");
            playerLight.EnableLightSource(); // Método para reactivar la luz del jugador
            _hasStolenLight = false;
             // TODO: Quitar feedback visual del Folletillo
            _animator?.SetBool("IsHoldingLight", false); // Ejemplo con Animator
        }
        */
        // Asegurarse de resetear el flag incluso si la luz ya no existe o no se pudo devolver.
       _hasStolenLight = false;
       _animator?.SetBool(IsHoldingLight, false); // Resetear estado visual
    }

    // --- Collision Logic ---
    // BaseDupeEnemy probablemente maneja el daño por contacto en OnCollisionEnter2D.
    // Si NO queremos que este enemigo haga daño por contacto, descomentar lo siguiente:
    /*
    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        // Sobrescribir para evitar el daño por contacto de la clase base.
        // Añadir lógica aquí si queremos que la colisión haga otra cosa (rebotar, etc.)
    }
    */
    protected virtual void CheckForPlayer()
    {
        // Lógica para detectar al jugador dentro de un rango de visión/aggro
        // Si lo detecta y no está ya persiguiendo/atacando, cambia a currentState = EnemyState.Chase;
        // Esto dependerá de cómo tu BaseDupeEnemy maneja la detección inicial.
        // Si BaseDupeEnemy ya lo hace (p.ej., usando un trigger collider), este método puede no ser necesario aquí.
    }
}