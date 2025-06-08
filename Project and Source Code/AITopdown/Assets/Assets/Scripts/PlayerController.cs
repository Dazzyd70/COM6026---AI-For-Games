using UnityEngine;
using UnityEngine.UI; // For score UI, if you want to display it
using System.Collections;
using UnityEngine.SceneManagement;

// ----------- PLAYER CONTROLLER -----------

public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class Weapon
    {
        public string weaponName;
        public GameObject bulletPrefab;
        public Transform firePoint;
        public int bulletsPerShot = 1;
        public float fireRate = 0.4f;
        public float spread = 2.5f;
        public int damage = 15;
        public float bulletSpeed = 14f;
        public bool isGrenadeLauncher;
        public float grenadeLaunchForce = 18f;
    }

    public Weapon[] weapons;
    private int currentWeaponIndex = 0;
    private float lastFireTime = -999f;

    public int maxHealth = 100;
    public int currentHealth;
    public ScreenVignetteFlash vignetteFlash;
    public bool isDead = false;
    public int score = 0;

    public float moveSpeed = 6f;
    private Rigidbody rb;
    private Vector3 movement;

    public CanvasGroup deathScreenUI;
    public DeathScreenUI DSUI;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;
        Time.timeScale = 1;

        // Fallback for missing firepoint
        if (weapons.Length > 0 && weapons[0].firePoint == null)
            weapons[0].firePoint = transform;
    }

    void Update()
    {
        if (isDead)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        // Input for movement
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        movement = new Vector3(moveX, 0, moveZ).normalized;

        FaceTowardsMouse();

        // Weapon switching (Q/E or mouse wheel)
        if (Input.GetKeyDown(KeyCode.Q)) CycleWeapon(-1);
        if (Input.GetKeyDown(KeyCode.E)) CycleWeapon(1);
        if (Input.mouseScrollDelta.y != 0)
            CycleWeapon((int)Mathf.Sign(Input.mouseScrollDelta.y));

        // Shooting
        if (Input.GetButton("Fire1") && Time.time > lastFireTime + weapons[currentWeaponIndex].fireRate)
        {
            Shoot();
            lastFireTime = Time.time;
        }
    }

    void FixedUpdate()
    {
        // Smooth acceleration
        Vector3 targetVel = movement * moveSpeed;
        rb.velocity = Vector3.Lerp(rb.velocity, targetVel, 0.25f);

        // Stay on XZ plane
        rb.position = new Vector3(rb.position.x, 0.5f, rb.position.z);
    }

    void FaceTowardsMouse()
    {
        // Raycast from mouse to world, Y plane at player height
        Plane plane = new Plane(Vector3.up, transform.position);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float distance;
        if (plane.Raycast(ray, out distance))
        {
            Vector3 lookPoint = ray.GetPoint(distance);
            Vector3 dir = (lookPoint - transform.position).normalized;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 0.22f);
            }
        }
    }

    void Shoot()
    {
        Weapon w = weapons[currentWeaponIndex];
        Transform fp = w.firePoint != null ? w.firePoint : transform;

        if (w.isGrenadeLauncher)
        {
            Vector3 shootDir = GetMouseAimDirection(fp.position, 0);
            Vector3 grenadeDir = shootDir.normalized;

            GameObject grenade = Instantiate(w.bulletPrefab, fp.position + shootDir * 0.2f, Quaternion.identity);
            Grenade grenadeScript = grenade.GetComponent<Grenade>();
            if (grenadeScript)
            {
                grenadeScript.isPlayer = true;
                grenadeScript.initialVelocity = grenadeDir * w.grenadeLaunchForce;
            }
            Physics.IgnoreCollision(grenade.GetComponent<Collider>(), GetComponent<Collider>());

            PlayerHeatmap.Instance.RegisterAttack(transform.position);
            return;
        }

        // Normal bullet(s)
        for (int i = 0; i < w.bulletsPerShot; i++)
        {
            Vector3 shootDir = GetMouseAimDirection(fp.position, w.spread);
            GameObject bullet = Instantiate(w.bulletPrefab, fp.position + shootDir * 0.1f, Quaternion.identity);
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript)
            {
                bulletScript.SetDirection(shootDir);
                bulletScript.damage = w.damage;
                bulletScript.speed = w.bulletSpeed;
                bulletScript.isPlayer = true;
            }
            Physics.IgnoreCollision(bullet.GetComponent<Collider>(), GetComponent<Collider>());
        }

        PlayerHeatmap.Instance.RegisterAttack(transform.position);
    }

    Vector3 GetMouseAimDirection(Vector3 from, float spread = 0)
    {
        Plane plane = new Plane(Vector3.up, from);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float distance;
        Vector3 targetPoint = from + transform.forward;
        if (plane.Raycast(ray, out distance))
            targetPoint = ray.GetPoint(distance);

        Vector3 dir = (targetPoint - from).normalized;
        dir = Quaternion.Euler(0, Random.Range(-spread, spread), 0) * dir;
        return dir;
    }

    void CycleWeapon(int dir)
    {
        currentWeaponIndex = (currentWeaponIndex + weapons.Length + dir) % weapons.Length;
        Debug.Log("Switched to: " + weapons[currentWeaponIndex].weaponName);
    }

    public void AddScore(int amount)
    {
        score += amount;
    }

    public string GetCurrentWeaponName()
    {
        return weapons[currentWeaponIndex].weaponName;
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (vignetteFlash != null)
            vignetteFlash.Flash();

        if (currentHealth <= 0)
        {
            isDead = true;
            StartCoroutine(DeathSequence());
            StartCoroutine(FadeInDeathUI(deathScreenUI, 5f));
        }
    }

    IEnumerator DeathSequence()
    {
        float slowTime = 0.2f;
        float zoomTime = 5f;
        float t = 0;
        float startTimeScale = Time.timeScale;
        Camera cam = Camera.main;

        float startSize = cam.orthographicSize;
        float targetSize = startSize * 0.2f;

        while (t < Mathf.Max(slowTime, zoomTime))
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(startTimeScale, 0.12f, Mathf.Clamp01(t / slowTime));
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, Mathf.Clamp01(t / zoomTime));
            yield return null;
        }
        Time.timeScale = 0.12f;
        cam.orthographicSize = targetSize;
    }

    IEnumerator FadeInDeathUI(CanvasGroup deathUI, float fadeDuration)
    {
        float timer = 0f;
        DSUI.Show(score);
        deathUI.alpha = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            deathUI.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            yield return null;
        }
        deathUI.alpha = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public int GetCurrentHealth() => currentHealth;
}
