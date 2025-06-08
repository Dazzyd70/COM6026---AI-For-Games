using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Alerted, Chase, Attack, SeekCover, Peeking, Flee, Dead, Dodge, Search, Panic, Heal, MeleeAttack }
    public State currentState = State.Patrol;

    public enum Personality { Assault, Sniper, Shotgunner, Grenadier, Berserker, Flanker, Medic }
    public Personality personality = Personality.Assault;

    [Header("General Settings")]
    public float health = 100f;
    public float fleeHealthThreshold = 51f;
    public float maxHealth = 100f;

    [Header("Vision Settings")]
    public float visionAngle = 100f;
    public float visionDistance = 20f;

    [Header("Squad Settings")]
    public int squadID = -1;
    public bool isSquadLeader = false;
    public float regroupDist = 12f;

    [Header("Memory/Search Settings")]
    public float forgetPlayerDelay = 4f;
    private Vector3 lastSeenPosition;
    private float timeSinceLastSeen = 999f;
    private bool playerWasSeenRecently = false;
    private float patrolTimer = 0f;
    private float patrolResetTime = 5f;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform shootPoint;
    private float lastShootTime = -999f;
    public float aimSpread = 1.5f;
    public int bulletDamage = 10;
    public float bulletSpeed = 12f;
    public float shootCooldown = 0.6f;
    public float shootingRange = 10f;
    public int numPellets = 1;

    // Assault burst logic
    private int burstBullets = 0;
    private int burstMax = 0;
    private float burstPauseTime = 0f;

    // Sniper relocation logic
    private int sniperShotsLeft = 0;
    private float sniperRelocateCooldown = 3f;
    private float sniperRelocateTimer = 0f;

    // Panic logic
    private float panicEndTime = -1f;
    private bool forcedPanic = false;
    private float panicMoveTimer = 0f;
    private float panicMoveInterval = 0f;
    private Vector3 panicTarget;


    // Cover system
    private bool inCover = false;
    private bool isPeeking = false;
    private float peekTime = 1.2f;
    private float peekTimer = 0f;
    private Transform currentCover = null;

    // Shotgunner zig-zag
    private float shotgunZigzagTimer = 0f;
    private float shotgunZigzagDir = 1f;

    // Grenadier logic
    public GameObject grenadePrefab;
    private float nextGrenadeTime = 0f;
    public float grenadeCooldown = 5f;
    private float grenadierSkittishCooldown = 0f;

    // Flanker logic
    private float flankerOrbitTimer = 0f;
    private int flankerOrbitDir = 1;

    // Berserker melee
    public float meleeRange = 2.1f;
    public float meleeDamage = 32f;
    public float berserkSpeed = 7.4f;
    private bool berserked = false;
    private float nextMeleeTime = 0f;
    public float meleeCooldown = 0.8f;
    private int meleeHits = 0;
    private int maxMeleeHits = 2;
    private float berserkerRetreatTime = 2.5f;
    private float berserkerRetreatTimer = 0f;
    private bool isRetreating = false;


    // Medic healing
    public float healAmount = 17f;
    public float healInterval = 2.6f;
    public float healRange = 8.5f;
    private float nextHealTime = 0f;
    private EnemyAI medicHealTarget = null;
    private float healPulseEndTime = 0f;
    private Transform healTarget = null;
    public GameObject medicHealPulsePrefab;

    // Other AI parameters
    private bool leadShots = false;
    private bool allowDodge = false;
    private bool groupUp = false;
    private bool attackWhileMoving = true;
    private bool seekCoverOften = false;
    private bool isDying = false;
    private Vector3 lastHeatPos;

    [Header("References")]
    public Transform player;
    public NavMeshAgent agent;
    private Vector3 patrolPoint;
    private List<Transform> coverPoints;
    public static List<EnemyAI> allEnemies = new List<EnemyAI>();

    // Dodge
    private float lastDodgeTime = -999f;
    private float dodgeCooldown = 3f;

    // Visual feedback
    Renderer rend;
    private GameObject stateTextObj;
    private TextMesh stateText;

    private EnemyAI nearestMedic = null;
    private Coroutine flashCoroutine;
    private bool isFlashing = false;


    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        allEnemies.Add(this);
        rend = GetComponentInChildren<Renderer>();
        SetupStateText();
    }

    void Start()
    {
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }
        FindAllCoverPoints();
        SetRandomPatrolPoint();
        lastSeenPosition = transform.position;

        switch (personality)
        {
            case Personality.Sniper:
                maxHealth = 125f;
                shootCooldown = 1.9f;
                shootingRange = 23f;
                bulletDamage = 20;
                bulletSpeed = 15f;
                aimSpread = 0.08f;
                numPellets = 1;
                leadShots = true;
                attackWhileMoving = false;
                agent.speed = 2f;
                seekCoverOften = false;
                sniperShotsLeft = Random.Range(2, 4);
                visionAngle = 80f;
                visionDistance = 26f;
                break;
            case Personality.Shotgunner:
                maxHealth = 100f;
                shootCooldown = 1.25f;
                shootingRange = 6f;
                bulletDamage = 7;
                bulletSpeed = 9f;
                aimSpread = 14f;
                numPellets = 7;
                leadShots = false;
                attackWhileMoving = true;
                agent.speed = 5.3f;
                seekCoverOften = false;
                visionAngle = 110f;
                visionDistance = 11f;
                break;
            case Personality.Grenadier:
                maxHealth = 150f;
                shootCooldown = 2.5f;
                shootingRange = 14f;
                bulletDamage = 6;
                bulletSpeed = 11f;
                aimSpread = 9f;
                numPellets = 1;
                attackWhileMoving = false;
                agent.speed = 3f;
                visionAngle = 100f;
                visionDistance = 16f;
                break;
            case Personality.Assault:
                maxHealth = 100f;
                shootCooldown = 0.23f;
                shootingRange = 14f;
                bulletDamage = 10;
                bulletSpeed = 10f;
                aimSpread = 6f;
                numPellets = 1;
                leadShots = false;
                attackWhileMoving = false;
                allowDodge = true;
                groupUp = true;
                seekCoverOften = true;
                agent.speed = 3.2f;
                burstBullets = 0;
                burstPauseTime = 0f;
                visionAngle = 100f;
                visionDistance = 18f;
                break;
            case Personality.Berserker:
                maxHealth = 75f;
                shootCooldown = 0f;
                shootingRange = 0f;
                bulletDamage = 0;
                meleeRange = 1f;
                meleeDamage = 15f;
                allowDodge = false;
                seekCoverOften = false;
                groupUp = false;
                agent.speed = 6.3f;
                berserkSpeed = 7.4f;
                meleeCooldown = 0.8f;
                break;
            case Personality.Flanker:
                maxHealth = 80f;
                shootCooldown = 0.43f;
                shootingRange = 12f;
                bulletDamage = 9;
                bulletSpeed = 11f;
                aimSpread = 4.5f;
                numPellets = 1;
                allowDodge = true;
                attackWhileMoving = true;
                groupUp = false;
                seekCoverOften = false;
                agent.speed = 4.5f;
                flankerOrbitDir = Random.value > 0.5f ? 1 : -1;
                break;
            case Personality.Medic:
                maxHealth = 65f;
                shootCooldown = 1.8f;
                shootingRange = 0f;
                bulletDamage = 0;
                aimSpread = 0f;
                bulletSpeed = 0f;
                numPellets = 0;
                allowDodge = false;
                attackWhileMoving = false;
                groupUp = false;
                seekCoverOften = true;
                agent.speed = 6f;
                healAmount = 17f;
                healInterval = 2.6f;
                healRange = 8.5f;
                nextHealTime = 0f;
                break;
        }
        health = maxHealth;

        if (personality == Personality.Assault && isSquadLeader)
        {
            health *= 1.5f;
            bulletDamage = Mathf.RoundToInt(bulletDamage * 1.5f);
            agent.speed *= 1.15f;
            transform.localScale *= 1.5f;
        }
    }

    void OnDestroy()
    {
        allEnemies.Remove(this);
        if (stateTextObj) Destroy(stateTextObj);
        if (isSquadLeader)
            SquadPanic(squadID);
    }

    void Update()
    {
        if (isDying) return;
        if (agent == null || !agent.enabled) return; // Robustness

        if (health <= 0 && currentState != State.Dead) currentState = State.Dead;
        if (currentState == State.Dead)
        {
            if (!isDying)
            {
                isDying = true;
                StartCoroutine(DeathAnimation());
            }
            return;
        }

        if (forcedPanic)
        {
            currentState = State.Panic;
            if (Time.time > panicEndTime)
            {
                forcedPanic = false;
                currentState = State.Patrol;
                return;
            }
            HandlePanic();
            UpdateVisuals();
            return;
        }

        if (player == null)
        {
            Debug.LogWarning($"{gameObject.name}: No player found, cannot run AI logic!");
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSee = CanSeePlayerWithVision();

        // MEMORY/SEARCH LOGIC
        if (canSee)
        {
            lastSeenPosition = player.position;
            timeSinceLastSeen = 0f;
            playerWasSeenRecently = true;
        }
        else
        {
            timeSinceLastSeen += Time.deltaTime;
            if (timeSinceLastSeen > forgetPlayerDelay)
                playerWasSeenRecently = false;
        }

        // -- PERSONALITY LOGIC OVERRIDES --
        if (personality == Personality.Medic) { MedicLogic(); UpdateVisuals(); return; }
        if (personality == Personality.Berserker) { BerserkerLogic(); UpdateVisuals(); return; }
        if (personality == Personality.Flanker) { FlankerLogic(); UpdateVisuals(); return; }

        if (personality == Personality.Sniper && sniperRelocateTimer > 0)
        {
            sniperRelocateTimer -= Time.deltaTime;
            if (sniperRelocateTimer <= 0)
            {
                currentState = State.SeekCover;
                sniperShotsLeft = Random.Range(2, 4);
            }
        }

        UpdateVisuals();

        // --- UTILITY AI STATE DECISION (improved, not hardcoded) ---
        if (currentState != State.Panic && currentState != State.Heal)
        {
            float healthNorm = Mathf.Clamp01(health / maxHealth);
            float distNorm = Mathf.Clamp01(distToPlayer / visionDistance);

            // Utility functions (0-1)
            float attackUtility = (canSee ? 1f : 0f) * (distToPlayer < shootingRange ? 1f : Mathf.Clamp01(1.2f - distNorm));
            float seekCoverUtility = ShouldSeekCover() ? (1f - healthNorm) : 0f;
            float fleeUtility = (health < fleeHealthThreshold) ? 1f - healthNorm : 0f;
            float dodgeUtility = (allowDodge && Time.time > lastDodgeTime + dodgeCooldown) ? Random.value * (1f - healthNorm) : 0f;
            float searchUtility = (playerWasSeenRecently && !canSee) ? 1f - distNorm : 0f;
            float patrolUtility = (!playerWasSeenRecently && health > 0.7f * maxHealth)
                ? 1f
                : 0.2f * (playerWasSeenRecently ? 0f : 1f);
            float chaseUtility = (canSee || playerWasSeenRecently) ? Mathf.Clamp01(1f - distNorm) : 0f;

            float meleeUtility = (personality == Personality.Berserker && health < 40f) ? 1f : 0f;

            // Pick best state
            float[] utilities = { patrolUtility, chaseUtility, attackUtility, seekCoverUtility, fleeUtility, dodgeUtility, searchUtility, meleeUtility };
            int bestIndex = 0; float best = utilities[0];
            for (int i = 1; i < utilities.Length; i++) { if (utilities[i] > best) { best = utilities[i]; bestIndex = i; } }
            State[] states = { State.Patrol, State.Chase, State.Attack, State.SeekCover, State.Flee, State.Dodge, State.Search, State.MeleeAttack };
            State chosen = states[bestIndex];

            // Avoid unnecessary state thrashing
            if (currentState != chosen)
            {
                currentState = chosen;
                if (chosen == State.Dodge) lastDodgeTime = Time.time;
            }
        }

        // Execute state
        switch (currentState)
        {
            case State.Patrol: Patrol(); break;
            case State.Alerted:
            case State.Chase: Chase(); break;
            case State.Attack: Attack(); break;
            case State.SeekCover: SeekCover(); break;
            case State.Peeking: PeekingFromCover(); break;
            case State.Flee: FleeToMedic(); break;
            case State.Dodge: Dodge(); break;
            case State.Search: SearchForPlayer(); break;
            case State.Panic: HandlePanic(); break;
            case State.Heal: HealAlly(); break;
            case State.MeleeAttack: BerserkerMeleeAttack(); break;
        }

        // LOS-aware group alerting
        if (currentState == State.Patrol && distToPlayer < shootingRange && canSee)
        {
            AlertAllies();
            currentState = State.Chase;
        }
        if (currentState == State.Alerted && distToPlayer < shootingRange && canSee)
            currentState = State.Chase;

        // Heatmap logging
        if (Vector3.Distance(transform.position, lastHeatPos) > 2f)
        {
            if (PlayerHeatmap.Instance != null)
                PlayerHeatmap.Instance.RegisterPath(transform.position, personality == Personality.Flanker ? 2f : 1f);
            lastHeatPos = transform.position;
        }
    }

    // ================== ENEMY AI LOGIC ==================


    void FlankerLogic()
    {
        // Death state check
        if (health <= 0 && currentState != State.Dead)
            currentState = State.Dead;

        if (currentState == State.Dead)
        {
            if (!isDying)
                StartCoroutine(DeathAnimation());
            return;
        }

        // Flanking calculation
        if (player == null || agent == null)
            return; // Defensive: Ensure references

        Vector3 toPlayer = player.position - transform.position;
        Vector3 leftDir = Vector3.Cross(Vector3.up, toPlayer.normalized);
        Vector3 rightDir = -leftDir;
        const float flankDistance = 8f;

        Vector3 leftFlank = player.position + leftDir * flankDistance;
        Vector3 rightFlank = player.position + rightDir * flankDistance;

        float lateral = PlayerPatternTracker.Instance != null ? PlayerPatternTracker.Instance.GetLateralMovementRatio() : 0f;
        int patternPreferred = (lateral > 0.6f) ? -1 : 1;

        float leftHeat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetHeat(leftFlank) : 0f;
        float rightHeat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetHeat(rightFlank) : 0f;

        const float bias = 0.8f; // 0 = all pattern, 1 = all heat
        float leftScore = Mathf.Lerp(patternPreferred > 0 ? 1f : 0f, 1f - leftHeat, bias);
        float rightScore = Mathf.Lerp(patternPreferred < 0 ? 1f : 0f, 1f - rightHeat, bias);

        float leftPathHeat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetPathHeat(leftFlank) : float.MaxValue;
        float rightPathHeat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetPathHeat(rightFlank) : float.MaxValue;

        Vector3 chosenFlank = leftPathHeat < rightPathHeat ? leftFlank : rightFlank;

        agent.SetDestination(chosenFlank);
        agent.isStopped = false;

        // Attack logic
        if (
            Time.time > lastShootTime + shootCooldown &&
            Vector3.Distance(transform.position, player.position) < shootingRange &&
            CanSeePlayerWithVision())
        {
            ShootAssault();
            lastShootTime = Time.time;
        }
    }

    void BerserkerLogic()
    {
        // Death state check
        if (health <= 0 && currentState != State.Dead)
            currentState = State.Dead;

        if (currentState == State.Dead)
        {
            if (!isDying)
                StartCoroutine(DeathAnimation());
            return;
        }

        if (agent == null || player == null)
            return;

        // Retreating logic
        if (isRetreating)
        {
            Vector3 away = (transform.position - player.position).normalized * 8f;
            agent.speed = berserkSpeed * 1.3f;
            agent.SetDestination(transform.position + away);
            agent.isStopped = false;

            berserkerRetreatTimer -= Time.deltaTime;
            if (berserkerRetreatTimer <= 0)
            {
                isRetreating = false;
                meleeHits = 0;
            }
            UpdateVisuals();
            return;
        }

        // Chase logic
        agent.isStopped = false;
        agent.speed = berserkSpeed;
        agent.SetDestination(player.position);

        // Melee attack
        if (
            Vector3.Distance(transform.position, player.position) < meleeRange &&
            Time.time > nextMeleeTime)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage((int)meleeDamage);
                meleeHits++;
                nextMeleeTime = Time.time + meleeCooldown;

                if (meleeHits >= maxMeleeHits)
                {
                    isRetreating = true;
                    berserkerRetreatTimer = berserkerRetreatTime + Random.Range(-0.5f, 0.7f);
                }
            }
        }
        UpdateVisuals();
    }


    void MedicLogic()
    {
        // Death state check
        if (health <= 0 && currentState != State.Dead)
            currentState = State.Dead;

        if (currentState == State.Dead)
        {
            if (!isDying)
                StartCoroutine(DeathAnimation());
            return;
        }

        if (agent == null)
            return;

        // Flee if player visible
        if (CanSeePlayerWithVision())
        {
            Flee();
            UpdateVisuals();
            return;
        }

        // Heal ally if possible
        if (Time.time > nextHealTime && FindMedicTarget() && medicHealTarget != null)
        {
            agent.SetDestination(medicHealTarget.transform.position);

            bool isClose = Vector3.Distance(transform.position, medicHealTarget.transform.position) < 2.2f;
            bool allyAlive = medicHealTarget.currentState != State.Dead;

            if (isClose && allyAlive)
            {
                float previousHealth = medicHealTarget.health;
                medicHealTarget.health = Mathf.Min(medicHealTarget.health + healAmount, medicHealTarget.maxHealth);
                nextHealTime = Time.time + healInterval;

                if (medicHealTarget.health > previousHealth && medicHealPulsePrefab != null)
                {
                    GameObject pulse = Instantiate(medicHealPulsePrefab, transform.position + Vector3.up * 0.7f, Quaternion.identity);
                    pulse.transform.localScale = Vector3.one * (healRange * 0.35f);
                    medicHealTarget.StartFlash(Color.green, 0.45f);
                }
            }
        }
        else
        {
            Patrol();
        }

        UpdateVisuals();
    }

    void BerserkerMeleeAttack()
    {
        BerserkerLogic();
    }

    void HealAlly()
    {
        // Logic moved to MedicLogic.
        currentState = State.Patrol;
    }


    // ----------- GENERAL UTILITY -----------

    bool CanSeePlayerWithVision()
    {
        if (player == null) return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0;
        float distance = toPlayer.magnitude;
        float fov = visionAngle;
        if (distance > visionDistance) return false;

        Vector3 forward = transform.forward;
        forward.y = 0;
        float angleToPlayer = Vector3.Angle(forward, toPlayer);
        if (angleToPlayer > fov * 0.5f) return false;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        RaycastHit hit;
        if (Physics.Raycast(origin, (player.position - origin).normalized, out hit, distance + 0.1f))
        {
            if (hit.collider != null && hit.collider.transform == player)
            {
                Debug.DrawLine(origin, hit.point, Color.green, 0.05f);
                return true;
            }
        }
        return false;
    }

    // ----------- MOVEMENT/PATROL -----------

    void Patrol()
    {
        if (agent == null) return;

        agent.isStopped = false;

        // Follow squad leader if assault and not leader
        if (personality == Personality.Assault && !isSquadLeader)
        {
            EnemyAI closestLeader = null;
            float closestDist = float.MaxValue;

            foreach (var ai in allEnemies)
            {
                if (ai != this && ai.isSquadLeader && ai.currentState != State.Dead)
                {
                    float dist = Vector3.Distance(transform.position, ai.transform.position);
                    if (dist < closestDist)
                    {
                        closestLeader = ai;
                        closestDist = dist;
                    }
                }
            }

            if (closestLeader != null)
            {
                squadID = closestLeader.squadID;
                agent.SetDestination(closestLeader.transform.position);
                return;
            }
        }

        patrolTimer += Time.deltaTime;
        if (patrolTimer > patrolResetTime)
        {
            SetRandomPatrolPoint();
            patrolTimer = 0f;
        }
        agent.SetDestination(patrolPoint);

        if (Vector3.Distance(transform.position, patrolPoint) < 0.5f)
        {
            SetRandomPatrolPoint();
            patrolTimer = 0f;
        }

        // Stuck? Speed up patrol reset timer.
        if (agent.velocity.magnitude < 0.05f)
            patrolTimer += Time.deltaTime * 2f;
    }

    void Chase()
    {
        if (agent == null || player == null) return;

        agent.isStopped = false;

        // Flanker: Orbit is handled elsewhere

        // Shotgunner: Zig-zag movement
        if (personality == Personality.Shotgunner)
        {
            float zigzagInterval = 0.7f;
            if (Time.time > shotgunZigzagTimer)
            {
                shotgunZigzagDir = Random.value > 0.5f ? 1f : -1f;
                shotgunZigzagTimer = Time.time + zigzagInterval;
            }
            Vector3 perp = Vector3.Cross((player.position - transform.position).normalized, Vector3.up);
            Vector3 zigzag = transform.position + perp * shotgunZigzagDir * Random.Range(3.5f, 6.2f);
            agent.SetDestination(Vector3.Lerp(player.position, zigzag, 0.37f));
            return;
        }

        // Assault: Regroup with squad if too far from leader
        if (personality == Personality.Assault)
        {
            EnemyAI leader = null;
            float minDist = float.MaxValue;
            foreach (var ally in allEnemies)
            {
                if (ally == this || ally.currentState == State.Dead) continue;
                if (ally.isSquadLeader)
                {
                    float d = Vector3.Distance(transform.position, ally.transform.position);
                    if (d < minDist)
                    {
                        leader = ally;
                        minDist = d;
                    }
                }
            }

            if (leader != null && leader != this)
            {
                float dist = Vector3.Distance(transform.position, leader.transform.position);
                if (dist > regroupDist)
                {
                    agent.SetDestination(leader.transform.position);
                    return;
                }

                // Formation positioning
                List<EnemyAI> squad = allEnemies.FindAll(e => e != leader && e.currentState != State.Dead);
                int index = squad.IndexOf(this);
                float spacing = 2.2f;
                Vector3 right = Vector3.Cross((player.position - leader.transform.position).normalized, Vector3.up);
                Vector3 formationPos = leader.transform.position + right * (index - squad.Count / 2f) * spacing;
                agent.SetDestination(formationPos + (player.position - leader.transform.position).normalized * 2f);
                return;
            }
            else
            {
                FlockWithAlliesOrTargetPlayer();
                return;
            }
        }

        // Sniper: Move to best spot or hang back
        if (personality == Personality.Sniper)
        {
            int numAttackers = 0;
            foreach (var e in allEnemies)
            {
                if (e == this) continue;
                if (
                    (e.personality == Personality.Assault ||
                    e.personality == Personality.Flanker ||
                    e.personality == Personality.Grenadier ||
                    e.personality == Personality.Shotgunner)
                    &&
                    (e.currentState == State.Chase ||
                     e.currentState == State.Attack ||
                     e.currentState == State.Search))
                {
                    numAttackers++;
                }
            }
            if (numAttackers >= 2 || CanSeePlayerWithVision())
            {
                Transform bestSpot = FindBestSniperSpot();
                if (bestSpot != null)
                {
                    agent.SetDestination(bestSpot.position);
                }
                else
                {
                    Vector3 toPlayer = (player.position - transform.position).normalized;
                    Vector3 sniperSpot = player.position - toPlayer * (shootingRange - 2f);
                    agent.SetDestination(sniperSpot);
                }
            }
            else
            {
                SeekCover();
            }
            return;
        }

        // Grenadier: Skittish reposition
        if (personality == Personality.Grenadier)
        {
            if (Time.time > grenadierSkittishCooldown)
            {
                Vector3 awayFromPlayer = (transform.position - player.position).normalized;
                Vector3 newPos = transform.position + awayFromPlayer * Random.Range(2.5f, 7f) + Random.insideUnitSphere * 2f;
                newPos.y = transform.position.y;
                agent.SetDestination(newPos);
                grenadierSkittishCooldown = Time.time + Random.Range(2.3f, 4.8f);
            }
            else
            {
                agent.isStopped = true;
            }
            return;
        }

        // Default: Move to last seen position or patrol
        Vector3 target = playerWasSeenRecently ? lastSeenPosition : patrolPoint;
        agent.SetDestination(target);
    }

    // ----------- COMBAT/ATTACK -----------

    void Attack()
    {
        FacePlayer();

        // Flanker: Already handled in FlankerLogic
        if (personality == Personality.Flanker) return;
        // Berserker: Only attacks in MeleeAttack state
        if (personality == Personality.Berserker) return;
        // Medic: Does not attack
        if (personality == Personality.Medic) return;

        // Assault burst fire
        if (personality == Personality.Assault)
        {
            agent.isStopped = false;
            if (burstBullets == 0 && burstPauseTime <= 0)
            {
                burstMax = Random.Range(3, 7);
                burstBullets = burstMax;
            }
            if (burstPauseTime > 0)
            {
                burstPauseTime -= Time.deltaTime;
                return;
            }

            if (burstBullets > 0 &&
                Time.time > lastShootTime + shootCooldown &&
                Vector3.Distance(transform.position, player.position) < shootingRange &&
                CanSeePlayerWithVision())
            {
                // Pattern-aware fire
                float lateral = PlayerPatternTracker.Instance != null ? PlayerPatternTracker.Instance.GetLateralMovementRatio() : 0f;
                Vector3 shootTarget = player.position;

                if (Mathf.Abs(lateral) > 0.7f)
                {
                    Vector3 perp = Vector3.Cross((player.position - transform.position).normalized, Vector3.up);
                    shootTarget += perp * Mathf.Sign(lateral) * 2.5f;
                }

                ShootAssault();
                burstBullets--;
                lastShootTime = Time.time;
                if (burstBullets == 0)
                {
                    burstPauseTime = Random.Range(0.7f, 2.1f);
                }
            }
        }
        // Sniper logic
        else if (personality == Personality.Sniper)
        {
            agent.isStopped = true;
            if (sniperShotsLeft > 0 &&
                Time.time > lastShootTime + shootCooldown &&
                Vector3.Distance(transform.position, player.position) < shootingRange &&
                CanSeePlayerWithVision())
            {
                ShootSniper();
                sniperShotsLeft--;
                lastShootTime = Time.time;
                if (sniperShotsLeft == 0)
                {
                    sniperRelocateTimer = Random.Range(1.6f, 2.7f);
                    currentState = State.SeekCover;
                }
            }
        }
        // Shotgunner logic
        else if (personality == Personality.Shotgunner)
        {
            agent.isStopped = false;
            if (Time.time > lastShootTime + shootCooldown &&
                Vector3.Distance(transform.position, player.position) < shootingRange &&
                CanSeePlayerWithVision())
            {
                ShootShotgun();
                lastShootTime = Time.time;
            }
        }
        // Grenadier logic
        else if (personality == Personality.Grenadier)
        {
            agent.isStopped = false;
            if (Time.time > nextGrenadeTime &&
                Vector3.Distance(transform.position, player.position) < shootingRange + 2f &&
                CanSeePlayerWithVision())
            {
                ThrowGrenade();
                nextGrenadeTime = Time.time + grenadeCooldown;
            }
        }

        if (inCover && !isPeeking && canPeekAtPlayer())
        {
            isPeeking = true;
            peekTimer = peekTime;
            currentState = State.Peeking;
        }
    }

    void ShootAssault()
    {
        if (!bulletPrefab || !shootPoint) return;

        Vector3 targetPos = player.position;
        Vector3 dir = (targetPos - shootPoint.position).normalized;
        dir = Quaternion.AngleAxis(Random.Range(-aimSpread, aimSpread), Vector3.up) * dir;

        GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);

        if (bullet.TryGetComponent<Bullet>(out Bullet b))
        {
            b.damage = bulletDamage;
            b.speed = bulletSpeed;
            b.SetDirection(dir);
            b.isPlayer = false;
        }

        Collider myCol = GetComponent<Collider>();
        Collider bulletCol = bullet.GetComponent<Collider>();
        if (myCol && bulletCol)
            Physics.IgnoreCollision(bulletCol, myCol);
    }

    // ----------- FLEE/HEAL LOGIC -----------

    void FleeToMedic()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        nearestMedic = FindNearestMedic();
        if (nearestMedic != null && nearestMedic.currentState != State.Dead)
        {
            agent.isStopped = false;
            agent.speed = GetBaseSpeedForPersonality() * 1.25f;
            agent.SetDestination(nearestMedic.transform.position);

            if (Vector3.Distance(transform.position, nearestMedic.transform.position) < 2.5f)
            {
                agent.isStopped = true;
            }
        }
        else
        {
            Vector3 fleeDir = (transform.position - player.position).normalized;
            Vector3 fleePos = transform.position + fleeDir * 7.5f;
            agent.SetDestination(fleePos);
        }
    }

    EnemyAI FindNearestMedic()
    {
        float minDist = float.MaxValue;
        EnemyAI nearest = null;
        foreach (var e in allEnemies)
        {
            if (e == this) continue;
            if (e.personality == Personality.Medic && e.currentState != State.Dead)
            {
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = e;
                }
            }
        }
        return nearest;
    }

    // ----------- COVER PEEKING -----------

    void PeekingFromCover()
    {
        agent.isStopped = true;
        FacePlayer();

        peekTimer -= Time.deltaTime;
        if (peekTimer > 0)
        {
            if (CanSeePlayerWithVision() && Time.time > lastShootTime + shootCooldown)
            {
                if (personality == Personality.Assault) ShootAssault();
                else if (personality == Personality.Sniper) ShootSniper();
                else if (personality == Personality.Shotgunner) ShootShotgun();
                else if (personality == Personality.Grenadier) ThrowGrenade();

                lastShootTime = Time.time;
            }
        }
        else
        {
            isPeeking = false;
            currentState = State.SeekCover;
        }
    }


    // ----------- COVER/TACTICAL POSITIONING -----------

    Transform FindBestCoverSpot()
    {
        Transform best = null;
        float bestValue = float.NegativeInfinity;
        foreach (var c in coverPoints)
        {
            float dist = Vector3.Distance(transform.position, c.position);
            float playerDist = Vector3.Distance(c.position, player.position);

            // Find closest ally to this cover point
            float allyDist = float.MaxValue;
            foreach (var enemy in allEnemies)
            {
                if (enemy == this) continue;
                float d = Vector3.Distance(enemy.transform.position, c.position);
                if (d < allyDist) allyDist = d;
            }

            float heat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetHeat(c.position) : 0f;
            float heatPenalty = heat * 2.5f;

            // Only choose cover that's actually blocking
            Vector3 dir = (player.position - c.position).normalized;
            RaycastHit hit;
            bool blocked = false;
            if (Physics.Raycast(c.position + Vector3.up * 0.5f, dir, out hit, playerDist))
                blocked = hit.collider && hit.collider.transform != player;
            if (!blocked) continue;

            float attackHeatVal = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetAttackHeat(c.position) : 0f;
            float attackPenalty = attackHeatVal * 3f;
            float score = -dist * 0.8f + playerDist * 1.5f - allyDist * 0.3f - heatPenalty - attackPenalty;
            if (score > bestValue)
            {
                best = c;
                bestValue = score;
            }
        }
        return best;
    }

    Transform FindBestSniperSpot()
    {
        Transform best = null;
        float bestValue = float.NegativeInfinity;
        foreach (var c in coverPoints)
        {
            float playerDist = Vector3.Distance(c.position, player.position);
            if (playerDist < shootingRange * 0.7f) continue; // Not too close

            float coverScore = playerDist * 1.8f;
            float heat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetHeat(c.position) : 0f;
            coverScore -= heat * 2.2f;

            // Avoid clustering with other snipers
            foreach (var e in allEnemies)
            {
                if (e == this) continue;
                float d = Vector3.Distance(e.transform.position, c.position);
                if (d < 2f) coverScore -= 3f;
            }

            // Only use spots with real cover
            Vector3 dir = (player.position - c.position).normalized;
            RaycastHit hit;
            if (Physics.Raycast(c.position + Vector3.up * 0.5f, dir, out hit, playerDist))
            {
                if (hit.collider && hit.collider.transform != player)
                    coverScore += 8f;
            }

            if (coverScore > bestValue)
            {
                best = c;
                bestValue = coverScore;
            }
        }
        return best;
    }

    Vector3 GetHidePosition(Transform cover, Vector3 threatPos)
    {
        Vector3 dir = (cover.position - threatPos).normalized;
        return cover.position + dir * 1.2f;
    }

    void SeekCover()
    {
        agent.isStopped = false;
        currentCover = FindBestCoverSpot();
        if (currentCover)
        {
            Vector3 hidePos = GetHidePosition(currentCover, player.position);
            agent.SetDestination(hidePos);
            inCover = true;
            if (Vector3.Distance(transform.position, hidePos) < 0.6f)
            {
                inCover = true;
                if (!isPeeking && canPeekAtPlayer())
                {
                    isPeeking = true;
                    peekTimer = peekTime;
                    currentState = State.Peeking;
                }
            }
        }
        else
        {
            inCover = false;
            Flee();
        }
    }

    bool canPeekAtPlayer()
    {
        if (!currentCover) return false;
        Vector3 coverEdge = currentCover.position + (player.position - currentCover.position).normalized * 1.4f + Vector3.up * 0.5f;
        RaycastHit hit;
        if (Physics.Raycast(coverEdge, (player.position - coverEdge).normalized, out hit, shootingRange + 2f))
        {
            return hit.collider && hit.collider.transform == player;
        }
        return false;
    }

    // ----------- GENERAL ACTIONS -----------

    void Flee()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        Vector3 fleeDir = (transform.position - player.position).normalized;
        Vector3 fleePos = transform.position + fleeDir * 7.5f;
        agent.SetDestination(fleePos);
    }

    void Dodge()
    {
        agent.isStopped = false;
        float dodgeDir = Random.value > 0.5f ? 1f : -1f;
        Vector3 perp = Vector3.Cross((player.position - transform.position).normalized, Vector3.up);
        Vector3 dodgeTarget = transform.position + perp * dodgeDir * Random.Range(1f, 2f);
        agent.SetDestination(dodgeTarget);
        if (Vector3.Distance(transform.position, dodgeTarget) < 0.3f)
            currentState = State.Chase;
    }

    void SearchForPlayer()
    {
        agent.isStopped = false;
        agent.speed = 2.5f;
        agent.SetDestination(lastSeenPosition);

        if (Vector3.Distance(transform.position, lastSeenPosition) < 0.7f || timeSinceLastSeen > forgetPlayerDelay)
        {
            playerWasSeenRecently = false;
            currentState = State.Patrol;
        }
    }

    void HandlePanic()
    {
        agent.isStopped = false;
        float baseSpeed = GetBaseSpeedForPersonality();
        agent.speed = baseSpeed * 1.5f;

        if (Time.time > panicEndTime)
        {
            agent.speed = baseSpeed;
            currentState = State.Patrol;
            return;
        }

        panicMoveTimer += Time.deltaTime;
        if (panicMoveTimer >= panicMoveInterval || agent.remainingDistance < 1f)
        {
            panicMoveTimer = 0f;
            panicMoveInterval = Random.Range(0.5f, 3f);

            Vector3 randDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            panicTarget = transform.position + randDir * Random.Range(6f, 18f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(panicTarget, out hit, 2f, NavMesh.AllAreas))
                panicTarget = hit.position;

            agent.SetDestination(panicTarget);
        }
    }

    float GetBaseSpeedForPersonality()
    {
        switch (personality)
        {
            case Personality.Sniper: return 2f;
            case Personality.Shotgunner: return 5.3f;
            case Personality.Grenadier: return 3f;
            case Personality.Assault: return 3.2f;
            case Personality.Berserker: return 6.3f;
            case Personality.Flanker: return 4.5f;
            case Personality.Medic: return 6f;
            default: return 3f;
        }
    }

    // ----------- SQUAD LOGIC -----------

    void SquadPanic(int squad)
    {
        foreach (var e in allEnemies)
        {
            if (e && e.squadID == squad && e.currentState != State.Dead && e.personality == Personality.Assault)
            {
                e.ForcePanic(Random.Range(4f, 10f));
                Debug.Log($"{e.name} is panicking due to squad leader death!");
            }
        }
    }

    public void ForcePanic(float duration)
    {
        panicMoveTimer = 0f;
        panicMoveInterval = Random.Range(0.5f, 3f);
        forcedPanic = true;
        currentState = State.Panic;
        panicEndTime = Time.time + duration;
        Debug.Log($"{gameObject.name} forced into PANIC for {duration:F2} seconds!");
    }

    void FlockWithAlliesOrTargetPlayer()
    {
        Vector3 center = Vector3.zero; int count = 0;
        foreach (var ally in allEnemies)
        {
            if (ally != this && ally.personality == Personality.Assault && Vector3.Distance(transform.position, ally.transform.position) < 7f)
            {
                center += ally.transform.position;
                count++;
            }
        }
        if (count > 0)
        {
            center /= count;
            Vector3 dest = Vector3.Lerp(center, player.position, 0.4f);
            agent.SetDestination(dest);
        }
        else
        {
            agent.SetDestination(player.position);
        }
    }

    // ----------- SHOOTING & GRENADE -----------

    void ShootSniper()
    {
        if (!bulletPrefab || !shootPoint) return;
        Vector3 playerVel = player.GetComponent<Rigidbody>() != null ? player.GetComponent<Rigidbody>().velocity : Vector3.zero;
        Vector3 toPlayer = player.position - shootPoint.position;
        float t = toPlayer.magnitude / bulletSpeed;

        float straightness = PlayerPatternTracker.Instance != null ? PlayerPatternTracker.Instance.GetMovementStraightness() : 0f;
        float predictionMultiplier = Mathf.Lerp(1f, 1.7f, Mathf.Clamp01(straightness));
        Vector3 futurePos = player.position + playerVel * t * predictionMultiplier;

        Vector3 dir = (futurePos + Vector3.up * 0.5f - shootPoint.position).normalized;
        dir = Quaternion.AngleAxis(Random.Range(-aimSpread, aimSpread), Vector3.up) * dir;

        GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
        if (bullet.TryGetComponent<Bullet>(out Bullet b))
        {
            b.damage = bulletDamage;
            b.speed = bulletSpeed;
            b.SetDirection(dir);
            b.isPlayer = false;
        }
        Collider myCol = GetComponent<Collider>();
        Collider bulletCol = bullet.GetComponent<Collider>();
        if (myCol && bulletCol) Physics.IgnoreCollision(bulletCol, myCol);
    }

    void ShootShotgun()
    {
        if (!bulletPrefab || !shootPoint) return;
        for (int i = 0; i < numPellets; i++)
        {
            Vector3 targetPos = player.position;
            targetPos.y = shootPoint.position.y;
            Vector3 dir = (targetPos - shootPoint.position).normalized;
            dir = Quaternion.AngleAxis(Random.Range(-aimSpread, aimSpread), Vector3.up) * dir;

            GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
            if (bullet.TryGetComponent<Bullet>(out Bullet b))
            {
                b.damage = bulletDamage;
                b.speed = bulletSpeed;
                b.SetDirection(dir);
                b.isPlayer = false;
            }
            Collider myCol = GetComponent<Collider>();
            Collider bulletCol = bullet.GetComponent<Collider>();
            if (myCol && bulletCol) Physics.IgnoreCollision(bulletCol, myCol);
        }
    }

    void ThrowGrenade()
    {
        if (!grenadePrefab || !shootPoint) return;
        Vector3 playerVel = player.GetComponent<Rigidbody>() != null ? player.GetComponent<Rigidbody>().velocity : Vector3.zero;
        float prediction = 0.8f;
        if (PlayerPatternTracker.Instance)
            prediction = Mathf.Lerp(0.7f, 1.5f, PlayerPatternTracker.Instance.GetMovementStraightness());

        Vector3 predictedPos = player.position + playerVel * prediction;
        Vector3 toPredicted = predictedPos - shootPoint.position;
        toPredicted.y = 0;
        Vector3 throwVel = toPredicted.normalized * (30f * Random.Range(0.5f, 1.6f));
        GameObject grenadeObj = Instantiate(grenadePrefab, shootPoint.position, Quaternion.identity);
        grenadeObj.GetComponent<Grenade>().initialVelocity = throwVel;
    }

    // ----------- MISCELLANEOUS -----------

    void FacePlayer()
    {
        Vector3 targetDirection = player.position - transform.position;
        targetDirection.y = 0;
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360 * Time.deltaTime);
        }
    }

    void SetRandomPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 candidate = transform.position + new Vector3(Random.Range(-7f, 7f), 0, Random.Range(-7f, 7f));
            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas))
            {
                patrolPoint = hit.position;
                return;
            }
        }
        patrolPoint = transform.position;
    }

    void FindAllCoverPoints()
    {
        coverPoints = new List<Transform>();
        foreach (var obj in GameObject.FindGameObjectsWithTag("Cover"))
            coverPoints.Add(obj.transform);
    }

    bool ShouldSeekCover()
    {
        float threshold = (seekCoverOften ? 50f : 35f);
        return (health < threshold && Vector3.Distance(transform.position, player.position) < shootingRange && CanSeePlayerWithVision());
    }

    void AlertAllies()
    {
        foreach (var ally in allEnemies)
        {
            if (ally == this) continue;
            if (Vector3.Distance(transform.position, ally.transform.position) < shootingRange * 1.4f)
            {
                if (ally.currentState == State.Patrol)
                    ally.currentState = State.Alerted;
            }
        }
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        StartFlash(new Color(1f, 1f, 1f), 0.05f);

        lastSeenPosition = player != null ? player.position : transform.position;
        playerWasSeenRecently = true;
        timeSinceLastSeen = 0f;

        if (personality == Personality.Assault && health < 60f && Time.time > lastDodgeTime + dodgeCooldown)
        {
            currentState = State.Dodge;
            lastDodgeTime = Time.time;
        }
        if (health <= 0) currentState = State.Dead;
    }

    IEnumerator DeathAnimation()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
            player.AddScore(1);
        if (agent) agent.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col) col.enabled = false;

        if (rend) rend.material.color = Color.Lerp(Color.red, Color.gray, 0.7f);

        float t = 0f;
        Vector3 startScale = transform.localScale;
        Color startColor = rend.material.color;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if (rend && rend.material.HasProperty("_Color"))
            {
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                rend.material.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    void SetupStateText()
    {
        stateTextObj = new GameObject("StateText");
        stateTextObj.transform.SetParent(transform);
        stateTextObj.transform.localPosition = new Vector3(0, 2.2f, 0);
        stateText = stateTextObj.AddComponent<TextMesh>();
        stateText.characterSize = 0.25f;
        stateText.alignment = TextAlignment.Center;
        stateText.anchor = TextAnchor.MiddleCenter;
        stateText.fontSize = 40;
        stateText.color = Color.black;
    }

    IEnumerator FlashColor(Color color, float duration = 0.18f)
    {
        if (rend == null) yield break;
        isFlashing = true;
        Color originalColor = rend.material.color;
        rend.material.color = color;
        yield return new WaitForSeconds(duration);
        rend.material.color = originalColor;
        isFlashing = false;
        flashCoroutine = null;
    }

    void StartFlash(Color color, float duration = 0.18f)
    {
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashColor(color, duration));
    }

    void UpdateVisuals()
    {
        if (!rend) return;
        if (isFlashing) return;

        switch (personality)
        {
            case Personality.Sniper: rend.material.color = new Color(1f, 0.13f, 0.13f); break;
            case Personality.Shotgunner: rend.material.color = new Color(1f, 0.8f, 0f); break;
            case Personality.Assault: rend.material.color = new Color(0.15f, 0.27f, 1f); break;
            case Personality.Grenadier: rend.material.color = new Color(0.7f, 0f, 1f); break;
            case Personality.Berserker: rend.material.color = new Color(0.6f, 0f, 0f); break;
            case Personality.Flanker: rend.material.color = new Color(0f, 1f, 1f); break;
            case Personality.Medic:
                if (Time.time < healPulseEndTime) rend.material.color = Color.green;
                else rend.material.color = new Color(0.8f, 1f, 0.6f);
                break;
        }

        if (stateText == null) return;
        switch (currentState)
        {
            case State.Patrol: stateText.text = "Patrol"; break;
            case State.Alerted: stateText.text = "Alerted"; break;
            case State.Chase: stateText.text = "Chase"; break;
            case State.Attack: stateText.text = "Attack"; break;
            case State.SeekCover: stateText.text = "Cover"; break;
            case State.Peeking: stateText.text = "Peek"; break;
            case State.Flee: stateText.text = "Flee"; break;
            case State.Dodge: stateText.text = "Dodge"; break;
            case State.Search: stateText.text = "Search"; break;
            case State.Panic: stateText.text = "Panic!"; break;
            case State.Dead: stateText.text = "Dead"; break;
            case State.Heal: stateText.text = "Heal"; break;
            case State.MeleeAttack: stateText.text = "Berserk"; break;
        }
    }

    bool FindMedicTarget()
    {
        float lowestHealth = 9999f;
        EnemyAI target = null;
        foreach (var e in allEnemies)
        {
            if (e == this || e.currentState == State.Dead) continue;
            if (e.health < e.maxHealth && Vector3.Distance(transform.position, e.transform.position) < healRange)
            {
                if (e.health < lowestHealth)
                {
                    lowestHealth = e.health;
                    target = e;
                }
            }
        }
        medicHealTarget = target;
        return medicHealTarget != null;
    }

    public void ForceMoveTo(Vector3 pos)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.SetDestination(pos);
    }

    public void SetHealTarget(Transform target)
    {
        healTarget = target;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionDistance);
        Vector3 left = Quaternion.Euler(0, -visionAngle / 2f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, visionAngle / 2f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position, transform.position + left * visionDistance);
        Gizmos.DrawLine(transform.position, transform.position + right * visionDistance);
    }
#endif
}