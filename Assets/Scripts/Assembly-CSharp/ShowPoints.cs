using UnityEngine;
using UnityEngine.UI;

public class ShowPoints : MonoBehaviour
{
	public Text text;

	private GameObject configGO;

	private void Awake()
	{
		configGO = GameObject.FindGameObjectWithTag("Config");
		int points = configGO.GetComponent<ConfigController>().getPoints();
		text.text = points.ToString();
	}
}
