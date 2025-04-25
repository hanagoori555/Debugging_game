using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Загружает сцену по имени
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Выход из игры
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Game is exiting...");
    }
}
