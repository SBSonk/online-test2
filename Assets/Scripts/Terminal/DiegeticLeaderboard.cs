using UnityEngine;
using TMPro;
using System.Linq;

public class DiegeticLeaderboard : MonoBehaviour
{
    public TMP_Text scoreListText;

    public void RefreshScores()
    {
        if (scoreListText == null) return;

        // Find all player score objects in the scene
        var allPlayers = FindObjectsByType<NetworkPlayerScore>();
        
        // Sort by score descending
        var sortedPlayers = allPlayers
            .OrderByDescending(p => p.currentScore.Value)
            .ToList();

        string displayText = "TOP SCORES\n";
        displayText += "---------------------------\n";
        displayText += "PLAYER | GAMES | SCORE\n";
        displayText += "---------------------------\n";
        
        foreach (var player in sortedPlayers)
        {
            string hexColor = "FFFFFF"; // Default
            string displayName = $"P{player.OwnerClientId}"; // Fallback if no name is found

            if (player.TryGetComponent(out PlayerColorManager colorManager))
            {
                int pIndex = colorManager.colorIndex.Value;
                var sets = colorManager.colorSets;
                
                if (sets.Length > 0)
                {
                    var activeSet = sets[pIndex % sets.Length];
                    
                    // 1. Grab the color
                    Color themeColor = activeSet.playerColor;
                    hexColor = ColorUtility.ToHtmlStringRGB(themeColor);
                    
                    // 2. Grab the name! 
                    if (!string.IsNullOrEmpty(activeSet.themeName))
                    {
                        displayName = activeSet.themeName;
                    }
                }
            }

            // Inject the dynamic name inside the color tags
            displayText += $"<color=#{hexColor}>{displayName}</color>  |  {player.gamesPlayed.Value}  |  {player.currentScore.Value}\n";
        }

        scoreListText.text = displayText;
    }
}