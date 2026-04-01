using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;

public class GeminiLiveWebSocket : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private string API_KEY;

    [Header("Model")]
    [SerializeField] private string MODEL_NAME = "gemini-3.1-flash-live-preview";

    private string WS_URL => $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={API_KEY}";

    private WebSocket _websocket;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;


    // -------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------

    async void Start()
    {
        await ConnectAsync();
    }

    void Update()
{
#if !UNITY_WEBGL || UNITY_EDITOR
    _websocket?.DispatchMessageQueue();
#endif

    // Play next queued clip when the current one finishes
    if (!_audioSource.isPlaying && _audioQueue.Count > 0)
    {
        _audioSource.clip = _audioQueue.Dequeue();
        _audioSource.Play();
    }
}


    async void OnApplicationQuit()
    {
        if (_websocket != null)
            await _websocket.Close();
    }

    // -------------------------------------------------------
    // Connection
    // -------------------------------------------------------

    private async System.Threading.Tasks.Task ConnectAsync()
    {
        _websocket = new WebSocket(WS_URL);

        _websocket.OnOpen    += OnOpen;
        _websocket.OnMessage += OnMessage;
        _websocket.OnError   += OnError;
        _websocket.OnClose   += OnClose;

        await _websocket.Connect();
    }

    // -------------------------------------------------------
    // Handlers
    // -------------------------------------------------------

    private void OnOpen()
    {
        Debug.Log("WebSocket Connected");
        SendConfig();
    }

    private void OnMessage(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        Debug.Log($"Received: {json}");
        HandleResponse(json);
    }

    private void OnError(string error)
    {
        Debug.LogError($"WebSocket Error: {error}");
    }

    private void OnClose(WebSocketCloseCode code)
    {
        Debug.Log($"WebSocket Closed: {code}");
    }

    // -------------------------------------------------------
    // Send helpers
    // -------------------------------------------------------

private void SendConfig()
{
    string json = "{"
        + "\"setup\":{"
            + $"\"model\":\"models/{MODEL_NAME}\","
            + "\"generationConfig\":{"
                + "\"responseModalities\":[\"AUDIO\"]"
            + "},"
            + "\"systemInstruction\":{"
                + "\"parts\":[{\"text\":\"You are a helpful assistant.\"}]"
            + "}"
        + "}"
    + "}";

    Debug.Log("Sending config: " + json);
    SendRaw(json);
}

    public void SendTextMessage(string text)
    {
        if (_websocket?.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not open.");
            return;
        }

        // Escape any special characters in the text
        string escaped = EscapeJson(text);
        string json = "{\"realtimeInput\":{\"text\":\"" + escaped + "\"}}";

        SendRaw(json);
        Debug.Log($"Text message sent: {text}");
    }

    private async void SendRaw(string json)
    {
        await _websocket.SendText(json);
    }

    // -------------------------------------------------------
    // Response parsing (manual)
    // -------------------------------------------------------

private void HandleResponse(string json)
{
    // Try both with and without space after colon
    string audioBase64 = ExtractValue(json, "\"data\": \"") 
                      ?? ExtractValue(json, "\"data\":\"");

    if (audioBase64 != null)
    {
        Debug.Log($"Received audio data (base64 len: {audioBase64.Length})");
        ProcessAudioData(audioBase64);
    }

    int inputIdx = json.IndexOf("\"inputTranscription\"");
    if (inputIdx >= 0)
    {
        string inputText = ExtractValue(json.Substring(inputIdx), "\"text\": \"") 
                        ?? ExtractValue(json.Substring(inputIdx), "\"text\":\"");
        if (inputText != null)
            Debug.Log($"User: {inputText}");
    }

    int outputIdx = json.IndexOf("\"outputTranscription\"");
    if (outputIdx >= 0)
    {
        string outputText = ExtractValue(json.Substring(outputIdx), "\"text\": \"") 
                         ?? ExtractValue(json.Substring(outputIdx), "\"text\":\"");
        if (outputText != null)
            Debug.Log($"Gemini: {outputText}");
    }

    if (json.Contains("\"toolCall\""))
    {
        Debug.Log("Tool call received");
        HandleToolCall(json);
    }
}
    // Extracts the string value after a given key prefix, up to the closing quote
private string ExtractValue(string json, string keyPrefix)
{
    int start = json.IndexOf(keyPrefix);
    if (start < 0) return null;

    start += keyPrefix.Length;
    int end = json.IndexOf('"', start);  // ← THIS is the bug
    if (end < 0) return null;

    return json.Substring(start, end - start);
}
    private void HandleToolCall(string json)
    {
        // Add your tool call logic here
        // Use ExtractValue() to pull out specific fields as needed
    }


private Queue<AudioClip> _audioQueue = new Queue<AudioClip>();

private void ProcessAudioData(string base64Audio)
{
    byte[] audioBytes = Convert.FromBase64String(base64Audio);
    int sampleCount = audioBytes.Length / 2;
    float[] samples = new float[sampleCount];

    for (int i = 0; i < sampleCount; i++)
    {
        short pcmSample = (short)(audioBytes[i * 2] | (audioBytes[i * 2 + 1] << 8));
        samples[i] = pcmSample / 32768f;
    }

    AudioClip clip = AudioClip.Create("GeminiAudio", sampleCount, 1, 24000, false);
    clip.SetData(samples, 0);
    _audioQueue.Enqueue(clip);
}


    // -------------------------------------------------------
    // Utility
    // -------------------------------------------------------

    private string EscapeJson(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}