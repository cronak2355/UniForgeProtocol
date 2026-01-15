using System;
using System.IO;
using System.Net;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Uniforge.FastTrack.Editor
{
    [InitializeOnLoad]
    public class UniforgeServer
    {
        private static HttpListener _listener;
        private static Thread _serverThread;
        private static bool _isRunning;
        private const int PORT = 7777;
        
        // Critical: Main thread data exchange
        private static string _pendingData;
        private static readonly object _lock = new object();

        static UniforgeServer()
        {
            // Clean up previous instances on reload
            StopServer();
            
            // Hook into Editor Update
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
            EditorApplication.quitting += StopServer;

            StartServer();
        }

        private static void StartServer()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{PORT}/import/");
                _listener.Start();
                _isRunning = true;

                _serverThread = new Thread(ListenLoop);
                _serverThread.Start();

                Debug.Log($"<color=cyan>[UniforgeServer]</color> Listening on port {PORT}...");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UniforgeServer] Verify Port {PORT}: {e.Message}");
                _isRunning = false;
            }
        }

        public static void StopServer()
        {
            _isRunning = false;
            
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }
        }

        private static void ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext(); // Blocking call
                    ThreadPool.QueueUserWorkItem((_) => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UniforgeServer] Loop Error: {e.Message}");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // Add CORS Headers
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                if (req.HttpMethod == "OPTIONS")
                {
                    res.StatusCode = 200;
                    res.Close();
                    return;
                }

                if (req.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    {
                        string json = reader.ReadToEnd();
                        
                        lock (_lock)
                        {
                            _pendingData = json;
                        }
                    }

                    res.StatusCode = 200;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes("OK");
                    res.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    res.StatusCode = 405; // Method Not Allowed
                }
            }
            catch (Exception e)
            {
                res.StatusCode = 500;
                Debug.LogError($"[UniforgeServer] Handle Error: {e.Message}");
            }
            finally
            {
                res.Close();
            }
        }

        // Main Thread Update
        private static void Update()
        {
            string dataToProcess = null;

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_pendingData))
                {
                    dataToProcess = _pendingData;
                    _pendingData = null;
                }
            }

            if (dataToProcess != null)
            {
                UniforgeImporter.ImportFromJson(dataToProcess);
            }
        }

        [MenuItem("Uniforge/Restart Server")]
        public static void RestartServerMenu()
        {
            StopServer();
            StartServer();
            Debug.Log("[UniforgeServer] Manual Restart Triggered");
        }

        [MenuItem("Uniforge/Check Status")]
        public static void CheckStatusMenu()
        {
            Debug.Log($"[UniforgeServer] IsRunning: {_isRunning}, Listener: {(_listener != null ? "Active" : "Null")}");
        }
    }
}
