using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialEnd : MonoBehaviour
{
	public void Play()
	{
		SceneManager.LoadScene("Game");
	}

	public void ReturnMenu()
	{
		Object.Destroy(GameObject.FindWithTag("Config"));
		SceneManager.LoadScene("Menu");
	}

	public void EnterTutorial2()
	{
		SceneManager.LoadScene("Tutorial2");
	}

	public void EnterTutorial3()
	{
		SceneManager.LoadScene("Tutorial3");
	}
}
