using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class DiscordRichPresenceClient : MonoBehaviour
{
    private const string ClientId = "1500836098648313876";
    private const int DiscordRpcVersion = 1;

    private static DiscordRichPresenceClient instance;

    private NamedPipeClientStream pipe;
    private Thread readThread;
    private volatile bool isRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
#if !UNITY_WEBGL
        if (instance != null)
        {
            return;
        }

        var go = new GameObject(nameof(DiscordRichPresenceClient));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DiscordRichPresenceClient>();
#endif
    }

    private void Start()
    {
#if !UNITY_WEBGL
        StartCoroutine(ConnectAndSetActivity());
#endif
    }

    private void OnApplicationQuit()
    {
        ClearActivity();
        Disconnect();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        Disconnect();
    }

    private IEnumerator ConnectAndSetActivity()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer &&
            Application.platform != RuntimePlatform.WindowsEditor)
        {
            yield break;
        }

        Disconnect();

        for (var i = 0; i < 10; i++)
        {
            var candidate = new NamedPipeClientStream(
                ".",
                "discord-ipc-" + i,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            var connected = false;
            try
            {
                candidate.Connect(100);
                pipe = candidate;
                isRunning = true;
                StartReadLoop();
                SendHandshake();
                connected = true;
            }
            catch
            {
                candidate.Dispose();
            }

            if (!connected)
            {
                continue;
            }

            yield return new WaitForSeconds(0.5f);
            SetActivity();
            yield break;
        }

        UnityEngine.Debug.Log("Discord Rich Presence: Discord IPC pipe was not found.");
    }

    private void StartReadLoop()
    {
        readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "Discord Rich Presence IPC"
        };
        readThread.Start();
    }

    private void ReadLoop()
    {
        var header = new byte[8];

        while (isRunning && pipe != null && pipe.IsConnected)
        {
            try
            {
                if (!ReadExactly(header, header.Length))
                {
                    break;
                }

                var length = BitConverter.ToInt32(header, 4);
                if (length <= 0)
                {
                    continue;
                }

                var payload = new byte[length];
                if (!ReadExactly(payload, payload.Length))
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }
    }

    private bool ReadExactly(byte[] buffer, int length)
    {
        var offset = 0;
        while (offset < length)
        {
            var read = pipe.Read(buffer, offset, length - offset);
            if (read <= 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private void SendHandshake()
    {
        SendFrame(0, "{\"v\":" + DiscordRpcVersion + ",\"client_id\":\"" + ClientId + "\"}");
    }

    private void SetActivity()
    {
        var payload =
            "{\"cmd\":\"SET_ACTIVITY\"," +
            "\"args\":{\"pid\":" + Process.GetCurrentProcess().Id + "," +
            "\"activity\":{\"details\":\"Playing\"}}," +
            "\"nonce\":\"" + Guid.NewGuid().ToString("N") + "\"}";

        SendFrame(1, payload);
    }

    private void ClearActivity()
    {
        if (pipe == null || !pipe.IsConnected)
        {
            return;
        }

        var payload =
            "{\"cmd\":\"SET_ACTIVITY\"," +
            "\"args\":{\"pid\":" + Process.GetCurrentProcess().Id + ",\"activity\":null}," +
            "\"nonce\":\"" + Guid.NewGuid().ToString("N") + "\"}";

        SendFrame(1, payload);
    }

    private void SendFrame(int opcode, string payload)
    {
        if (pipe == null || !pipe.IsConnected)
        {
            return;
        }

        try
        {
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var opcodeBytes = BitConverter.GetBytes(opcode);
            var lengthBytes = BitConverter.GetBytes(payloadBytes.Length);

            pipe.Write(opcodeBytes, 0, opcodeBytes.Length);
            pipe.Write(lengthBytes, 0, lengthBytes.Length);
            pipe.Write(payloadBytes, 0, payloadBytes.Length);
            pipe.Flush();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("Discord Rich Presence: failed to send IPC frame. " + e.Message);
        }
    }

    private void Disconnect()
    {
        isRunning = false;

        if (pipe != null)
        {
            pipe.Dispose();
            pipe = null;
        }
    }
}
