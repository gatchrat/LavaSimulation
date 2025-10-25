using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public void LoadSPH2D()
    {
        SceneManager.LoadScene("2D");
    }
    public void LoadSPH()
    {
        SceneManager.LoadScene("SPH");
    }
    public void LoadShuriken()
    {
        SceneManager.LoadScene("UnityPartikles");
    }public void LoadAlembic()
    {
        SceneManager.LoadScene("SampleScene");
    }
}
