using UnityEngine;

// ----------- BULLET LOGIC -----------

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 2f;
    public int damage = 20;
    public bool isPlayer;

    private Vector3 direction;
    private Vector3 lastPosition;

    public TrailRenderer trail;

    public void SetDirection(Vector3 dir)
    {
        dir.y = 0f;
        direction = dir.normalized;
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
        speed *= Random.Range(0.7f, 1.3f);

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 newPosition = transform.position + direction * speed * Time.deltaTime;

        // Raycast from last to new position to catch all collisions
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, (newPosition - transform.position).magnitude))
        {
            if (isPlayer && hit.collider.CompareTag("Enemy"))
            {
                var enemy = hit.collider.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                    Debug.Log("Raycast Bullet hit enemy: " + hit.collider.name);
                }
                Destroy(gameObject);
                return;
            }
            // Add player, wall, or other logic as needed
        }

        transform.position = newPosition;
        lastPosition = transform.position;
    }

    // Backup kill (used by OnTriggerEnter)
    public void KillBullet()
    {
        var bulletTrail = GetComponent<TrailRenderer>();
        if (bulletTrail != null)
        {
            bulletTrail.transform.parent = null;
            bulletTrail.autodestruct = true;
            bulletTrail.emitting = false;
        }
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            if (!isPlayer) return;
            var enemy = collision.GetComponent<EnemyAI>();
            if (enemy != null)
                enemy.TakeDamage(damage);
            KillBullet();
            return;
        }
        if (collision.CompareTag("Player"))
        {
            if (isPlayer) return;
            var player = collision.GetComponent<PlayerController>();
            if (player != null)
                player.TakeDamage(damage);
            KillBullet();
            return;
        }
        if (collision.CompareTag("Bullet") || collision.CompareTag("Floor"))
            return;

        KillBullet();
    }
}
