using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GeminiChatUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GeminiLiveWebSocket _geminiClient;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text _responseText;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button _sendButton;
    [SerializeField] private Button _startRecordingButton;
     [SerializeField] private Button _startAudioRecordingButton;
    [SerializeField] private Button _startScreenRecordingButton;


    private void Start()
    {
        _sendButton.onClick.AddListener(OnSendClicked);

        _startRecordingButton.onClick.AddListener(OnRecordClicked);
        
        _startAudioRecordingButton.onClick.AddListener(OnAudioRecordClicked);

        _startScreenRecordingButton.onClick.AddListener(OnRecordScreenClicked);

        // Allow pressing Enter to send
        _inputField.onSubmit.AddListener(OnInputSubmit);

        _responseText.text = "";
    }

    private void OnDestroy()
    {
        _sendButton.onClick.RemoveListener(OnSendClicked);
        _startRecordingButton.onClick.RemoveListener(OnRecordClicked);

        _startAudioRecordingButton.onClick.RemoveListener(OnAudioRecordClicked);

        _startScreenRecordingButton.onClick.RemoveListener(OnRecordScreenClicked);

        _inputField.onSubmit.RemoveListener(OnInputSubmit);
    }

    private void OnSendClicked()
    {
        TrySendMessage();
    }

    private void OnRecordClicked()
    {
        TryRecordVideo();
    }

    private void OnAudioRecordClicked()
    {
        TryRecordAudio();
    }

    private void OnRecordScreenClicked()
    {
        TryRecordScreen();
    }

    private void OnInputSubmit(string value)
    {
        TrySendMessage();
    }

    private void TrySendMessage()
    {
        string text = _inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Show what the user typed in the response box
        AppendToChat("You", text);

        _geminiClient.SendTextMessage(text);

        _inputField.text = "";
        _inputField.ActivateInputField(); // keep focus
    }

    private void TryRecordVideo()
    {
        _geminiClient.StartVideoStream();
    }

    private void TryRecordScreen()
    {
        _geminiClient.StartScreenCapture();
    }


    private void TryRecordAudio()
    {
        _geminiClient.StartAudioStreaming();
    }

    public void ShowUserTranscription(string text)
    {
        AppendToChat("You", text);
    }

    public void ShowModelResponse(string text)
    {
        AppendToChat("Gemini", text);
    }

    public void ShowAudioReceived(int base64Length)
    {
        AppendToChat("Gemini", $"[Audio received — {base64Length} bytes]");
    }

    private void AppendToChat(string speaker, string message)
    {
        if (!string.IsNullOrEmpty(_responseText.text))
            _responseText.text += "\n\n";

        // Bold speaker label + message
        _responseText.text += $"<b>{speaker}:</b> {message}";

    }
}
