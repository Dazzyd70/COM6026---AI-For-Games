using UnityEngine;
using System.Collections.Generic;

// ----------- PLAYER HEATMAP (PLAYER ACTIVITY TRACKING) -----------

public class PlayerHeatmap : MonoBehaviour
{
    public static PlayerHeatmap Instance;

    public float cellSize = 6f;
    public float heatDecayPerSecond = 0.08f;

    private Dictionary<Vector3Int, float> heat = new Dictionary<Vector3Int, float>();
    private Dictionary<Vector2Int, float> attackHeat = new Dictionary<Vector2Int, float>();
    private Dictionary<Vector2Int, float> pathHeat = new Dictionary<Vector2Int, float>();
    private Transform player;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
            player = playerObj.transform;
    }

    void Update()
    {
        if (!player) return;

        // Record current player cell
        Vector3 pos = player.position;
        Vector3Int cell = WorldToCell(pos);
        if (!heat.ContainsKey(cell)) heat[cell] = 0;
        heat[cell] += Time.deltaTime;

        // Decay all heat values over time
        List<Vector3Int> keys = new List<Vector3Int>(heat.Keys);
        foreach (var key in keys)
            heat[key] = Mathf.Max(0, heat[key] - heatDecayPerSecond * Time.deltaTime);
    }

    Vector3Int WorldToCell(Vector3 pos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / cellSize),
            0,
            Mathf.FloorToInt(pos.z / cellSize)
        );
    }

    Vector2Int WorldToCell2D(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / cellSize),
            Mathf.FloorToInt(pos.z / cellSize)
        );
    }

    public float GetHeat(Vector3 worldPos)
    {
        Vector3Int cell = WorldToCell(worldPos);
        return heat.ContainsKey(cell) ? heat[cell] : 0f;
    }

    public float GetAttackHeat(Vector3 pos)
    {
        Vector2Int cell = WorldToCell2D(pos);
        return attackHeat.TryGetValue(cell, out var h) ? h : 0f;
    }

    public float GetPathHeat(Vector3 pos)
    {
        Vector2Int cell = WorldToCell2D(pos);
        return pathHeat.TryGetValue(cell, out var h) ? h : 0f;
    }

    public void RegisterAttack(Vector3 pos, float amount = 1f)
    {
        Vector2Int cell = WorldToCell2D(pos);
        if (!attackHeat.ContainsKey(cell))
            attackHeat[cell] = 0f;
        attackHeat[cell] += amount;
    }

    public void RegisterPath(Vector3 pos, float amount = 1f)
    {
        Vector2Int cell = WorldToCell2D(pos);
        if (!pathHeat.ContainsKey(cell))
            pathHeat[cell] = 0f;
        pathHeat[cell] += amount;
    }

    void OnDrawGizmos()
    {
        if (heat == null) return;
        foreach (var kvp in heat)
        {
            if (kvp.Value > 0.2f)
            {
                Vector3 center = new Vector3(
                    kvp.Key.x * cellSize + cellSize / 2f,
                    0.2f,
                    kvp.Key.z * cellSize + cellSize / 2f
                );
                Gizmos.color = Color.Lerp(Color.blue, Color.red, kvp.Value / 5f);
                Gizmos.DrawCube(center, new Vector3(cellSize, 0.2f, cellSize));
            }
        }
    }
}
