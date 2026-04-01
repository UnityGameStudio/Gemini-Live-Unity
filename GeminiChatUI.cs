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
    // [SerializeField] private ScrollRect _scrollRect;

    // -------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------

    private void Start()
    {
        _sendButton.onClick.AddListener(OnSendClicked);

        // Allow pressing Enter to send
        _inputField.onSubmit.AddListener(OnInputSubmit);

        _responseText.text = "";
    }

    private void OnDestroy()
    {
        _sendButton.onClick.RemoveListener(OnSendClicked);
        _inputField.onSubmit.RemoveListener(OnInputSubmit);
    }

    // -------------------------------------------------------
    // Input
    // -------------------------------------------------------

    private void OnSendClicked()
    {
        TrySendMessage();
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

    // -------------------------------------------------------
    // Output (called by GeminiLiveClient)
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private void AppendToChat(string speaker, string message)
    {
        if (!string.IsNullOrEmpty(_responseText.text))
            _responseText.text += "\n\n";

        // Bold speaker label + message
        _responseText.text += $"<b>{speaker}:</b> {message}";

        // // Scroll to bottom after layout updates
        // StartCoroutine(ScrollToBottom());
    }

    // private IEnumerator ScrollToBottom()
    // {
    //     // Wait for layout to rebuild before scrolling
    //     yield return new WaitForEndOfFrame();
    //     _scrollRect.verticalNormalizedPosition = 0f;
    // }
}