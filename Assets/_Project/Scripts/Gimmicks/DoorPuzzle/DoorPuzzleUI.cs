using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class DoorPuzzleUI : MonoBehaviour
{
    private static DoorPuzzleUI activeInstance;
    private static bool suppressStationUntilExit;

    [SerializeField] private DoorPuzzleController puzzleController;

    private GameObject panelRoot;
    private GameObject infoContent;
    private GameObject inputContent;
    private Text infoCodeText;
    private Text inputTitleText;
    private Text enteredCodeText;
    private Text statusText;
    private string enteredCode = string.Empty;
    private bool memoryShortcutMode;

    public static bool IsPanelVisible => activeInstance != null &&
                                         activeInstance.panelRoot != null &&
                                         activeInstance.panelRoot.activeSelf;
    public static bool IsStationSuppressed => suppressStationUntilExit;

    private void Awake()
    {
        activeInstance = this;
        suppressStationUntilExit = false;
        if (puzzleController == null)
        {
            puzzleController = GetComponent<DoorPuzzleController>();
        }

        CreateCanvas();
        HidePanel(false);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            suppressStationUntilExit = false;
            activeInstance = null;
        }
    }

    public void ShowInfoPanel(string code)
    {
        ClearUiSelection();
        panelRoot.SetActive(true);
        infoContent.SetActive(true);
        inputContent.SetActive(false);
        infoCodeText.text = "CODE: " + code[0] + "  " + code[1] + "  " + code[2];
        SetLocalInputBlocked(true);
        UnlockCursor();
    }

    public void ShowInputPanel(bool memoryShortcut)
    {
        ClearUiSelection();
        memoryShortcutMode = memoryShortcut;
        enteredCode = string.Empty;
        panelRoot.SetActive(true);
        infoContent.SetActive(false);
        inputContent.SetActive(true);
        inputTitleText.text = memoryShortcut ? "Memory Shortcut" : "Input Station";
        statusText.text = memoryShortcut
            ? "Enter the code you remember from Cooperation."
            : "Ask your partner for the 3-digit code.";
        RefreshEnteredCode();
        SetLocalInputBlocked(true);
        UnlockCursor();
    }

    public void SetStatus(string status, bool clearInput)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }

        if (clearInput)
        {
            enteredCode = string.Empty;
            RefreshEnteredCode();
        }
    }

    public void ShowSubmitResult(bool success, string status, bool memoryShortcut)
    {
        SetStatus(status, !success);
        if (success)
        {
            HidePanel(true);
        }
    }

    public void HidePanel(bool lockCursor)
    {
        ClearUiSelection();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        SetLocalInputBlocked(false);
        if (!lockCursor)
        {
            suppressStationUntilExit = false;
        }
        if (lockCursor)
        {
            LockCursor();
        }
    }

    public static bool IsPointerOverPanel(Vector2 screenPosition)
    {
        if (!IsPanelVisible)
        {
            return false;
        }

        RectTransform rect = activeInstance.panelRoot.GetComponent<RectTransform>();
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition);
    }

    public static void AllowStationReentry()
    {
        suppressStationUntilExit = false;
    }

    private void CreateCanvas()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("DoorPuzzleCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        panelRoot = CreateUiObject("DoorPuzzlePanel", canvasObject.transform, typeof(Image));
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(440f, 580f);
        panelRoot.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.96f);

        infoContent = CreateUiObject("InfoPanel", panelRoot.transform);
        CreateText(infoContent.transform, "InfoTitleText", "Partner Information", 26,
            20f, 28f, 400f, 44f, TextAnchor.MiddleCenter);
        infoCodeText = CreateText(infoContent.transform, "CodeText", "CODE: ---", 38,
            20f, 105f, 400f, 70f, TextAnchor.MiddleCenter);
        CreateText(infoContent.transform, "InfoHintText", "Tell your partner the code.", 18,
            20f, 190f, 400f, 40f, TextAnchor.MiddleCenter);
        CreateButton(infoContent.transform, "InfoCloseButton", "Close", 150f, 445f, 140f, 48f,
            ClosePanel);

        inputContent = CreateUiObject("InputPanel", panelRoot.transform);
        inputTitleText = CreateText(inputContent.transform, "InputTitleText", "Input Station", 25,
            20f, 20f, 400f, 40f, TextAnchor.MiddleCenter);
        statusText = CreateText(inputContent.transform, "StatusText", string.Empty, 16,
            20f, 65f, 400f, 48f, TextAnchor.MiddleCenter);
        enteredCodeText = CreateText(inputContent.transform, "EnteredCodeText", "_  _  _", 32,
            20f, 116f, 400f, 48f, TextAnchor.MiddleCenter);

        for (int digit = 1; digit <= 9; digit++)
        {
            int capturedDigit = digit;
            int index = digit - 1;
            float x = 78f + (index % 3) * 96f;
            float y = 176f + (index / 3) * 76f;
            CreateButton(inputContent.transform, "NumberButton_" + digit, digit.ToString(),
                x, y, 72f, 58f, () => AddDigit(capturedDigit));
        }

        CreateButton(inputContent.transform, "NumberButton_0", "0", 174f, 392f, 72f, 58f,
            () => AddDigit(0));
        CreateButton(inputContent.transform, "ResetButton", "Reset", 65f, 458f, 135f, 48f,
            ResetCode);
        CreateButton(inputContent.transform, "SubmitButton", "Submit", 240f, 458f, 135f, 48f,
            SubmitCode);
        CreateButton(inputContent.transform, "InputCloseButton", "Close", 150f, 518f, 140f, 48f,
            ClosePanel);
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
        Transform parent, string objectName, string value, int size,
        float x, float y, float width, float height, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent, typeof(Text));
        SetTopLeftRect(textObject.GetComponent<RectTransform>(), x, y, width, height);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private Button CreateButton(
        Transform parent, string objectName, string label,
        float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent, typeof(Image), typeof(Button));
        SetTopLeftRect(buttonObject.GetComponent<RectTransform>(), x, y, width, height);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.38f, 0.56f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        CreateText(buttonObject.transform, objectName + "Label", label, 20,
            2f, 2f, width - 4f, height - 4f, TextAnchor.MiddleCenter);
        return button;
    }

    private static void ClearUiSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void AddDigit(int digit)
    {
        if (enteredCode.Length >= 3)
        {
            return;
        }

        enteredCode += digit.ToString();
        RefreshEnteredCode();
    }

    private void ResetCode()
    {
        enteredCode = string.Empty;
        RefreshEnteredCode();
        statusText.text = memoryShortcutMode ? "Enter the code you remember." : "Code reset.";
    }

    private void SubmitCode()
    {
        if (enteredCode.Length != 3)
        {
            statusText.text = "Enter all 3 digits.";
            return;
        }

        statusText.text = "Checking...";
        if (memoryShortcutMode)
        {
            puzzleController.SubmitShortcutCodeServerRpc(enteredCode);
        }
        else
        {
            puzzleController.SubmitCooperationCodeServerRpc(enteredCode);
        }
    }

    private void ClosePanel()
    {
        suppressStationUntilExit = true;
        puzzleController.RequestCloseStationServerRpc();
        HidePanel(true);
    }

    private void RefreshEnteredCode()
    {
        if (enteredCodeText == null)
        {
            return;
        }

        string display = string.Empty;
        for (int i = 0; i < 3; i++)
        {
            display += i < enteredCode.Length ? enteredCode[i].ToString() : "_";
            if (i < 2) display += "  ";
        }

        enteredCodeText.text = display;
    }

    private void SetLocalInputBlocked(bool blocked)
    {
        if (Unity.Netcode.NetworkManager.Singleton == null ||
            Unity.Netcode.NetworkManager.Singleton.LocalClient == null ||
            Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            return;
        }

        GameObject localPlayer = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        localPlayer.GetComponent<PlayerMovement>()?.SetPuzzleUiInputBlocked(blocked);
        localPlayer.GetComponent<PlayerInteraction>()?.SetPuzzleUiInputBlocked(blocked);
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void LockCursor()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] types)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        foreach (System.Type type in types)
        {
            uiObject.AddComponent(type);
        }

        uiObject.transform.SetParent(parent, false);

        // Containers such as InfoPanel and InputPanel must fill the panel so their
        // children's top-left coordinates are relative to the visible panel bounds.
        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return uiObject;
    }

    private static void SetTopLeftRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
