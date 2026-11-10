using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
	private GameObject configGO;

	public Text pointsText;

	public Slider slider;

	public List<GameObject> units;

	private int points;

	public int inRed;

	public float[] redAreaX = new float[2] { -0.5f, 0.5f };

	public float[] redAreaY = new float[2] { 0f, -1f };

	public float[] greenAreaX = new float[2] { 6.5f, -6.5f };

	public GameObject unitPrefab;

	private float timer;

	private float spawnTime = 3.15f;

	private void Awake()
	{
		configGO = GameObject.FindGameObjectWithTag("Config");
		slider.value = 1f;
		units = new List<GameObject>();
	}

	private void Update()
	{
		timer -= Time.deltaTime;
		slider.value -= 1f / spawnTime * Time.deltaTime;
		if (spawnTime <= 0.7f)
		{
			spawnTime = 0.7f;
		}
		else
		{
			spawnTime -= 0.3f * Time.deltaTime;
		}
		if (timer <= 0f)
		{
			points += CreateUnit(units);
			pointsText.text = points.ToString();
			if (points >= 116)
			{
				Victory();
			}
			timer = spawnTime;
			slider.value = 1f;
		}
	}

	private int CreateUnit(List<GameObject> units)
	{
		if (VerifyGreen())
		{
			float x = ((!(Random.Range(0f, 1f) >= 0.5f)) ? (-6.5f) : 6.5f);
			GameObject gameObject = Object.Instantiate(position: new Vector3(x, Random.Range(-5, 5), 0f), original: unitPrefab, rotation: Quaternion.identity);
			gameObject.GetComponent<UnitBehaviour>().setController(base.gameObject);
			units.Add(gameObject);
			return 1;
		}
		return SpawnInRed(units);
	}

	private int SpawnInRed(List<GameObject> units)
	{
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 2; j++)
			{
				bool flag = true;
				for (int k = 0; k < units.Count; k++)
				{
					if (units[k].transform.position.x == redAreaX[i] && units[k].transform.position.y == redAreaY[j])
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					GameObject gameObject = Object.Instantiate(position: new Vector3(redAreaX[i], redAreaY[j], 0f), original: unitPrefab, rotation: Quaternion.identity);
					gameObject.GetComponent<UnitBehaviour>().setController(base.gameObject);
					gameObject.GetComponent<UnitBehaviour>().setRed();
					gameObject.GetComponent<UnitBehaviour>().subSec();
					units.Add(gameObject);
					upInRed();
					return 1;
				}
			}
		}
		Defeat();
		return 0;
	}

	private bool VerifyGreen()
	{
		for (int i = 0; i < units.Count; i++)
		{
			for (int j = 0; j < 2; j++)
			{
				if (units[i].transform.position.x == greenAreaX[j])
				{
					return false;
				}
			}
		}
		return true;
	}

	public void upInRed()
	{
		inRed++;
		if (inRed > 2)
		{
			Defeat();
		}
	}

	public void downInRed()
	{
		inRed--;
	}

	public void Defeat()
	{
		for (int i = 0; i < units.Count; i++)
		{
			units[i].GetComponent<UnitBehaviour>().zeroSec();
		}
		configGO.GetComponent<ConfigController>().setPoints(points);
		SceneManager.LoadScene("Death");
	}

	public void Victory()
	{
		SceneManager.LoadScene("Victory");
	}
}
