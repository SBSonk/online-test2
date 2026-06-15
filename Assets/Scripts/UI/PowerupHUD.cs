using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System.Collections.Generic;
using NaughtyAttributes;

[System.Serializable]
public struct PowerupIconMapping
{
    public string powerupName; 
    public string description;
    public Sprite icon;        
}

public class PowerupHUD : MonoBehaviour
{
    public static PowerupHUD Instance;

    [Header("HUD Container References")]
    public GameObject powerupUIPrefab; 
    public Transform powerupContainer; 

    [Header("Popup Announcement References")]
    public GameObject announcementPrefab; 
    public Transform announcementContainer;

    [Header("Icon Settings")]
    public List<PowerupIconMapping> iconMappings = new List<PowerupIconMapping>();

    private Dictionary<string, TMP_Text> activePowerups = new Dictionary<string, TMP_Text>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // --- THE FIX: Simplified parameters to strictly handle Time! ---
    public void UpdatePowerupDisplay(string powerupName, float timeLeft)
    {
        // 1. Deletion Phase
        if (timeLeft <= 0)
        {
            if (activePowerups.ContainsKey(powerupName))
            {
                Destroy(activePowerups[powerupName].transform.parent.gameObject);
                activePowerups.Remove(powerupName);
            }
            return;
        }

        // 2. Spawning Phase
        if (!activePowerups.ContainsKey(powerupName))
        {
            bool foundMapping = TryGetMapping(powerupName, out PowerupIconMapping mapping);

            if (announcementPrefab != null && announcementContainer != null)
            {
                GameObject popup = Instantiate(announcementPrefab, announcementContainer);
                if (popup.TryGetComponent(out PowerupAnnouncement announcementScript))
                {
                    string desc = foundMapping ? mapping.description : "Powerup Acquired!";
                    Sprite spr = foundMapping ? mapping.icon : null;
                    announcementScript.Show(powerupName, desc, spr);
                }
            }

            GameObject newUI = Instantiate(powerupUIPrefab, powerupContainer);
            TMP_Text textComponent = newUI.GetComponentInChildren<TMP_Text>();
            activePowerups.Add(powerupName, textComponent);

            if (foundMapping)
            {
                Image iconImage = newUI.GetComponentInChildren<Image>();
                if (iconImage != null && mapping.icon != null)
                {
                    iconImage.sprite = mapping.icon;
                }
            }
        }

        // 3. Update Text Phase
        // --- THE FIX: Only display the raw time remaining! ---
        activePowerups[powerupName].text = $"{timeLeft:F1}s";
    }

    private bool TryGetMapping(string name, out PowerupIconMapping result)
    {
        foreach (var mapping in iconMappings)
        {
            if (mapping.powerupName == name)
            {
                result = mapping;
                return true;
            }
        }
        result = default;
        return false;
    }

    [Header("Debug & Testing")]
    public string testPowerupName = "Rapid Fire";
    public float testDuration = 10f;

    [Button("Test Spawn Powerup")]
    private void TestSpawnPowerup()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("You must be in Play Mode to test the HUD animations!");
            return;
        }

        UpdatePowerupDisplay(testPowerupName, testDuration);
    }
}