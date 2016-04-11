using UnityEngine;
using System.Collections;

public class CameraScript : MonoBehaviour {

    public RenderTexture renderTexture;

	void Start () {
	    renderTexture.width = NearestSuperiorPowerOf2(Mathf.RoundToInt(renderTexture.height * Screen.width / Screen.height));
	    Debug.Log("Setting render texture width:" + renderTexture.width);
	}
	
	// Update is called once per frame
	void Update () {
	
	}
	
	int NearestSuperiorPowerOf2(int n)
	{
	    return (int) Mathf.Pow(2, Mathf.Ceil(Mathf.Log(n) / Mathf.Log(2)));
	}
}
