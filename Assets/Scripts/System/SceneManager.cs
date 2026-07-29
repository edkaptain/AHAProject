using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerEdit : MonoBehaviour
{
    public void ResetScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
