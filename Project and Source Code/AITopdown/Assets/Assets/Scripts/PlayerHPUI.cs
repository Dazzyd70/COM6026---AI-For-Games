using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ----------- PLAYER HEALTH & WEAPON UI -----------

public class PlayerHealthUI : MonoBehaviour
{
    public PlayerController player;
    public Image healthBarFill;
    public TMP_Text weaponText;

    void Update()
    {
        if (!player || !healthBarFill || !weaponText)
            return;

        // Health Bar
        float healthPercent = Mathf.Clamp01((float)player.GetCurrentHealth() / player.maxHealth);
        healthBarFill.fillAmount = healthPercent;

        // Weapon Type
        weaponText.text = player.GetCurrentWeaponName();
    }
}
