using UnityEngine;
using UnityEngine.UI;

public class UnitBehaviour : MonoBehaviour
{
	public GameObject controller;

	private float seconds;

	private float correction = 66.1465f;

	public Text secondsText;

	public RectTransform text;

	private bool mirrored;

	private bool red;

	private int movement = 1;

	private void Awake()
	{
		float x = base.gameObject.transform.position.x * correction;
		float y = base.gameObject.transform.position.y * correction;
		mirrored = VerifyMirroring();
		text.transform.Translate(x, y, 0f);
		seconds = 3f;
		secondsText.text = (seconds + 1f).ToString();
	}

	private void Update()
	{
		if (seconds > 0f)
		{
			seconds -= Time.deltaTime;
			secondsText.text = (seconds + 1f).ToString();
			Move(mirrored);
		}
		else if (!red)
		{
			IfRed();
		}
	}

	public void zeroSec()
	{
		seconds = 0f;
		secondsText.text = seconds.ToString();
	}

	public void subSec()
	{
		seconds = 1f;
		secondsText.text = (seconds + 1f).ToString();
	}

	private void Move(bool mirrored)
	{
		if (mirrored)
		{
			if (Input.GetKeyDown(KeyCode.RightArrow) && !Physics.Raycast(base.transform.position, -Vector3.right, 1f) && ((Physics.Raycast(base.transform.position, -Vector3.right, out var hitInfo, 2f) && hitInfo.collider.tag == "Wall") || !Physics.Raycast(base.transform.position, -Vector3.right, out hitInfo, 2f) || !hitInfo.transform.gameObject.GetComponent<UnitBehaviour>().getMirror()))
			{
				base.transform.Translate(-movement, 0f, 0f);
				text.transform.Translate(0f - correction, 0f, 0f);
				IfRed();
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow) && !Physics.Raycast(base.transform.position, Vector3.right, 1f) && ((Physics.Raycast(base.transform.position, Vector3.right, out var hitInfo2, 2f) && hitInfo2.collider.tag == "Wall") || !Physics.Raycast(base.transform.position, Vector3.right, out hitInfo2, 2f) || !hitInfo2.transform.gameObject.GetComponent<UnitBehaviour>().getMirror()))
			{
				base.transform.Translate(movement, 0f, 0f);
				text.transform.Translate(correction, 0f, 0f);
				IfRed();
			}
		}
		else
		{
			if (Input.GetKeyDown(KeyCode.RightArrow) && !Physics.Raycast(base.transform.position, Vector3.right, 1f))
			{
				base.transform.Translate(movement, 0f, 0f);
				text.transform.Translate(correction, 0f, 0f);
				IfRed();
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow) && !Physics.Raycast(base.transform.position, -Vector3.right, 1f))
			{
				base.transform.Translate(-movement, 0f, 0f);
				text.transform.Translate(0f - correction, 0f, 0f);
				IfRed();
			}
		}
		if (Input.GetKeyDown(KeyCode.UpArrow) && !Physics.Raycast(base.transform.position, Vector3.up, 1f))
		{
			base.transform.Translate(0f, movement, 0f);
			text.transform.Translate(0f, correction, 0f);
			IfRed();
		}
		if (Input.GetKeyDown(KeyCode.DownArrow) && !Physics.Raycast(base.transform.position, -Vector3.up, 1f))
		{
			base.transform.Translate(0f, -movement, 0f);
			text.transform.Translate(0f, 0f - correction, 0f);
			IfRed();
		}
	}

	private bool VerifyMirroring()
	{
		if (base.transform.position.x < 0f)
		{
			return false;
		}
		return true;
	}

	public bool getMirror()
	{
		if (mirrored && seconds > 0f)
		{
			return true;
		}
		return false;
	}

	public void setRed()
	{
		red = true;
	}

	private void IfRed()
	{
		float[] array = new float[2] { -0.5f, 0.5f };
		float[] array2 = new float[2] { 0f, -1f };
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 2; j++)
			{
				if (base.transform.position.x == array[i] && base.transform.position.y == array2[j] && !red)
				{
					red = true;
					controller.GetComponent<PlayerController>().upInRed();
					return;
				}
			}
		}
		if (red)
		{
			red = false;
			controller.GetComponent<PlayerController>().downInRed();
		}
	}

	public void setController(GameObject newController)
	{
		controller = newController;
	}
}
