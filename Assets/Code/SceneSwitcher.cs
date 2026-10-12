using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject optionsPanel; // Assign your Options UI panel here

    // Load a new scene
    public void GoToScene()
    {
        Cursor.visible = false; // Hide the mouse cursor
        Cursor.lockState = CursorLockMode.Locked; // Lock cursor to center
        SceneManager.LoadScene("EXPScene");
    }

    // Quit the game
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }

    // Toggle Options Menu mid-game
    public void ToggleOptions()
    {
        if (optionsPanel != null)
        {
            bool isActive = optionsPanel.activeSelf;
            optionsPanel.SetActive(!isActive); // Show/hide panel
        }
    }
}