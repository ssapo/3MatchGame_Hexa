using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFramerate : MonoBehaviour
{

	public int targetFramerate = 60;

	private void Start()
	{
		Application.targetFrameRate = targetFramerate;
#if UNITY_STANDALONE
		Screen.SetResolution(50 * 9, 50 * 16, false);
#endif
	}
}
