using UnityEngine;

// ----------- START SCREEN FADE -----------

public class StartScreenFade : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public float fadeDuration = 1.5f;
    public float stayTime = 2f;

    void Start()
    {
        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        StartCoroutine(FadeOutRoutine());
    }

    System.Collections.IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(stayTime);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false); // Hides the canvas completely after fade
    }
}
