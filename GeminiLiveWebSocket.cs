using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System.Collections;

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

[Header("Video")]
[SerializeField] private int _videoFrameRate = 1; // frames per second to send
[SerializeField] private int _videoWidth = 640;
[SerializeField] private int _videoHeight = 480;

// Camera Variable
private WebCamTexture _webcamTexture;
private bool _isStreamingVideo = false;
private float _videoFrameInterval => 1f / _videoFrameRate;
private float _videoFrameTimer = 0f;

// Screensharing Variable
private bool _isCapturingScreen = false;
private float _screenFrameTimer = 0f;
[SerializeField] private float _screenFrameInterval = 1.0f; // Send 1 frame per second

// Audio Recording Variables
private float _audioFrameTimer = 0f;
private float _audioFrameInterval = 0.1f; 

// Mic Variables

private string _micDevice;
private AudioClip _micClip;
private bool _isStreamingAudio = false;
private int _lastSamplePos = 0;
private const int SAMPLING_RATE = 16000; 

public void StartVideoStream()
{
    if (_isStreamingVideo)
    {
        Debug.LogWarning("Video stream already running.");
        return;
    }

    if (WebCamTexture.devices.Length == 0)
    {
        Debug.LogError("No camera devices found.");
        return;
    }

    _webcamTexture = new WebCamTexture(
        WebCamTexture.devices[0].name, 
        _videoWidth, 
        _videoHeight, 
        _videoFrameRate
    );

    _webcamTexture.Play();
    _isStreamingVideo = true;
    _videoFrameTimer = 0f;

    Debug.Log($"Video stream started: {WebCamTexture.devices[0].name}");
}

public void StopVideoStream()
{
    if (!_isStreamingVideo) return;

    _isStreamingVideo = false;

    if (_webcamTexture != null)
    {
        _webcamTexture.Stop();
        _webcamTexture = null;
    }

    Debug.Log("Video stream stopped.");
}


public void StartAudioStreaming()
{
    if (_isStreamingAudio) return;

    if (Microphone.devices.Length == 0)
    {
        Debug.LogError("No microphone found.");
        return;
    }

    _micDevice = Microphone.devices[0];
    // Create a 10-second looping clip
    _micClip = Microphone.Start(_micDevice, true, 10, SAMPLING_RATE);
    _isStreamingAudio = true;
    _lastSamplePos = 0;

    Debug.Log($"Audio streaming started: {_micDevice}");
}

public void StopAudioStreaming()
{
    if (!_isStreamingAudio) return;

    Microphone.End(_micDevice);
    _micClip = null;
    _isStreamingAudio = false;
    Debug.Log("Audio streaming stopped.");
}

private void CaptureAndSendAudio()
{
    if (!_isStreamingAudio || _websocket?.State != WebSocketState.Open) return;

    int currentPos = Microphone.GetPosition(_micDevice);
    if (currentPos == _lastSamplePos) return;

    // Calculate how many samples were recorded since last time
    int sampleCount = (currentPos > _lastSamplePos) 
        ? currentPos - _lastSamplePos 
        : (SAMPLING_RATE * 10) - _lastSamplePos + currentPos;

    if (sampleCount <= 0) return;

    float[] samples = new float[sampleCount];
    _micClip.GetData(samples, _lastSamplePos);
    _lastSamplePos = currentPos;

    // Convert float samples (-1.0 to 1.0) to 16-bit PCM (short)
    short[] pcmData = new short[samples.Length];
    for (int i = 0; i < samples.Length; i++)
    {
        pcmData[i] = (short)(samples[i] * 32767f);
    }

    // Convert short[] to byte[]
    byte[] byteData = new byte[pcmData.Length * 2];
    Buffer.BlockCopy(pcmData, 0, byteData, 0, byteData.Length);

    // Send to Gemini
    string base64Audio = Convert.ToBase64String(byteData);
    string json = "{"
        + "\"realtimeInput\":{"
            + "\"audio\":{"
                + "\"data\":\"" + base64Audio + "\","
                + "\"mimeType\":\"audio/pcm;rate=16000\""
            + "}"
        + "}"
    + "}";

    SendRaw(json);
}


private IEnumerator SendDesktopScreenFrame()
{
    if (_websocket?.State != WebSocketState.Open) yield break;
    
    // CRITICAL: Wait until the frame is completely rendered
    yield return new WaitForEndOfFrame();
    
    int width = Screen.width;
    int height = Screen.height;
    
    // Create a texture matching the screen size
    Texture2D screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
    
    // Now ReadPixels will work because we're inside the drawing frame
    screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
    screenTexture.Apply();
    
    byte[] jpegBytes = screenTexture.EncodeToJPG(70); 

    // 3. Clean up the texture memory immediately to prevent crashes
    Destroy(screenTexture); 

    // 4. Convert and Send
    string base64Frame = Convert.ToBase64String(jpegBytes);

    string json = "{"
        + "\"realtimeInput\":{"
            + "\"video\":{"
                + "\"data\":\"" + base64Frame + "\","
                + "\"mimeType\":\"image/jpeg\""
            + "}"
        + "}"
    + "}";

    SendRaw(json);
}

public void StartScreenCapture()
{
    if (_isCapturingScreen)
    {
        Debug.LogWarning("Screen capture already running.");
        return;
    }

    _isCapturingScreen = true;
    _screenFrameTimer = 0f;
    Debug.Log("Desktop Screen capture started.");
}

public void StopScreenCapture()
{
    _isCapturingScreen = false;
    Debug.Log("Desktop Screen capture stopped.");
}


private void SendVideoFrame()
{
    if (_websocket?.State != WebSocketState.Open)
    {
        Debug.LogWarning("WebSocket not open, skipping frame.");
        return;
    }

    if (_webcamTexture == null || !_webcamTexture.isPlaying) return;

    // Grab current frame and encode to JPEG
    Texture2D snapshot = new Texture2D(_webcamTexture.width, _webcamTexture.height);
    snapshot.SetPixels(_webcamTexture.GetPixels());
    snapshot.Apply();

    byte[] jpegBytes = snapshot.EncodeToJPG(75); // 75 = quality
    Destroy(snapshot); // avoid memory leak

    string base64Frame = Convert.ToBase64String(jpegBytes);

    string json = "{"
        + "\"realtimeInput\":{"
            + "\"video\":{"
                + "\"data\":\"" + base64Frame + "\","
                + "\"mimeType\":\"image/jpeg\""
            + "}"
        + "}"
    + "}";

    SendRaw(json);
}

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

    // Video frame dispatch
    if (_isStreamingVideo)
    {
        _videoFrameTimer += Time.deltaTime;
        if (_videoFrameTimer >= _videoFrameInterval)
        {
            _videoFrameTimer = 0f;
            SendVideoFrame();
        }
    }

    if (_isCapturingScreen)
    {
        _screenFrameTimer += Time.deltaTime;
        if (_screenFrameTimer >= _screenFrameInterval)
        {
            _screenFrameTimer = 0f;
            // Coroutines must be started this way
            StartCoroutine(SendDesktopScreenFrame());
        }
    }

     if (_isStreamingAudio)
    {
        _audioFrameTimer += Time.deltaTime;
        if (_audioFrameTimer >= _audioFrameInterval)
        {
            _audioFrameTimer = 0f;
            CaptureAndSendAudio();
        }
    }
}


    async void OnApplicationQuit()
    {
        StopScreenCapture();
        StopVideoStream(); 
        StopAudioStreaming();
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
    // private void HandleToolCall(string json)
    // {
    //     // Add your tool call logic here
    //     // Use ExtractValue() to pull out specific fields as needed
    // }


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
