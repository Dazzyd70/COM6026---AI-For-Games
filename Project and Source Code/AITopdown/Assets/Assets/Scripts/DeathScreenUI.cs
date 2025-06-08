using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

// ----------- DEATH SCREEN UI -----------

public class DeathScreenUI : MonoBehaviour
{
    public TMP_Text killText;
    public GameObject panel;
    public PlayerController pc;

    private void Start()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void Show(int kills)
    {
        if (panel != null)
            panel.SetActive(true);
        if (killText != null)
            killText.text = $"You killed {kills}.";
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    // Restart button
    public void OnRestart()
    {
        Debug.Log("Restart pressed!");
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Quit button
    public void OnQuit()
    {
        Application.Quit();
    }
}
