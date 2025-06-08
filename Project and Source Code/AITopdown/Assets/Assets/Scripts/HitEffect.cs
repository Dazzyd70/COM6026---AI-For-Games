using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// ----------- SCREEN VIGNETTE FLASH -----------

public class ScreenVignetteFlash : MonoBehaviour
{
    public Image vignetteImage;
    public float flashAlpha = 0.6f;
    public float fadeSpeed = 2.5f;

    private Coroutine flashRoutine;

    void Start()
    {
        if (vignetteImage)
            vignetteImage.color = new Color(1, 0, 0, 0);
    }

    public void Flash()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(DoFlash());
    }

    IEnumerator DoFlash()
    {
        if (!vignetteImage) yield break;
        vignetteImage.color = new Color(1, 0, 0, flashAlpha);

        float t = 0;
        while (t < 1)
        {
            float alpha = Mathf.Lerp(flashAlpha, 0, t);
            vignetteImage.color = new Color(1, 0, 0, alpha);
            t += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        vignetteImage.color = new Color(1, 0, 0, 0);
    }
}
