using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using TMPro;

public class PlayerListTab : NetworkBehaviour
{
    public GameObject tabPanel;
    public Transform listContainer;
    public GameObject playerSlotPrefab; 
    
    private Dictionary<ulong, GameObject> playerSlots = new Dictionary<ulong, GameObject>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) tabPanel.SetActive(true);
        if (Input.GetKeyUp(KeyCode.Tab)) tabPanel.SetActive(false);

        if (tabPanel.activeSelf) RefreshList();
    }

    void RefreshList()
    {
        // 1. Add/Update slots
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong id = client.ClientId;
            // Get the ping for this specific client
            ulong ping = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(id);

            // Find the player's score object to get name/score/color
            NetworkPlayerScore playerScore = null;
            if (client.PlayerObject != null)
                client.PlayerObject.TryGetComponent(out playerScore);

            if (!playerSlots.ContainsKey(id))
            {
                GameObject slot = Instantiate(playerSlotPrefab, listContainer);
                playerSlots.Add(id, slot);
            }

            // Update the text with the condensed format
            UpdateSlotText(playerSlots[id], id, playerScore, ping);
        }
        
        // 2. Cleanup disconnected
        List<ulong> toRemove = new List<ulong>();
        foreach (var id in playerSlots.Keys)
        {
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(id))
            {
                Destroy(playerSlots[id]);
                toRemove.Add(id);
            }
        }
        foreach (var id in toRemove) playerSlots.Remove(id);
    }

    void UpdateSlotText(GameObject slot, ulong id, NetworkPlayerScore scoreObj, ulong ping)
    {
        string displayName = $"Player {id}";
        string hexColor = "FFFFFF";
        string score = "0";

        // Pull data from the score object if it exists
        if (scoreObj != null)
        {
            score = scoreObj.currentScore.Value.ToString();
            
            if (scoreObj.TryGetComponent(out PlayerColorManager cm) && cm.colorSets.Length > 0)
            {
                var activeSet = cm.colorSets[cm.colorIndex.Value % cm.colorSets.Length];
                displayName = activeSet.themeName;
                hexColor = ColorUtility.ToHtmlStringRGB(activeSet.playerColor);
            }
        }

        // Add "(YOU)" tag
        string youTag = (id == NetworkManager.Singleton.LocalClientId) ? " (YOU)" : "";

        // Format: Name | Score | Ping
        slot.GetComponent<TMP_Text>().text = 
            $"<color=#{hexColor}>{displayName}{youTag}</color> | Score: {score} | {ping}ms";
    }
}