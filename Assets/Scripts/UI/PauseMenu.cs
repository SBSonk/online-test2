using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set;}
    public GameObject pausePanel;
    public GameObject gameHUD; // --- NEW: Assign your HUD/Canvas here! ---

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        IsPaused = !pausePanel.activeSelf;
        pausePanel.SetActive(IsPaused);
        
        if (gameHUD != null) gameHUD.SetActive(!IsPaused);
        
        Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = IsPaused;
    }

    public void LeaveGame()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene("Game"); 
    }
}