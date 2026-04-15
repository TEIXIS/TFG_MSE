using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class UserAPI : MonoBehaviour
{
    public static string baseUrl = "http://192.168.137.1:8080";

    [Serializable]
    private class CreateUserRequest
    {
        public string nom;
        public int edat;
        public bool independent;
    }

    public static IEnumerator GetUsers(Action<User[]> onSuccess, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(baseUrl + "/users");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                User[] users = JsonHelper.FromJson<User>(req.downloadHandler.text);
                onSuccess?.Invoke(users);
            }
            catch (Exception e)
            {
                onError?.Invoke("Error parseando usuarios: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
            }
        }
        else
        {
            string errorMsg = req.error;
            if (!string.IsNullOrEmpty(req.downloadHandler.text))
                errorMsg += "\n" + req.downloadHandler.text;

            onError?.Invoke(errorMsg);
        }
    }

    public static IEnumerator CreateUser(string nom, int edat, bool independent, Action onSuccess = null, Action<string> onError = null)
    {
        CreateUserRequest data = new CreateUserRequest
        {
            nom = nom,
            edat = edat,
            independent = independent
        };

        string json = JsonUtility.ToJson(data);

        using UnityWebRequest req = new UnityWebRequest(baseUrl + "/users", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            string errorMsg = req.error;
            if (!string.IsNullOrEmpty(req.downloadHandler.text))
                errorMsg += "\n" + req.downloadHandler.text;

            onError?.Invoke(errorMsg);
        }
    }

    public static IEnumerator DeleteUser(int userId, Action onSuccess = null, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Delete(baseUrl + "/users/" + userId);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            string errorMsg = req.error;
            if (!string.IsNullOrEmpty(req.downloadHandler.text))
                errorMsg += "\n" + req.downloadHandler.text;

            onError?.Invoke(errorMsg);
        }
    }
}