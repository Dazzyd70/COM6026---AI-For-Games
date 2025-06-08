using UnityEngine;

// ----------- MEDIC HEAL PULSE -----------

public class MedicHealPulse : MonoBehaviour
{
    private Material mat;
    public float fadeSpeed = 1.2f;
    public float scaleTime = 0.22f;
    public float maxScale = 2.6f;

    private Vector3 initialScale;
    private float timer = 0;
    private Color startColor;
    private Color startEmission;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        initialScale = transform.localScale;
        transform.localScale = initialScale * 0.18f;

        // Setup material for alpha fading
        SetMaterialFadeMode(mat);

        // Set the color and emission
        startColor = new Color(0.25f, 1f, 0.5f, 0.46f); // Soft green
        mat.color = startColor;
        if (mat.HasProperty("_EmissionColor"))
        {
            startEmission = new Color(0.1f, 0.95f, 0.3f);
            mat.SetColor("_EmissionColor", startEmission);
        }
    }

    void Update()
    {
        if (timer < scaleTime)
        {
            float t = timer / scaleTime;
            transform.localScale = Vector3.Lerp(initialScale * 0.18f, initialScale * maxScale, t);
        }

        // Fade alpha only
        Color c = mat.color;
        c.a -= Time.deltaTime * fadeSpeed;
        c.a = Mathf.Clamp01(c.a);
        mat.color = c;

        // Emission stays full strength
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", startEmission);

        if (c.a <= 0)
            Destroy(gameObject);

        timer += Time.deltaTime;
    }

    // Ensure Standard Shader is in Fade mode
    void SetMaterialFadeMode(Material m)
    {
        if (m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode", 2); // 2 = Fade, 3 = Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
    }
}
