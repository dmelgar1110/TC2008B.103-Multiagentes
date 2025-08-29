using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json.Linq;

public class TCPIPServer : MonoBehaviour
{
    public GameObject cellPrefab;
    public GameObject dirtPrefab;
    public GameObject agentPrefab;

    private GameObject agent;
    private GameObject[,] dirtGrid;
    private int rows, cols;

    private Thread SocketThread;
    private volatile bool keepReading = false;

    private Socket listener;
    private Socket handler;

    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    void Start()
    {
        Application.runInBackground = true;
        StartServer();
    }

    void StartServer()
    {
        SocketThread = new Thread(NetworkCode);
        SocketThread.IsBackground = true;
        SocketThread.Start();
    }

    private void NetworkCode()
    {
        byte[] buffer = new byte[4096];

        IPAddress IPAdr = IPAddress.Parse("127.0.0.1");
        IPEndPoint localEndPoint = new IPEndPoint(IPAdr, 1104);

        listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(10);
            Debug.Log("Servidor iniciado");

            while (true)
            {
                keepReading = true;
                handler = listener.Accept();
                Debug.Log("Cliente conectado");

                // Enviar mensaje inicial al cliente
                byte[] SendBytes = System.Text.Encoding.Default.GetBytes("I will send key\n");
                handler.Send(SendBytes);

                string data = "";

                while (keepReading)
                {
                    if (handler.Available > 0)
                    {
                        int bytesRec = handler.Receive(buffer);
                        if (bytesRec <= 0)
                        {
                            keepReading = false;
                            handler.Disconnect(true);
                            break;
                        }

                        data += System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRec);
                        string[] messages = data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var msg in messages)
                        {
                            messageQueue.Enqueue(msg);

                            try
                            {
                                JObject json = JObject.Parse(msg);
                                string type = json["type"].ToString();
                                if (type == "end" || msg.Contains("$"))
                                    keepReading = false;
                            }
                            catch { }
                        }

                        data = ""; // limpiar buffer después de procesar
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }

                handler.Close();
                Debug.Log("Cliente desconectado");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }

    void Update()
    {
        while (messageQueue.TryDequeue(out string msg))
        {
            Debug.Log("Servidor recibe: " + msg);
            ProcessMessage(msg);
        }
    }

    void ProcessMessage(string str)
    {
        JObject data = JObject.Parse(str);
        string type = data["type"].ToString();

        if (type == "setup")
        {
            JArray sucias = (JArray)data["sucias"];
            rows = sucias.Count;
            cols = sucias[0].ToObject<JArray>().Count;
            dirtGrid = new GameObject[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Vector3 pos = new Vector3(i, 0, j);
                    Instantiate(cellPrefab, pos, Quaternion.identity);

                    if (sucias[i][j].ToObject<bool>() == false)
                        dirtGrid[i, j] = Instantiate(dirtPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
                }
            }

            agent = Instantiate(agentPrefab, new Vector3(0, 1f, 0), Quaternion.identity);
        }
        else if (type == "step")
        {
            int x = data["x"].ToObject<int>();
            int y = data["y"].ToObject<int>();

            if (agent != null)
                agent.transform.position = new Vector3(x, 0.5f, y);

            if (dirtGrid != null && dirtGrid[x, y] != null)
            {
                Destroy(dirtGrid[x, y]);
                dirtGrid[x, y] = null;
            }
        }
        else if (type == "end")
        {
            StopServer();
        }
    }

    void OnDisable()
    {
        StopServer();
    }

    void StopServer()
    {
        keepReading = false;



        if (SocketThread != null && SocketThread.IsAlive)
            SocketThread.Abort();

        if (handler != null && handler.Connected)
        {
            handler.Disconnect(false);
            handler.Close();
        }

        if (listener != null)
        {
            listener.Close();
        }

        Debug.Log("Servidor detenido");
    }
}
