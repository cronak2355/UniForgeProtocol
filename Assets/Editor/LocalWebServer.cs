using UnityEditor;
using UnityEngine;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;

public static class LocalWebServer
{
    private static HttpListener listener;
    private const int PORT = 7777;

    [MenuItem("Web/Start Local Import Server")]
    public static void StartServer()
    {
        if (listener != null && listener.IsListening)
        {
            Debug.Log("Server already running");
            return;
        }

        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{PORT}/");
        listener.Start();

        Debug.Log($" Local Server Started : http://localhost:{PORT}");

        Task.Run(ListenLoop);
    }

    [MenuItem("Web/Stop Local Import Server")]
    public static void StopServer()
    {
        if (listener == null) return;

        listener.Stop();
        listener.Close();
        listener = null;

        Debug.Log(" Local Server Stopped");
    }

    private static async Task ListenLoop()
    {
        while (listener != null && listener.IsListening)
        {
            var context = await listener.GetContextAsync();
            _ = Task.Run(() => HandleRequest(context));
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 200;
            res.Close();
            return;
        }

        if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/import")
        {
            using var reader = new StreamReader(req.InputStream);
            string json = reader.ReadToEnd();

            SaveJsonToStreamingAssets(json);

            res.StatusCode = 200;
            using var writer = new StreamWriter(res.OutputStream);
            writer.Write("OK");
            res.Close();
            return;
        }

        res.StatusCode = 404;
        res.Close();
    }

    private static void SaveJsonToStreamingAssets(string json)
    {
        string dir = Application.streamingAssetsPath;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, "scene.json");
        File.WriteAllText(path, json);

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            var importer = Object.FindFirstObjectByType<SceneImportManager>();
            importer?.SceneFromJson();
        });

        AssetDatabase.Refresh();
    }
}
