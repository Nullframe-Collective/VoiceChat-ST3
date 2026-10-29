using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VoiceChat.Audio;
using Object = UnityEngine.Object;

namespace VoiceChat.UI
{
    internal sealed class SettingsMenu
    {
        private readonly VoiceOptions _options;
        private readonly MicrophoneCapture _microphone;
        private readonly MelonLogger.Instance _logger;
        private RectTransform _root;
        private Font _font;
        private Text _device, _bind, _test, _enabled, _meter;
        private bool _binding;
        private float _bindAfter;
        private string[] _devices = Array.Empty<string>();
        internal bool Visible => _root;

        internal SettingsMenu(VoiceOptions options, MicrophoneCapture microphone, MelonLogger.Instance logger)
        {
            _options = options;
            _microphone = microphone;
            _logger = logger;
        }

        internal void Show()
        {
            if (_root) return;

            _font = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(font =>
                font.name.Contains("Slender", StringComparison.OrdinalIgnoreCase));
            if (!_font)
            {
                _font = Resources.GetBuiltinResource(Il2CppType.Of<Font>(), "LegacyRuntime.ttf").Cast<Font>();
            }

            GameObject target = new("NullFrameVoiceSettings");
            Object.DontDestroyOnLoad(target);
            _root = target.AddComponent<RectTransform>();
            Canvas canvas = target.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32761;
            CanvasScaler scaler = target.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            target.AddComponent<GraphicRaycaster>();
            target.AddComponent<Image>().color = new Color(0.025f, 0.03f, 0.04f, 0.98f);
            RectTransform body = Rect("Body", _root, 0.16f, 0.08f, 0.84f, 0.94f);
            Label(body, "VOICE CHAT", 38, 0f, 0.89f, 1f, 1f);
            Label(body, "Spatial voice on NullFrame servers. Hold your key to talk.", 24, 0f, 0.81f, 1f, 0.89f);
            _enabled = Button(body, "", Toggle, 0f, 0.70f, 0.47f, 0.79f);
            Button(body, "Refresh microphones", Refresh, 0.51f, 0.70f, 1f, 0.79f);
            _device = Button(body, "", NextDevice, 0f, 0.58f, 1f, 0.68f);
            Range(body, "Microphone gain", _options.Gain, 0f, 4f, 0.47f, value => _options.Gain = value);
            Range(body, "Voice volume", _options.Volume, 0f, 1f, 0.36f, value => _options.Volume = value);
            _bind = Button(body, "", Bind, 0f, 0.25f, 1f, 0.34f);
            _test = Button(body, "Test microphone", Test, 0f, 0.14f, 0.47f, 0.23f);
            Button(body, "Close", Close, 0.51f, 0.14f, 1f, 0.23f);
            _meter = Label(body, "", 22, 0f, 0f, 1f, 0.13f);
            Refresh();
            ToggleLabel();
            BindingLabel();
        }

        internal void Update()
        {
            if (!_root) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_binding)
                {
                    _binding = false;
                    BindingLabel();
                }
                else
                {
                    Close();
                }

                return;
            }

            if (_binding && Time.unscaledTime > _bindAfter)
            {
                foreach (KeyCode key in Enum.GetValues<KeyCode>())
                {
                    if (key is KeyCode.None or KeyCode.Escape || !Input.GetKeyDown(key)) continue;

                    _options.Key = key;
                    _binding = false;
                    BindingLabel();
                    break;
                }
            }

            _meter.text = _microphone.Error ?? (_microphone.Testing
                ? "Microphone level: " + Mathf.RoundToInt(_microphone.Level * 100f) + "% (local test, not transmitted)"
                : "Press Test microphone to check input. Device button cycles through available microphones.");
            _meter.color = _microphone.Error != null ? new Color(1f, 0.4f, 0.4f) :
                Color.Lerp(Color.gray, Color.green, _microphone.Level);
        }

        private void Toggle()
        {
            _options.Enabled = !_options.Enabled;
            if (!_options.Enabled)
            {
                _microphone.Testing = false;
                _microphone.Dispose();
            }

            ToggleLabel();
        }

        private void ToggleLabel() => _enabled.text = _options.Enabled ? "Voice chat: Enabled" : "Voice chat: Disabled";

        private void Refresh()
        {
            _devices = Microphone.devices;
            DeviceLabel();
        }

        private void NextDevice()
        {
            if (_devices.Length == 0)
            {
                Refresh();
                return;
            }

            int index = Math.Max(0, Array.IndexOf(_devices, _options.Device));
            _options.Device = _devices[(index + 1) % _devices.Length];
            _microphone.Dispose();
            DeviceLabel();
        }

        private void DeviceLabel() => _device.text = _devices.Length == 0 ? "No microphone found" :
            "Microphone: " + (_devices.Contains(_options.Device) ? _options.Device : _devices[0]);

        private void Bind()
        {
            _binding = true;
            _bindAfter = Time.unscaledTime + 0.25f;
            _bind.text = "Press a key or mouse button (Escape cancels)";
        }

        private void BindingLabel() => _bind.text = "Push to talk: " + _options.Key + "   [Change]";

        private void Test()
        {
            _microphone.Testing = !_microphone.Testing;
            _test.text = _microphone.Testing ? "Stop microphone test" : "Test microphone";
        }

        internal void Close()
        {
            _microphone.Testing = false;
            _microphone.Dispose();
            _binding = false;
            if (!_root) return;

            Object.Destroy(_root.gameObject);
            _root = null;

            try
            {
                _options.Save();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _logger.Warning("Could not save voice settings: " + error.Message);
            }
        }

        private RectTransform Rect(string name, Transform parent, float left, float bottom, float right, float top)
        {
            GameObject target = new(name);
            RectTransform rect = target.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text Label(Transform parent, string text, int size, float left, float bottom, float right, float top)
        {
            Text label = Rect("Label", parent, left, bottom, right, top).gameObject.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = size;
            label.text = text;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        private Text Button(Transform parent, string text, Action clicked, float left, float bottom, float right, float top)
        {
            RectTransform rect = Rect("Button", parent, left, bottom, right, top);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.15f, 0.17f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(clicked));
            Text label = Label(rect, text, 27, 0.03f, 0f, 0.97f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            return label;
        }

        private void Range(Transform parent, string title, float value, float minimum, float maximum, float top, Action<float> changed)
        {
            RectTransform row = Rect(title, parent, 0f, top, 1f, top + 0.09f);
            Text label = Label(row, title + ": " + value.ToString("0.00") + "x", 25, 0f, 0f, 0.49f, 1f);
            RectTransform track = Rect("Slider", row, 0.52f, 0.15f, 0.98f, 0.85f);
            Rect("Track", track, 0f, 0.4f, 1f, 0.6f).gameObject.AddComponent<Image>().color = Color.gray;
            RectTransform handle = Rect("Handle", track, 0f, 0.1f, 0f, 0.9f);
            handle.sizeDelta = new Vector2(18f, 0f);
            Image image = handle.gameObject.AddComponent<Image>();
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = image;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.value = value;
            slider.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>(new Action<float>(current =>
            {
                changed(current);
                label.text = title + ": " + current.ToString("0.00") + "x";
            })));
        }
    }
}
