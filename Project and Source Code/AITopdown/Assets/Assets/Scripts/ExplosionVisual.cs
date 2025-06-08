using UnityEngine;

// ----------- EXPLOSION VISUAL FADE -----------

public class ExplosionVisualFade : MonoBehaviour
{
    private Material mat;
    public float fadeSpeed = 1.7f;
    public float scaleTime = 0.18f;
    public float maxScale = 2.5f;

    private Vector3 initialScale;
    private float timer = 0f;
    private Color startColor;
    private Color startEmission;

    void Start()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("ExplosionVisualFade: No Renderer found!");
            enabled = false;
            return;
        }

        mat = renderer.material;
        initialScale = transform.localScale;
        transform.localScale = initialScale * 0.25f;
        startColor = mat.color;

        if (mat.HasProperty("_EmissionColor"))
            startEmission = mat.GetColor("_EmissionColor");
    }

    void Update()
    {
        if (timer < scaleTime)
        {
            float t = timer / scaleTime;
            transform.localScale = Vector3.Lerp(initialScale * 0.25f, initialScale * maxScale, t);
        }

        // Fade main color alpha
        if (mat != null)
        {
            Color c = mat.color;
            c.a -= Time.deltaTime * fadeSpeed;
            mat.color = c;

            // Fade emission brightness in sync with alpha
            if (mat.HasProperty("_EmissionColor"))
            {
                float emissionFade = Mathf.Clamp01(c.a / startColor.a);
                Color fadedEmission = startEmission * emissionFade;
                mat.SetColor("_EmissionColor", fadedEmission);
            }

            if (c.a <= 0f)
                Destroy(gameObject);
        }

        timer += Time.deltaTime;
    }
}
