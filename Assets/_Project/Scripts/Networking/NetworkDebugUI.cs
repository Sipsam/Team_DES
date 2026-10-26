using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Unity.Netcode;

public sealed class NetworkDebugUI : MonoBehaviour
{
    private static readonly Rect MenuRect = new Rect(0f, 0f, 600f, 720f);
    private static NetworkDebugUI activeInstance;

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private BridgeController bridgeController;
    [SerializeField] private MultiplayerSessionManager sessionManager;

    private Text joinCodeText;
    private Text statusText;
    private Text gameDebugText;
    private InputField joinCodeInput;
    private Button createRoomButton;
    private Button joinRoomButton;
    private Button leaveButton;

    private void Awake()
    {
        activeInstance = this;
        if (networkManager == null) networkManager = FindFirstObjectByType<NetworkManager>();
        if (gameStateManager == null) gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (bridgeController == null) bridgeController = FindFirstObjectByType<BridgeController>();
        if (sessionManager == null) sessionManager = GetComponent<MultiplayerSessionManager>();
        if (sessionManager == null) sessionManager = gameObject.AddComponent<MultiplayerSessionManager>();

        CreateMenuCanvas();
        sessionManager.StatusChanged += HandleStatusChanged;
        sessionManager.JoinCodeChanged += HandleJoinCodeChanged;
        statusText.text = sessionManager.CurrentStatus;
        joinCodeText.text = "Join Code: " + sessionManager.CurrentJoinCode;
    }

    private void OnDestroy()
    {
        if (sessionManager != null)
        {
            sessionManager.StatusChanged -= HandleStatusChanged;
            sessionManager.JoinCodeChanged -= HandleJoinCodeChanged;
        }

        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

    private void Update()
    {
        if (gameDebugText == null)
        {
            return;
        }

        int connectedPlayers = networkManager != null && networkManager.IsListening
            ? networkManager.ConnectedClientsIds.Count
            : 0;
        string gameState = gameStateManager != null ? gameStateManager.CurrentState.ToString() : "Unavailable";
        string turnPoint = gameStateManager != null
            ? $"TurnPoint: {gameStateManager.TurnPointArrivalCount}/{connectedPlayers}"
            : "TurnPoint: Unavailable";
        string winner = gameStateManager != null && gameStateManager.HasWinner
            ? $"Winner: Player {gameStateManager.WinnerClientId + 1} (Client {gameStateManager.WinnerClientId})"
            : "Winner: None";
        string bridge = bridgeController != null ? bridgeController.CurrentState.ToString() : "Unavailable";
        string switchA = bridgeController != null && bridgeController.IsSwitchActivated(BridgeSwitchId.A)
            ? "Active" : "Inactive";
        string switchB = bridgeController != null && bridgeController.IsSwitchActivated(BridgeSwitchId.B)
            ? "Active" : "Inactive";

        gameDebugText.text =
            $"Game State: {gameState}\n{turnPoint}\n{winner}\nBridge: {bridge}\nSwitch A: {switchA}   Switch B: {switchB}";

        bool sessionActive = sessionManager != null && sessionManager.HasSession;
        bool busy = sessionManager != null && sessionManager.IsBusy;
        if (createRoomButton != null) createRoomButton.interactable = !sessionActive && !busy;
        if (joinRoomButton != null) joinRoomButton.interactable = !sessionActive && !busy;
        if (leaveButton != null) leaveButton.interactable = sessionActive && !busy;
    }

    public static bool IsPointerOverMenu(Vector2 screenPosition)
    {
        if (activeInstance == null || activeInstance.networkManager == null ||
            !activeInstance.networkManager.IsListening)
        {
            return false;
        }

        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        return MenuRect.Contains(guiPosition);
    }

    private void CreateMenuCanvas()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("RelaySessionCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateUiObject("NetworkMenu", canvasObject.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetTopLeftRect(panelRect, new Vector2(12f, 12f), new Vector2(460f, 560f));
        panel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.94f);

        CreateText(panel.transform, "NetworkMenuTitle", "Relay Multiplayer", 28, 16f, 16f, 420f, 40f);
        statusText = CreateText(panel.transform, "StatusText", "Offline", 18, 18f, 60f, 424f, 56f);

        createRoomButton = CreateButton(panel.transform, "CreateRoomButton", "Create Room", 18f, 128f);
        createRoomButton.onClick.AddListener(() => _ = sessionManager.CreateRoomAsync());

        joinCodeText = CreateText(panel.transform, "JoinCodeText", "Join Code: ", 21, 18f, 184f, 424f, 34f);
        joinCodeInput = CreateInputField(panel.transform, "JoinCodeInput", 18f, 228f);
        joinRoomButton = CreateButton(panel.transform, "JoinRoomButton", "Join Room", 18f, 280f);
        joinRoomButton.onClick.AddListener(() => _ = sessionManager.JoinRoomAsync(joinCodeInput.text));

        leaveButton = CreateButton(panel.transform, "LeaveButton", "Leave Session", 18f, 334f);
        leaveButton.onClick.AddListener(() => _ = sessionManager.LeaveRoomAsync());

        CreateText(panel.transform, "CursorHintText", "Press Esc to unlock the cursor and use this menu.",
            14, 18f, 388f, 424f, 28f);
        gameDebugText = CreateText(panel.transform, "GameDebugText", string.Empty, 16, 18f, 432f, 424f, 116f);
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        inputModule.AssignDefaultActions();
    }

