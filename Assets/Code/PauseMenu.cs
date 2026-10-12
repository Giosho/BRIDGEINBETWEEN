using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseUI;
    public KeyCode pauseKey = KeyCode.Escape;

    public static bool IsPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        Debug.Log("PAUSED");

        pauseUI.SetActive(true);
        Time.timeScale = 0f;
        IsPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        Debug.Log("Resi=med");

        pauseUI.SetActive(false);
        Time.timeScale = 1f;
        IsPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }
}