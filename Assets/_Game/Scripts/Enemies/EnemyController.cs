using UnityEngine;
using UnityEngine.AI;

namespace FalseParadise
{
    public enum EnemyState { Idle, Chase, Attack, Kneel, Spared, Dead }

    /// <summary>
    /// Враг: стоит → замечает игрока → преследует → атакует.
    /// На низком здоровье падает на колени: [E] — пощадить, ЛКМ — добить.
    /// NavMeshAgent необязателен: без него враг идёт к игроку напрямую.
    ///
    /// Параметры аниматора (все необязательные): float "Speed", trigger "Attack", trigger "Hit",
    /// bool "Kneeling", bool "Dead", bool "Spared".
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyController : MonoBehaviour, IInteractable
    {
        [Header("Идентичность")]
        [Tooltip("Уникальный id. Для босса 3-го этажа — ogre_chief")]
        [SerializeField] string enemyId = "goblin_01";
        [Tooltip("Ключ реплики Гласа, когда игрок впервые видит врага (можно пусто)")]
        [SerializeField] string narrationKeyOnSeen = "";

        [Header("Пощада")]
        [SerializeField] bool canBeSpared = true;
        [Range(0.05f, 0.9f)] [SerializeField] float kneelAtHealth = 0.3f;

        [Header("Поведение")]
        [SerializeField] float detectRange = 10f;
        [SerializeField] float loseRange = 16f;
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float moveSpeed = 2.8f;
        [SerializeField] float turnSpeed = 540f;
        [SerializeField] float attackDamage = 10f;
        [SerializeField] float attackWindup = 0.4f;
        [SerializeField] float attackCooldown = 1.4f;

        [Header("Иллюзия")]
        [SerializeField] IllusionVisual illusion;
        [Tooltip("Шанс на долю секунды показать настоящий облик при попадании")]
        [Range(0f, 1f)] [SerializeField] float truthFlashChance = 0.25f;
        [SerializeField] float truthFlashDuration = 0.25f;
        [Tooltip("Звук помех при мелькании настоящего облика")]
        [SerializeField] AudioClip glitchClip;

        // Первый удар по первому врагу в игре всегда показывает глитч: «мне показалось?»
        static bool firstGlitchShown;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => firstGlitchShown = false;

        public string EnemyId => enemyId;
        public EnemyState State { get; private set; } = EnemyState.Idle;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int KneelingHash = Animator.StringToHash("Kneeling");
        static readonly int DeadHash = Animator.StringToHash("Dead");
        static readonly int SparedHash = Animator.StringToHash("Spared");

        Health health;
        NavMeshAgent agent;
        Animator[] animators;
        Transform player;
        Health playerHealth;
        bool seenAnnounced;
        float attackTimer;     // > 0: идёт замах
        float nextAttackTime;
        float currentSpeed;

        void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.speed = moveSpeed;
            if (illusion == null) illusion = GetComponentInChildren<IllusionVisual>(true);
            // Аниматоры обеих моделей (иллюзия и правда), чтобы состояние совпадало при переключении Ока.
            animators = GetComponentsInChildren<Animator>(true);

            if (canBeSpared) health.Floor = 1f;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                playerHealth = p.GetComponent<Health>();
            }
        }

        void Update()
        {
            currentSpeed = 0f;

            if (player != null)
            {
                switch (State)
                {
                    case EnemyState.Idle: UpdateIdle(); break;
                    case EnemyState.Chase: UpdateChase(); break;
                    case EnemyState.Attack: UpdateAttack(); break;
                }
            }

            SyncAnimators();
        }

        float DistanceToPlayer() => Vector3.Distance(Flat(transform.position), Flat(player.position));
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        bool PlayerAlive => playerHealth == null || !playerHealth.IsDead;

        void UpdateIdle()
        {
            if (PlayerAlive && DistanceToPlayer() <= detectRange)
            {
                if (!seenAnnounced && !string.IsNullOrEmpty(narrationKeyOnSeen))
                {
                    seenAnnounced = true;
                    GameEvents.RaiseNarrationRequest(narrationKeyOnSeen);
                }
                State = EnemyState.Chase;
            }
        }

        void UpdateChase()
        {
            float dist = DistanceToPlayer();
            if (!PlayerAlive || dist > loseRange)
            {
                StopMoving();
                State = EnemyState.Idle;
                return;
            }
            if (dist <= attackRange)
            {
                StopMoving();
                State = EnemyState.Attack;
                return;
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
                currentSpeed = agent.velocity.magnitude;
            }
            else
            {
                FacePlayer();
                transform.position += transform.forward * (moveSpeed * Time.deltaTime);
                currentSpeed = moveSpeed;
            }
        }

        void UpdateAttack()
        {
            FacePlayer();
            float dist = DistanceToPlayer();

            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f && dist <= attackRange * 1.2f && playerHealth != null)
                    playerHealth.TakeDamage(attackDamage);
                return;
            }

            if (!PlayerAlive || dist > attackRange * 1.2f)
            {
                State = EnemyState.Chase;
                return;
            }

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                attackTimer = attackWindup;
                foreach (var a in animators) AnimatorUtil.SetTrigger(a, AttackHash);
            }
        }

        void FacePlayer()
        {
            Vector3 dir = Flat(player.position) - Flat(transform.position);
            if (dir.sqrMagnitude < 0.0001f) return;
            var target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        /// <summary>Удар игрока. Возвращает true, если удар засчитан (для отклика).</summary>
        public bool ReceivePlayerHit(float damage)
        {
            switch (State)
            {
                case EnemyState.Dead:
                case EnemyState.Spared:
                    return false;
                case EnemyState.Kneel:
                    Finish();
                    return true;
                default:
                    if (State == EnemyState.Idle) State = EnemyState.Chase; // ответный агр
                    health.TakeDamage(damage);
                    return true;
            }
        }

        void OnDamaged(float amount)
        {
            attackTimer = 0f; // удар прерывает замах
            foreach (var a in animators) AnimatorUtil.SetTrigger(a, HitHash);

            if (illusion != null && !EyeOfTruth.Active && (!firstGlitchShown || Random.value < truthFlashChance))
            {
                firstGlitchShown = true;
                illusion.FlashTruth(truthFlashDuration);
                if (glitchClip != null) AudioSource.PlayClipAtPoint(glitchClip, transform.position + Vector3.up);
            }

            if (canBeSpared && State != EnemyState.Kneel && health.Normalized <= kneelAtHealth)
                EnterKneel();
        }

        void EnterKneel()
        {
            State = EnemyState.Kneel;
            health.Invulnerable = true;
            attackTimer = 0f;
            StopMoving();
        }

        // IInteractable: пощада на E, только пока враг на коленях.
        public bool CanInteract => State == EnemyState.Kneel;
        public string Prompt => "[E] Пощадить     [ЛКМ] Добить";
        public void Interact() => Spare();

        public void Spare()
        {
            if (State != EnemyState.Kneel) return;
            State = EnemyState.Spared;
            GameEvents.RaiseEnemySpared(enemyId);
            // TODO (контент): пощажённый уходит, благодарит или остаётся сидеть.
        }

        public void Finish()
        {
            if (State == EnemyState.Dead) return;
            health.Floor = 0f;
            health.Invulnerable = false;
            health.Kill();
        }

        void OnDied()
        {
            State = EnemyState.Dead;
            StopMoving();
            if (agent != null) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            GameEvents.RaiseEnemyKilled(enemyId);
        }

        void SyncAnimators()
        {
            foreach (var a in animators)
            {
                AnimatorUtil.SetFloat(a, SpeedHash, currentSpeed);
                AnimatorUtil.SetBool(a, KneelingHash, State == EnemyState.Kneel);
                AnimatorUtil.SetBool(a, DeadHash, State == EnemyState.Dead);
                AnimatorUtil.SetBool(a, SparedHash, State == EnemyState.Spared);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
