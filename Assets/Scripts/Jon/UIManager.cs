using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject connectionCanvas;
    public GameObject mainCanvas;
    public GameObject lightCanvas;
    public GameObject soundCanvas;
    public GameObject environmentCanvas;

    private void Start()
	{
        if (connectionCanvas == null)
            GoToMainCanvas();
        else
            GoToConnectionCanvas();
	}

	public void ShowCanvas(GameObject canvas) 
    {
        mainCanvas.SetActive(false);
		lightCanvas.SetActive(false);
		soundCanvas.SetActive(false);
		environmentCanvas.SetActive(false);
    
		canvas.SetActive(true);
    }

    public void GoToConnectionCanvas() => ShowCanvas(connectionCanvas);
    public void GoToMainCanvas() => ShowCanvas(mainCanvas);
    public void GoToLightCanvas() => ShowCanvas(lightCanvas);
    public void GoToSoundCanvas() => ShowCanvas(soundCanvas);
    public void GoToEnvironmentCanvas() => ShowCanvas(environmentCanvas);
}
