using UnityEngine;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    [System.Serializable]
    public class AttackData
    {
        public int damage;
        public string animationTrigger;
        public AnimationClip animationClip; 
    }

    [Header("Combo Settings")]
    [SerializeField] private AttackData[] comboAttacks = new AttackData[3];
    [SerializeField] private float comboWindow = 0.5f; // Ventana de tiempo DESPUÉS de iniciar un ataque para registrar el siguiente input del combo

    private Animator _animator; 
    private PlayerMovement _playerMovement;

    // --- Variables de Estado ---
    private PlayerInput _playerInput;
    private bool _isAttacking;
    private int _currentAttackIndex;
    private float _lastAttackEndTime; // Momento en que terminó la animación del último ataque
    private bool _inputBuffered; // Indica si se presionó ataque durante la ventana de combo
    private Coroutine _attackCoroutine;

    // --- Control Externo ---
    public bool IsAttacking => _isAttacking;

    private void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _playerInput = GetComponent<PlayerInput>();
        _animator = transform.Find("Sprite").GetComponent<Animator>();
        if (comboAttacks.Length == 0)
        {
             Debug.LogError("El array comboAttacks está vacío. No se podrán realizar ataques.", this);
        }
    }

    private void Update()
    {
        if (!CanPerformAttack() || comboAttacks.Length == 0)
        {
            _inputBuffered = false; // Limpia el buffer si no se puede atacar
            return;
        }

        var attackInputPressed = _playerInput.AttackInput; // Lee el input una vez por frame

        // Si se presiona el botón de ataque
        if (attackInputPressed)
        {
            // Si no estamos atacando actualmente, inicia el primer ataque del combo
            if (!_isAttacking)
            {
                StartAttack(0); // Inicia siempre con el primer ataque
            }
            // Si ya estamos atacando, comprueba si estamos dentro de la ventana para hacer buffer
            else
            {
                // 2. Solo hacer buffer DENTRO del combo actual
                // Comprobamos si el tiempo desde que TERMINÓ el último ataque es menor que la ventana
                // O, si es el primer ataque, usamos una lógica similar (aunque StartAttack ya maneja el inicio)
                // La lógica clave es que el buffer SOLO se activa si ya hay un ataque en curso.
                // El buffer se consumirá en OnAttackFinished.
                _inputBuffered = true; // Marca que se presionó durante un ataque activo
            }
        }

        // --- Reset del combo si pasa demasiado tiempo ---
        // Si no estamos atacando y ha pasado un tiempo desde el último ataque, resetea el índice del combo
        // Esto evita que si terminas un combo y esperas mucho, el siguiente ataque continúe donde lo dejaste.
        if (!_isAttacking && _currentAttackIndex > 0 && Time.time > _lastAttackEndTime + comboWindow) // Puedes ajustar este tiempo de reset
        {
            _currentAttackIndex = 0;
            // Debug.Log("Combo index reset due to timeout.");
        }
    }
    
    private bool CanPerformAttack()
    {
        return !_playerMovement.IsSliding;
    }

    private void StartAttack(int index)
    {
        // Comprobación de seguridad por si el índice está fuera de rango (aunque la lógica debería prevenirlo)
        if (index < 0 || index >= comboAttacks.Length)
        {
            Debug.LogWarning($"Índice de ataque ({index}) fuera de rango. Reiniciando a 0.");
            index = 0;
        }

        _currentAttackIndex = index; // Actualiza el índice actual
        AttackData currentAttack = comboAttacks[_currentAttackIndex];

        // Verifica si el AnimationClip está asignado
        if (!currentAttack.animationClip)
        {
            Debug.LogError($"AnimationClip no asignado para el ataque en el índice {index}. Asigna el clip en el Inspector.", this);
            // Decide qué hacer aquí: ¿cancelar el ataque? ¿Usar una animación por defecto?
            // Por ahora, simplemente no haremos nada para evitar errores mayores.
             _isAttacking = false; // Asegura que no quede bloqueado
            return;
        }

        // Verifica si el trigger de animación es válido
        if (string.IsNullOrEmpty(currentAttack.animationTrigger))
        {
             Debug.LogError($"Animation Trigger no asignado para el ataque en el índice {index}.", this);
             _isAttacking = false;
             return;
        }

        // Debug.Log($"Starting Attack: {index} - Trigger: {currentAttack.animationTrigger}");

        _animator.SetTrigger(currentAttack.animationTrigger);
        _isAttacking = true;
        _inputBuffered = false; // Resetea el buffer al iniciar un nuevo ataque

        // Detiene la coroutine anterior si existiera
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
        }
        // Inicia la coroutine que espera a que termine la animación
        _attackCoroutine = StartCoroutine(AttackTimer(currentAttack.animationClip.length));
    }

    private IEnumerator AttackTimer(float duration)
    {
        // Espera la duración de la animación
        yield return new WaitForSeconds(duration);
        // Llama a la función que maneja el fin del ataque
        OnAttackFinished();
    }

    private void OnAttackFinished()
    {
        _lastAttackEndTime = Time.time; // Guarda cuándo terminó este ataque
        _isAttacking = false; // Ya no estamos en la animación de ataque

        // Lógica de combo:
        // Si se presionó el botón de ataque (inputBuffered es true) durante la animación que acaba de terminar...
        if (_inputBuffered)
        {
            _inputBuffered = false; // Consume el buffer
            int nextAttackIndex = _currentAttackIndex + 1;

            // Si hay un siguiente ataque en el combo, ejecútalo
            if (nextAttackIndex < comboAttacks.Length)
            {
                // Debug.Log("Buffered input detected, continuing combo.");
                StartAttack(nextAttackIndex);
            }
            // Si era el último ataque del combo, simplemente termina. El próximo input iniciará un nuevo combo desde 0.
            else
            {
                // Debug.Log("Buffered input detected, but combo finished. Resetting index.");
                _currentAttackIndex = 0; // Resetea para el próximo inicio
            }
        }
        // Si no hubo buffer, el combo se interrumpe (o termina) aquí.
        else
        {
            // Debug.Log("No buffered input. Combo sequence ended/broken.");
            _currentAttackIndex = 0; // Resetea el índice para que el próximo ataque empiece desde el principio
        }

        _attackCoroutine = null; // Limpia la referencia a la coroutine
    }

    // --- Método público para detener ataques ---
    public void ForceStopAttack()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        _isAttacking = false;
        _inputBuffered = false;
        _currentAttackIndex = 0;
        // Podrías querer resetear también el trigger del Animator o forzar un estado Idle
        // _animator.ResetTrigger(...); // Necesitarías saber qué triggers resetear
        // _animator.Play("IdleStateName"); // O forzar un estado
        Debug.Log("Attack forced stop.");
    }
}