    private static Text CreateText(
        Transform parent, string objectName, string value, int fontSize,
        float x, float y, float width, float height)
    {
        GameObject textObject = CreateUiObject(objectName, parent, typeof(Text));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, new Vector2(x, y), new Vector2(width, height));

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;
        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label, float x, float y)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent, typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, new Vector2(x, y), new Vector2(424f, 44f));

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.38f, 0.56f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = new ColorBlock
        {
            normalColor = image.color,
            highlightedColor = new Color(0.27f, 0.5f, 0.7f, 1f),
            pressedColor = new Color(0.12f, 0.27f, 0.4f, 1f),
            selectedColor = image.color,
            disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.7f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };

        Text labelText = CreateText(buttonObject.transform, objectName + "Label", label,
            18, 8f, 0f, 408f, 44f);
        labelText.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private InputField CreateInputField(Transform parent, string objectName, float x, float y)
    {
        GameObject inputObject = CreateUiObject(objectName, parent, typeof(Image), typeof(InputField));
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, new Vector2(x, y), new Vector2(424f, 42f));
        inputObject.GetComponent<Image>().color = new Color(0.93f, 0.94f, 0.96f, 1f);

        Text inputText = CreateText(inputObject.transform, objectName + "Text", string.Empty,
            18, 10f, 2f, 404f, 38f);
        inputText.color = Color.black;

        Text placeholder = CreateText(inputObject.transform, objectName + "Placeholder", "Enter Join Code",
            17, 10f, 2f, 404f, 38f);
        placeholder.color = new Color(0.38f, 0.4f, 0.43f, 0.85f);

        InputField inputField = inputObject.GetComponent<InputField>();
        inputField.targetGraphic = inputObject.GetComponent<Image>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.characterLimit = 24;
        inputField.lineType = InputField.LineType.SingleLine;
        return inputField;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        foreach (System.Type componentType in components)
        {
            uiObject.AddComponent(componentType);
        }

        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(offset.x, -offset.y);
        rect.sizeDelta = size;
    }

    private void HandleStatusChanged(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void HandleJoinCodeChanged(string joinCode)
    {
        if (joinCodeText != null)
        {
            joinCodeText.text = "Join Code: " + (string.IsNullOrEmpty(joinCode) ? "-" : joinCode);
        }
    }
}
