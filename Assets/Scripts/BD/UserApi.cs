using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class UserAPI : MonoBehaviour
{
    public static string baseUrl = "https://moving.cs.upc.edu/sensory_room_api/api/";

    private static string Url(string path)
    {
        return baseUrl.TrimEnd('/') + path;
    }

    [Serializable]
    private class CreateUserRequest
    {
        public string nom;
        public bool independent;
        public bool entorn_adult;
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
    }

    [Serializable]
    public class LastSessionElement
    {
        public string id_element;
        public int numero_posicio;
    }

    [Serializable]
    public class LastSessionResponse
    {
        public int id_sessio;
        public string postura_inicial;
        public string postura_final;
        public string postura_actual;
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
        public LastSessionElement[] vr_elements;
    }

    [Serializable]
    private class SessionSummary
    {
        public int id_sessio;
    }

    [Serializable]
    private class UserConfigResponse
    {
        public bool menu_mans_actiu;
        public bool particules_mans_actives;
    }

    public static IEnumerator GetUsers(Action<User[]> onSuccess, Action<string> onError = null)
    {
        string url = Url("/users");
        Debug.Log("[API] GET " + url);

        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;
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
            onError?.Invoke(BuildError(req));
        }
    }

    public static IEnumerator CreateUser(
        string nom,
        bool entornAdult,
        bool independent,
        bool menuMansActiu,
        bool particulesMansActives,
        Action onSuccess = null,
        Action<string> onError = null)
    {
        CreateUserRequest data = new CreateUserRequest
        {
            nom = nom,
            independent = independent,
            entorn_adult = entornAdult,
            menu_mans_actiu = menuMansActiu,
            particules_mans_actives = particulesMansActives
        };

        string json = JsonUtility.ToJson(data);

        using UnityWebRequest req = new UnityWebRequest(Url("/users"), "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    public static IEnumerator CreateUser(string nom, int edat, bool independent, Action onSuccess = null, Action<string> onError = null)
    {
        yield return CreateUser(nom, false, independent, true, true, onSuccess, onError);
    }

    public static IEnumerator GetLastSession(int userId, Action<LastSessionResponse> onSuccess, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(Url("/users/" + userId + "/last-session"));
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                LastSessionResponse session = JsonUtility.FromJson<LastSessionResponse>(req.downloadHandler.text);
                onSuccess?.Invoke(session);
            }
            catch (Exception e)
            {
                onError?.Invoke("Error parseando ultima sesion: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
            }
        }
        else if (req.responseCode == 404)
        {
            yield return GetLastSessionFromServerEndpoints(userId, onSuccess, onError);
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    public static IEnumerator DeleteUser(int userId, Action onSuccess = null, Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Delete(Url("/users/" + userId));
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke();
        }
        else
        {
            onError?.Invoke(BuildError(req));
        }
    }

    private static IEnumerator GetLastSessionFromServerEndpoints(int userId, Action<LastSessionResponse> onSuccess, Action<string> onError)
    {
        SessionSummary[] sessions = null;
        string sessionsError = null;

        yield return GetJsonArray<SessionSummary>(
            Url("/users/" + userId + "/sessions"),
            result => sessions = result,
            err => sessionsError = err
        );

        if (!string.IsNullOrEmpty(sessionsError))
        {
            onError?.Invoke(sessionsError);
            yield break;
        }

        if (sessions == null || sessions.Length == 0 || sessions[0].id_sessio <= 0)
        {
            onSuccess?.Invoke(null);
            yield break;
        }

        LastSessionResponse session = new LastSessionResponse
        {
            id_sessio = sessions[0].id_sessio,
            postura_inicial = "DE_PIE",
            postura_final = "DE_PIE",
            postura_actual = "DE_PIE",
            menu_mans_actiu = true,
            particules_mans_actives = true
        };

        LastSessionElement[] vrElements = null;
        string vrError = null;

        yield return GetJsonArray<LastSessionElement>(
            Url("/sessions/" + session.id_sessio + "/vr-elements"),
            result => vrElements = result,
            err => vrError = err
        );

        if (!string.IsNullOrEmpty(vrError))
        {
            onError?.Invoke(vrError);
            yield break;
        }

        session.vr_elements = vrElements;

        UserConfigResponse config = null;
        string configError = null;

        yield return GetJsonObject<UserConfigResponse>(
            Url("/users/" + userId + "/config"),
            result => config = result,
            err => configError = err
        );

        if (string.IsNullOrEmpty(configError) && config != null)
        {
            session.menu_mans_actiu = config.menu_mans_actiu;
            session.particules_mans_actives = config.particules_mans_actives;
        }

        onSuccess?.Invoke(session);
    }

    private static IEnumerator GetJsonArray<T>(string url, Action<T[]> onSuccess, Action<string> onError)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(BuildError(req));
            yield break;
        }

        try
        {
            onSuccess?.Invoke(JsonHelper.FromJson<T>(req.downloadHandler.text));
        }
        catch (Exception e)
        {
            onError?.Invoke("Error parseando respuesta: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
        }
    }

    private static IEnumerator GetJsonObject<T>(string url, Action<T> onSuccess, Action<string> onError)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        req.timeout = 10;
        yield return req.SendWebRequest();

        if (req.responseCode == 404 || req.downloadHandler.text == "null")
        {
            onSuccess?.Invoke(default);
            yield break;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(BuildError(req));
            yield break;
        }

        try
        {
            onSuccess?.Invoke(JsonUtility.FromJson<T>(req.downloadHandler.text));
        }
        catch (Exception e)
        {
            onError?.Invoke("Error parseando respuesta: " + e.Message + "\nRespuesta: " + req.downloadHandler.text);
        }
    }

    private static string BuildError(UnityWebRequest req)
    {
        string errorMsg = req.error;

        if (!string.IsNullOrEmpty(req.downloadHandler.text))
            errorMsg += "\n" + req.downloadHandler.text;

        return errorMsg;
    }
}
