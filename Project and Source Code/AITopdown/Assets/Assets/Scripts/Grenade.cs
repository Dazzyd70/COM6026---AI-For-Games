using UnityEngine;

// ----------- GRENADE LOGIC -----------

public class Grenade : MonoBehaviour
{
    public float fuseTime = 2f;
    public float explosionRadius = 4f;
    public int explosionDamage = 40;
    public GameObject explosionVisualPrefab;
    public Vector3 initialVelocity = new Vector3(0, 0, 10);
    public bool isPlayer = false;

    private bool hasExploded = false;

    void Start()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.velocity = initialVelocity;
        Invoke(nameof(Explode), fuseTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        if (isPlayer && collision.collider.CompareTag("Enemy"))
        {
            Explode(); // explode instantly on direct enemy hit
            Debug.Log("Grenade collided with: " + collision.collider.name);
        }
    }

    void Explode()
    {
        if (hasExploded) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            if (isPlayer && hit.CompareTag("Enemy"))
            {
                var enemy = hit.GetComponent<EnemyAI>();
                if (enemy != null)
                    enemy.TakeDamage(explosionDamage);
            }
            else if (!isPlayer && hit.CompareTag("Player"))
            {
                var player = hit.GetComponent<PlayerController>();
                if (player != null)
                    player.TakeDamage(explosionDamage);
            }
        }

        if (explosionVisualPrefab != null)
            Instantiate(explosionVisualPrefab, transform.position, Quaternion.identity);

        hasExploded = true;
        Destroy(gameObject);
    }
}
