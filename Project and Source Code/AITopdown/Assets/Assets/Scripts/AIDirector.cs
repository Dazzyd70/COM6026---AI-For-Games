using UnityEngine;
using System.Collections.Generic;

// ----------- AI DIRECTOR -----------

public class AIDirector : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject[] enemyPrefabs; // 0=Assault, 1=Sniper, 2=Shotgunner, 3=Grenadier, 4=Berserker, 5=Flanker, 6=Medic
    public Transform[] spawnPoints;

    [Header("Wave Settings")]
    public float baseTimeBetweenWaves = 8f;
    public int baseEnemiesPerWave = 3;
    public int maxEnemies = 15;

    private float timeSinceLastWave = 0f;
    private int currentWave = 1;

    private float difficulty = 1f;
    private float difficultyIncreasePerWave = 0.2f;

    public Camera mainCamera;
    public float spawnMinDistance = 14f;

    [Header("Minimums Per Wave")]
    public int minAssaultsPerWave = 2;
    public int minGrenadiersPerWave = 0;
    public int minMedicsPerWave = 0;
    public int minBerserkersPerWave = 0;
    public int minFlankersPerWave = 0;

    // Player analysis
    private Vector3 lastPlayerPosition;
    private float playerStillTime = 0f;
    private float playerMoveDistThreshold = 2f;
    private float campingTimeThreshold = 5f;
    private bool playerIsCamping = false;

    // Threat tracking
    private float playerThreat = 0f; // 0 = relaxed, 1 = very high
    private float threatIncreaseOnDamage = 0.12f;
    private float threatIncreasePerEnemyNear = 0.016f;
    private float threatDecreasePerSecond = 0.055f;
    private float threatDecayCooldown = 2f;
    private float threatDecayTimer = 0f;

    // Adaptive spawn delay
    private float adaptiveWaveDelay = 0f;
    private string lastEvent = "Normal";

    // Special events
    private int nextEventWave = 5;
    private bool lastWaveWasEvent = false;

    void Start()
    {
        if (!player)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) player = playerObj.transform;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
            Debug.LogWarning("No spawn points assigned to AI Director!");
        if (!mainCamera) mainCamera = Camera.main;
        lastPlayerPosition = player != null ? player.position : Vector3.zero;
        SpawnWave();
    }

    void Update()
    {
        timeSinceLastWave += Time.deltaTime;
        MonitorPlayerCamping();

        // Threat decay
        if (threatDecayTimer > 0)
            threatDecayTimer -= Time.deltaTime;
        else
            playerThreat = Mathf.Clamp01(playerThreat - threatDecreasePerSecond * Time.deltaTime);

        // Update threat from nearby enemies
        int enemiesNear = 0;
        foreach (var ai in EnemyAI.allEnemies)
        {
            if (ai == null) continue;
            float dist = Vector3.Distance(player.position, ai.transform.position);
            if (dist < 9f) enemiesNear++;
        }
        playerThreat = Mathf.Clamp01(playerThreat + enemiesNear * threatIncreasePerEnemyNear * Time.deltaTime);

        // Adaptive delay after intense waves
        if (adaptiveWaveDelay > 0)
        {
            adaptiveWaveDelay -= Time.deltaTime;
            return;
        }

        // Wave spawning
        if (timeSinceLastWave >= GetCurrentWaveDelay() && EnemyAI.allEnemies.Count < maxEnemies)
        {
            if (currentWave == nextEventWave)
            {
                SpawnSpecialEventWave();
                lastWaveWasEvent = true;
                lastEvent = "Special";
                nextEventWave += Random.Range(2, 4);
            }
            else
            {
                SpawnWave();
                lastWaveWasEvent = false;
                lastEvent = "Normal";
            }
            timeSinceLastWave = 0f;
            threatDecayTimer = threatDecayCooldown;
        }

        // Ensure squad leader always exists
        if (!SquadLeaderExists())
            SpawnSquadLeader();

        // Global AI coordination
        GlobalCoordination();
    }

    // ----------- Threat and Damage Tracking -----------

    public void OnPlayerDamaged()
    {
        playerThreat = Mathf.Clamp01(playerThreat + threatIncreaseOnDamage);
        threatDecayTimer = threatDecayCooldown;
    }

    // ----------- Player Camping Detection -----------

    void MonitorPlayerCamping()
    {
        if (player == null) return;

        float moveDist = Vector3.Distance(player.position, lastPlayerPosition);
        if (moveDist < playerMoveDistThreshold * Time.deltaTime)
        {
            playerStillTime += Time.deltaTime;
            if (playerStillTime > campingTimeThreshold)
                playerIsCamping = true;
        }
        else
        {
            playerStillTime = 0f;
            playerIsCamping = false;
        }
        lastPlayerPosition = player.position;
    }

    // ----------- HUD Display -----------
    //
    //void OnGUI()
    //{
    //    GUI.Label(new Rect(10, 10, 440, 32),
    //        $"Wave: {currentWave}   Threat: {playerThreat:F2}   Delay: {GetCurrentWaveDelay():F1}s   Event: {lastEvent}   Camping: {playerIsCamping}");
    //}

    // ----------- Global Coordination -----------

    void GlobalCoordination()
    {
        foreach (var ai in EnemyAI.allEnemies)
        {
            if (ai == null) continue;
            if (ai.personality.ToString() == "Medic")
            {
                EnemyAI target = FindLowestHealthAlly(ai);
                if (target != null && target.health < target.maxHealth * 0.7f && Vector3.Distance(ai.transform.position, target.transform.position) > 3f)
                {
                    ai.SetHealTarget(target.transform);
                }
            }
        }

        if (playerIsCamping)
        {
            List<EnemyAI> snipers = EnemyAI.allEnemies.FindAll(e => e && e.personality.ToString() == "Sniper");
            for (int i = 0; i < snipers.Count; i++)
            {
                float angle = 360f / snipers.Count * i;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * 7f;
                Vector3 desiredPos = player.position + offset;
                snipers[i].ForceMoveTo(desiredPos);
            }
        }
    }

    EnemyAI FindLowestHealthAlly(EnemyAI medic)
    {
        EnemyAI lowest = null;
        float minHealth = float.MaxValue;
        foreach (var ai in EnemyAI.allEnemies)
        {
            if (ai == null || ai == medic || ai.currentState == EnemyAI.State.Dead) continue;
            if (ai.health < ai.maxHealth && ai.health < minHealth)
            {
                lowest = ai;
                minHealth = ai.health;
            }
        }
        return lowest;
    }

    // ----------- Squad Logic -----------

    bool SquadLeaderExists()
    {
        foreach (var enemy in EnemyAI.allEnemies)
        {
            if (enemy != null && enemy.isSquadLeader) return true;
        }
        return false;
    }

    void SpawnSquadLeader()
    {
        List<Transform> validSpawns = GetValidSpawnPoints();
        if (validSpawns.Count == 0) return;
        Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
        GameObject enemyObj = Instantiate(enemyPrefabs[0], sp.position, Quaternion.identity); // Assault
        EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
        if (ai)
        {
            ai.player = player;
            ai.isSquadLeader = true;
            ai.squadID = currentWave + 99;
        }
    }

    public static bool IsOnScreen(Camera cam, Vector3 worldPos)
    {
        Vector3 viewportPoint = cam.WorldToViewportPoint(worldPos);
        return viewportPoint.z > 0 &&
               viewportPoint.x > 0 && viewportPoint.x < 1 &&
               viewportPoint.y > 0 && viewportPoint.y < 1;
    }

    // ----------- Spawn Point Selection -----------

    List<Transform> GetValidSpawnPoints()
    {
        List<Transform> validSpawns = new List<Transform>();

        float straightness = PlayerPatternTracker.Instance ? PlayerPatternTracker.Instance.GetMovementStraightness() : 0f;
        Vector3 avgDir = PlayerPatternTracker.Instance ? PlayerPatternTracker.Instance.GetAverageDirection() : Vector3.zero;

        foreach (var sp in spawnPoints)
        {
            float dist = Vector3.Distance(sp.position, player.position);
            float heat = PlayerHeatmap.Instance != null ? PlayerHeatmap.Instance.GetHeat(sp.position) : 0f;
            float dirBias = (avgDir != Vector3.zero) ? Vector3.Dot((sp.position - player.position).normalized, -avgDir.normalized) : 0f;

            float score = dist - 4f * dirBias * straightness;

            if (dist > spawnMinDistance && !IsOnScreen(mainCamera, sp.position) && heat < 2.2f && score > 0f)
                validSpawns.Add(sp);
        }
        return validSpawns;
    }

    // ----------- Difficulty & Weights -----------

    void AdjustDifficulty()
    {
        difficulty = 1f + (currentWave - 1) * difficultyIncreasePerWave;
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && pc.currentHealth > 70f)
        {
            difficulty += 0.3f;
        }

        minGrenadiersPerWave = (currentWave > 3) ? 1 : 0;
        minMedicsPerWave = (currentWave > 5) ? 1 : 0;
        minBerserkersPerWave = (currentWave > 6) ? 1 : 0;
        minFlankersPerWave = (currentWave > 8) ? 1 : 0;
    }

    // ----------- Adaptive Wave Delay -----------

    float GetCurrentWaveDelay()
    {
        float threatBias = Mathf.Lerp(0f, 7f, playerThreat); // Up to +7s delay at full threat
        float baseDelay = baseTimeBetweenWaves + threatBias;
        if (lastWaveWasEvent) baseDelay += 4f;
        return baseDelay;
    }

    // ----------- Wave Spawning -----------

    void SpawnWave()
    {
        if (EnemyAI.allEnemies.Count >= maxEnemies) return;

        AdjustDifficulty();
        int numEnemies = Mathf.RoundToInt(baseEnemiesPerWave * difficulty);
        currentWave++;

        int assaultsToSpawn = Mathf.Max(minAssaultsPerWave, numEnemies / 3);
        int grenadiersToSpawn = minGrenadiersPerWave;
        int medicsToSpawn = minMedicsPerWave;
        int berserkersToSpawn = minBerserkersPerWave;
        int flankersToSpawn = minFlankersPerWave;

        int[] spawnedCounts = new int[enemyPrefabs.Length];
        List<Transform> validSpawns = GetValidSpawnPoints();
        if (validSpawns.Count == 0) return;

        int spawned = 0;
        float[] weights = GetAdaptiveWeights();

        // Helper to spawn a type
        void SpawnType(int prefabIndex, int quota)
        {
            for (int i = 0; i < quota && spawned < numEnemies; i++)
            {
                Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                Vector3 spawnPos = sp.position; spawnPos.y = 0.5f;
                GameObject enemyObj = Instantiate(enemyPrefabs[prefabIndex], spawnPos, Quaternion.identity);
                EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                if (ai)
                {
                    ai.player = player;
                    ai.isSquadLeader = false;
                    ai.squadID = -1;
                }
                spawnedCounts[prefabIndex]++;
                spawned++;
            }
        }
        SpawnType(0, assaultsToSpawn);
        SpawnType(3, grenadiersToSpawn);
        SpawnType(4, berserkersToSpawn);
        SpawnType(5, flankersToSpawn);
        SpawnType(6, medicsToSpawn);

        // Dynamic squad spawning
        int left = numEnemies - spawned;
        int squadSize = Mathf.Clamp(Random.Range(3, 6), 2, left);
        bool spawnSquad = Random.value < 0.6f && left >= squadSize;
        if (spawnSquad)
        {
            int squadID = currentWave * 10;
            for (int i = 0; i < squadSize && spawned < numEnemies; i++)
            {
                Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                Vector3 spawnPos = sp.position; spawnPos.y = 0.5f;
                GameObject enemyObj = Instantiate(enemyPrefabs[0], spawnPos, Quaternion.identity);
                EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                if (ai)
                {
                    ai.player = player;
                    ai.isSquadLeader = (i == 0);
                    ai.squadID = squadID;
                }
                spawnedCounts[0]++;
                spawned++;
            }
        }

        // Fill remainder
        while (spawned < numEnemies)
        {
            float pick = Random.value * weights[weights.Length - 1];
            int chosen = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (pick < weights[i])
                {
                    chosen = i;
                    break;
                }
            }
            Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
            Vector3 spawnPos = sp.position; spawnPos.y = 0.5f;
            GameObject enemyObj = Instantiate(enemyPrefabs[chosen], spawnPos, Quaternion.identity);
            EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
            if (ai)
            {
                ai.player = player;
                ai.isSquadLeader = false;
                ai.squadID = -1;
            }
            spawnedCounts[chosen]++;
            spawned++;
        }
    }

    // ----------- Special Event Waves -----------

    void SpawnSpecialEventWave()
    {
        if (EnemyAI.allEnemies.Count >= maxEnemies) return;
        AdjustDifficulty();
        int numEnemies = Mathf.RoundToInt(baseEnemiesPerWave * (difficulty + 0.8f));
        currentWave++;

        List<Transform> validSpawns = GetValidSpawnPoints();
        if (validSpawns.Count == 0) return;

        int eventType = Random.Range(0, 4);
        int spawned = 0;

        switch (eventType)
        {
            case 0:
                // Berserker Rush
                for (int i = 0; i < numEnemies && spawned < maxEnemies; i++)
                {
                    Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                    GameObject enemyObj = Instantiate(enemyPrefabs[4], sp.position, Quaternion.identity);
                    EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                    if (ai)
                    {
                        ai.player = player;
                        ai.isSquadLeader = (i == 0);
                        ai.squadID = currentWave * 10;
                    }
                    spawned++;
                }
                lastEvent = "Berserker Rush!";
                break;
            case 1:
                // Medic Swarm
                for (int i = 0; i < numEnemies * 2 / 3; i++)
                {
                    Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                    GameObject enemyObj = Instantiate(enemyPrefabs[6], sp.position, Quaternion.identity);
                    EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                    if (ai)
                    {
                        ai.player = player;
                        ai.isSquadLeader = false;
                        ai.squadID = -1;
                    }
                }
                for (int i = 0; i < numEnemies / 3; i++)
                {
                    Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                    GameObject enemyObj = Instantiate(enemyPrefabs[3], sp.position, Quaternion.identity);
                    EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                    if (ai)
                    {
                        ai.player = player;
                        ai.isSquadLeader = false;
                        ai.squadID = -1;
                    }
                }
                spawned = numEnemies;
                lastEvent = "Medic Swarm!";
                break;
            case 2:
                // Flanker Frenzy
                for (int i = 0; i < numEnemies && spawned < maxEnemies; i++)
                {
                    Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                    GameObject enemyObj = Instantiate(enemyPrefabs[5], sp.position, Quaternion.identity);
                    EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                    if (ai)
                    {
                        ai.player = player;
                        ai.isSquadLeader = (i == 0);
                        ai.squadID = currentWave * 10;
                    }
                    spawned++;
                }
                lastEvent = "Flanker Frenzy!";
                break;
            case 3:
                // Assault Army
                for (int i = 0; i < numEnemies && spawned < maxEnemies; i++)
                {
                    Transform sp = validSpawns[Random.Range(0, validSpawns.Count)];
                    GameObject enemyObj = Instantiate(enemyPrefabs[0], sp.position, Quaternion.identity);
                    EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
                    if (ai)
                    {
                        ai.player = player;
                        ai.isSquadLeader = (i == 0);
                        ai.squadID = currentWave * 10;
                    }
                    spawned++;
                }
                lastEvent = "Assault Army!";
                break;
        }
        lastWaveWasEvent = true;
        adaptiveWaveDelay = 5f;
    }

    // ----------- Adaptive Weights -----------

    float[] GetAdaptiveWeights()
    {
        float[] baseWeights = { 1.6f, 0.9f, 1.1f, 0.7f, 0.5f, 0.5f, 0.6f };

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            if (pc.currentHealth < 45f)
            {
                baseWeights[4] *= 0.6f;
                baseWeights[6] *= 1.4f;
            }
            if (playerIsCamping)
            {
                baseWeights[5] *= 2f;
                baseWeights[3] *= 1.6f;
            }
            if (pc.score > 100)
            {
                baseWeights[1] *= 1.5f;
                baseWeights[4] *= 1.3f;
            }
        }

        float[] cumulative = new float[baseWeights.Length];
        float sum = 0;
        for (int i = 0; i < baseWeights.Length; i++)
        {
            sum += baseWeights[i];
            cumulative[i] = sum;
        }
        return cumulative;
    }
}
