using UnityEngine;
using UnityEngine.SceneManagement;

public class ConfigController : MonoBehaviour
{
	private int points;

	private void Awake()
	{
		Object.DontDestroyOnLoad(base.gameObject);
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.R))
		{
			EnterGame();
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			QuitGame();
		}
	}

	public void EnterGame()
	{
		SceneManager.LoadScene("Game");
	}

	public void QuitGame()
	{
		Application.Quit();
	}

	public void EnterTutorial()
	{
		SceneManager.LoadScene("Tutorial1");
	}

	public void setPoints(int newPoints)
	{
		points = newPoints;
	}

	public int getPoints()
	{
		return points;
	}
}
