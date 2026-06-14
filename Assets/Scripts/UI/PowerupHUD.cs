using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using System.Collections.Generic;
using NaughtyAttributes;

[System.Serializable]
public struct PowerupIconMapping
{
    public string powerupName; 
    public string description; // <--- NEW: Added for the DOTween popup
    public Sprite icon;        
}

public class PowerupHUD : MonoBehaviour
{
    public static PowerupHUD Instance;

    [Header("HUD Container References")]
    public GameObject powerupUIPrefab; 
    public Transform powerupContainer; 

    [Header("Popup Announcement References")]
    [Tooltip("The DOTween popup prefab that appears in the center of the screen.")]
    public GameObject announcementPrefab; 
    [Tooltip("Usually just the HUDCanvas itself, so it spawns in the middle of the screen.")]
    public Transform announcementContainer;

    [Header("Icon Settings")]
    public List<PowerupIconMapping> iconMappings = new List<PowerupIconMapping>();

    private Dictionary<string, TMP_Text> activePowerups = new Dictionary<string, TMP_Text>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdatePowerupDisplay(string powerupName, float timeLeft, int shotsLeft, bool isTimeBased)
    {
        // 1. Deletion Phase
        if ((isTimeBased && timeLeft <= 0) || (!isTimeBased && shotsLeft <= 0))
        {
            if (activePowerups.ContainsKey(powerupName))
            {
                Destroy(activePowerups[powerupName].transform.parent.gameObject);
                activePowerups.Remove(powerupName);
            }
            return;
        }

        // 2. Spawning Phase (IT'S A NEW POWERUP!)
        if (!activePowerups.ContainsKey(powerupName))
        {
            // Look up the specific struct for this powerup to get the description and icon
            bool foundMapping = TryGetMapping(powerupName, out PowerupIconMapping mapping);

            // --- 2A. Trigger the Flashy DOTween Announcement ---
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

            // --- 2B. Spawn the actual HUD icon that stays at the bottom ---
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
        if (isTimeBased)
        {
            activePowerups[powerupName].text = $"{powerupName}\n{timeLeft:F1}s";
        }
        else
        {
            activePowerups[powerupName].text = $"{powerupName}\n{shotsLeft} Shots";
        }
    }

    // --- NEW: Helper method safely returns the whole struct instead of just the sprite ---
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
    [Tooltip("Type the exact name of a powerup from your Icon Mappings list to test it.")]
    public string testPowerupName = "Rapid Fire";
    public float testDuration = 10f;
    public int testShots = 0;
    public bool testIsTimeBased = true;

    [Button("Test Spawn Powerup")]
    private void TestSpawnPowerup()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("You must be in Play Mode to test the HUD animations!");
            return;
        }

        UpdatePowerupDisplay(testPowerupName, testDuration, testShots, testIsTimeBased);
    }
}