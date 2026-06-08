using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

public class QuestCrashDiagnostics : MonoBehaviour
{
    private const string FileName = "quest_crash_diagnostics.log";
    private static readonly object SyncRoot = new object();
    private static string logPath;
    private static bool initialized;
    private static bool writing;

    public float heartbeatInterval = 5f;
    private float nextHeartbeat;

    public static string LogPath
    {
        get { return logPath; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (initialized)
            return;

        initialized = true;
        GameObject go = new GameObject("QuestCrashDiagnostics");
        DontDestroyOnLoad(go);
        go.AddComponent<QuestCrashDiagnostics>();
    }

    private void Awake()
    {
        logPath = Path.Combine(Application.persistentDataPath, FileName);
        WriteLine("BOOT", "Awake. path=" + logPath + " app=" + Application.identifier + " version=" + Application.version
            + " unity=" + Application.unityVersion + " device=" + SystemInfo.deviceModel
            + " graphics=" + SystemInfo.graphicsDeviceName + " systemMemoryMB=" + SystemInfo.systemMemorySize);

        Application.logMessageReceived += OnLogMessageReceived;
        Application.lowMemory += OnLowMemory;
        Application.quitting += OnQuitting;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnEnable()
    {
        WriteLine("LIFECYCLE", "QuestCrashDiagnostics OnEnable");
    }

    private void Start()
    {
        Log("QuestCrashDiagnostics Start. persistentDataPath=" + Application.persistentDataPath);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextHeartbeat)
            return;

        nextHeartbeat = Time.unscaledTime + Mathf.Max(1f, heartbeatInterval);
        WriteLine("HEARTBEAT", BuildState("alive"));
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        WriteLine("APP", "OnApplicationPause pause=" + pauseStatus + " " + BuildState(""));
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        WriteLine("APP", "OnApplicationFocus focus=" + hasFocus + " " + BuildState(""));
    }

    private void OnDisable()
    {
        WriteLine("LIFECYCLE", "QuestCrashDiagnostics OnDisable");
    }

    private void OnDestroy()
    {
        WriteLine("LIFECYCLE", "QuestCrashDiagnostics OnDestroy");
        Application.logMessageReceived -= OnLogMessageReceived;
        Application.lowMemory -= OnLowMemory;
        Application.quitting -= OnQuitting;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    public static void Log(string message)
    {
        WriteLine("BREADCRUMB", message);
        Debug.Log("[QDIAG] " + message);
    }

    public static void LogWarning(string message)
    {
        WriteLine("BREADCRUMB-WARN", message);
        Debug.LogWarning("[QDIAG] " + message);
    }

    public static void LogError(string message)
    {
        WriteLine("BREADCRUMB-ERROR", message);
        Debug.LogError("[QDIAG] " + message);
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (writing)
            return;

        string message = condition;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            message += "\n" + stackTrace;

        WriteLine("UNITY-" + type, message);
    }

    private static void OnLowMemory()
    {
        WriteLine("LOW_MEMORY", BuildState("Application.lowMemory"));
        Debug.LogWarning("[QDIAG] Application.lowMemory received. See persistent diagnostic log.");
    }

    private static void OnQuitting()
    {
        WriteLine("APP", "Application.quitting " + BuildState(""));
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteLine("UNHANDLED", "isTerminating=" + e.IsTerminating + " exception=" + e.ExceptionObject);
    }

    private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene current)
    {
        WriteLine("SCENE", "activeSceneChanged " + previous.name + " -> " + current.name + " " + BuildState(""));
    }

    private static string BuildState(string tag)
    {
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        long allocated = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        long reserved = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
        long mono = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);

        return tag
            + " frame=" + Time.frameCount
            + " t=" + Time.realtimeSinceStartup.ToString("F1")
            + " scene=" + scene.name
            + " loaded=" + scene.isLoaded
            + " allocMB=" + allocated
            + " reservedMB=" + reserved
            + " monoMB=" + mono
            + " focus=" + Application.isFocused;
    }

    private static void WriteLine(string category, string message)
    {
        lock (SyncRoot)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath))
                    logPath = Path.Combine(Application.persistentDataPath, FileName);

                writing = true;
                string directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string line = DateTime.UtcNow.ToString("o") + " [" + category + "] " + message + Environment.NewLine;
                File.AppendAllText(logPath, line, Encoding.UTF8);
            }
            catch
            {
                // Avoid crashing diagnostics while diagnosing a crash.
            }
            finally
            {
                writing = false;
            }
        }
    }
}
