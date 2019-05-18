using UnityEngine;
using UnityEngine.UI;

public class FPSDisplayer : MonoBehaviour
{
	public Text fpsText;
	public float deltaTime;

#if UNITY_EDITOR
	private void Update()
	{
		deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
		float fps = 1.0f / deltaTime;
		fpsText.text = $"FPS : {Mathf.Ceil(fps).ToString()}";
	}
#endif
}