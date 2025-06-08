using UnityEngine;
using System.Collections.Generic;

// ----------- PLAYER PATTERN TRACKER -----------

public class PlayerPatternTracker : MonoBehaviour
{
    public static PlayerPatternTracker Instance;
    public int bufferSize = 24;
    private Queue<Vector3> recentDirections = new Queue<Vector3>();
    private Vector3 lastPosition;
    public float patternCheckInterval = 0.3f;
    private float lastPatternTime = 0;

    void Awake()
    {
        Instance = this;
        lastPosition = transform.position;
    }

    void Update()
    {
        if (Time.time > lastPatternTime + patternCheckInterval)
        {
            Vector3 movement = (transform.position - lastPosition).normalized;
            if (movement.magnitude > 0.1f) // ignore idle
            {
                if (recentDirections.Count >= bufferSize) recentDirections.Dequeue();
                recentDirections.Enqueue(movement);
            }
            lastPosition = transform.position;
            lastPatternTime = Time.time;
        }
    }

    // How much is the player moving in a straight line
    public float GetMovementStraightness()
    {
        if (recentDirections.Count < 8) return 0f;
        Vector3 avg = Vector3.zero;
        foreach (var v in recentDirections) avg += v;
        avg /= recentDirections.Count;

        float alignment = 0f;
        foreach (var v in recentDirections) alignment += Vector3.Dot(v, avg.normalized);
        alignment /= recentDirections.Count;
        return alignment; // Close to 1 if all same dir, 0 if random
    }

    // How much is the player strafing (side-to-side) vs. moving forward
    public float GetLateralMovementRatio()
    {
        if (recentDirections.Count < 8) return 0f;
        // Assume forward is +Z for player
        Vector3 forward = Vector3.forward;
        float sideSum = 0f, forwardSum = 0f;
        foreach (var v in recentDirections)
        {
            sideSum += Mathf.Abs(Vector3.Dot(v, Vector3.right));
            forwardSum += Mathf.Abs(Vector3.Dot(v, forward));
        }
        return sideSum / (forwardSum + 0.01f);
    }

    // Get the average direction of recent movement
    public Vector3 GetAverageDirection()
    {
        if (recentDirections.Count == 0) return Vector3.zero;
        Vector3 avg = Vector3.zero;
        foreach (var v in recentDirections) avg += v;
        avg /= recentDirections.Count;
        return avg.normalized;
    }
}
