using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayServers : MonoBehaviour
{
    public LANDiscovery lanDiscovery;
	public Connection connection;

	// UI panel where found servers will be displayed.
	public Transform contentPanel;

	// One of these will be put into contentPanel for each different server available.
	public GameObject optionButton;

	// Ad-hoc UI manager, only used to navigate to my main panel when a server device is selected.
	public UIManager uiManager;

	[Header("Display servers UI")]
	public GameObject loadingIcon;
	public TMP_Text loadingText;
	public TMP_Text feedbackText;
	public TMP_Text errorText;
	public string text_lookingForServers;
	public string text_connectingToServer;
	public string text_connectError;
	public string text_disconnectError;


	private float timer;
	private float maxTime = 1;

	private void Start()
	{
		loadingText.text = text_lookingForServers;
		loadingIcon.SetActive(true);
		loadingText.gameObject.SetActive(true);

		connection.RegisterOnDisconnectCallback(OnDisconnectError);
	}

	public void Update()
	{
		// Periodically remake the menu
		timer += Time.deltaTime;
		if (timer > maxTime)
		{
			timer = 0;
			int count = FillPanel(lanDiscovery.discoveredServers);

			if (count == 0)
			{
				loadingIcon.SetActive(true);
				loadingText.gameObject.SetActive(true);
			}
			else 
			{
				loadingIcon.SetActive(false);
				loadingText.gameObject.SetActive(false);
			}
		}
	}

	private IEnumerator FadeErrorTextOut() 
	{
		errorText.alpha = 1.0f;
		yield return new WaitForSeconds(4.0f);

		for (int i = 1; i <= 15; ++i) 
		{
			float opacity = 1f - (float)i / 15f;
			errorText.alpha = opacity;
			yield return new WaitForSeconds(0.2f);
		}

		errorText.gameObject.SetActive(false);
	}

	// Call when connecting to the server fails
	public void OnConnectionError() 
	{
		lanDiscovery.ResetState();
		loadingText.text = text_lookingForServers;
		feedbackText.gameObject.SetActive(false);
		ClearPanel();
		uiManager.GoToConnectionCanvas();
		errorText.text = text_connectError;
		errorText.gameObject.SetActive(true);
		StartCoroutine(FadeErrorTextOut());
	}

	// Call when the connection is closed by the server
	public void OnDisconnectError() 
	{
		lanDiscovery.ResetState();
		loadingText.text = text_lookingForServers;
		ClearPanel();
		uiManager.GoToConnectionCanvas();
		errorText.text = text_disconnectError;
		errorText.gameObject.SetActive(true);
		StartCoroutine(FadeErrorTextOut());
	}

	private void ClearPanel() 
	{
		foreach (Transform t in contentPanel)
			Destroy(t.gameObject);
	}

	private IEnumerator ConnectToServer(string ipAddress) 
	{
		feedbackText.text = text_connectingToServer;
		feedbackText.gameObject.SetActive(true);

		// This makes the code waits until the next call to Update(), making sure the message appears on screen
		// Also it gives some time to read the message, providing feedback
		yield return new WaitForSeconds(1.0f);

		// Exit this menu if connected
		if (connection.ConnectToServer(ipAddress))
		{
			feedbackText.gameObject.SetActive(false);
			uiManager.GoToMainCanvas();
		}
	}

	int FillPanel(ConcurrentDictionary<string, string> servers) 
	{
		// Remake the menu, definitely not the cleanest option but simpler

		// 1: Clean
		ClearPanel();

		int count = 0;

		// 2: Make
		foreach (var kv in lanDiscovery.discoveredServers)
		{
			++count;

			GameObject button = Instantiate(optionButton, contentPanel);
			button.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = kv.Key;
			string ipAddress = kv.Value;

			button.GetComponent<Button>().onClick.AddListener(() => 
			{
				StartCoroutine(ConnectToServer(ipAddress));				
			});
		}

		return count;
	}
}
