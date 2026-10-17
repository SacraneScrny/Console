using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UniversalConsole
{
    /// <summary>Code-built overlay UI. Uses uGUI only, no prefabs or manual setup required.</summary>
    [DisallowMultipleComponent]
    public sealed class ConsoleOverlay : MonoBehaviour
    {
        ConsoleRegistry _registry;
        ConsoleManager _manager;

        Canvas _canvas;
        GameObject _panel;
        ScrollRect _scroll;
        Text _logText;
        InputField _input;
        Button _toggleButton;
        Font _font;

        readonly List<string> _lines = new List<string>();
        readonly List<string> _history = new List<string>();
        readonly ConcurrentQueue<string> _pendingPrints = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> _pendingLogs = new ConcurrentQueue<string>();

        int _historyIndex = -1;
        const int MaxLines = 500;
        const int MaxHistory = 100;
        const int MaxPendingPerFrame = 30;
        const int MaxConditionChars = 300;
        bool _open;
        bool _setupDone;
        string _lastLine;
        int _repeatCount;
        bool _logDirty;
        float _nextRefreshTime;
        GameObject _autoEventSystem;
        GameObject _suggestPanel;
        LayoutElement _suggestLayout;
        readonly List<GameObject> _suggestItems = new List<GameObject>();
        readonly List<Image> _suggestBgs = new List<Image>();
        readonly List<ConsoleSuggestion> _suggestData = new List<ConsoleSuggestion>();
        string _suggestBase = "";
        string _suggestTyped = "";
        int _suggestIndex = -1;
        bool _applyingSuggestion;
        Text _ghost;

        /// <summary>True while the console panel is visible.</summary>
        public bool IsOpen { get { return _open; } }

        /// <summary>Injects dependencies and builds the UI on first call.</summary>
        public void Setup(ConsoleRegistry registry, ConsoleManager manager)
        {
            if (_registry != null) _registry.OnPrint -= EnqueuePrint;
            _registry = registry;
            _manager = manager;
            if (_registry != null) _registry.OnPrint += EnqueuePrint;
            if (_setupDone) return;
            _setupDone = true;
            BuildAll();
            SetOpen(manager != null && manager.startOpen);
        }

        /// <summary>Clears all visible log lines.</summary>
        public void ClearScreen()
        {
            _lines.Clear();
            _lastLine = null;
            _repeatCount = 0;
            _logDirty = false;
            RefreshLog();
        }

        /// <summary>Queues an external Unity log line.</summary>
        public void EnqueueExternalLog(string condition, LogType type)
        {
            string prefix = type == LogType.Error || type == LogType.Exception ? "<color=red>" :
                            type == LogType.Warning ? "<color=yellow>" : "";
            string suffix = string.IsNullOrEmpty(prefix) ? "" : "</color>";
            _pendingLogs.Enqueue(prefix + Escape(FirstLine(condition)) + suffix);
            while (_pendingLogs.Count > 200)
            {
                string d;
                _pendingLogs.TryDequeue(out d);
            }
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n');
            string first = nl < 0 ? s : s.Substring(0, nl);
            first = first.Trim();
            if (first.Length > MaxConditionChars) first = first.Substring(0, MaxConditionChars) + "...";
            return first;
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        void OnEnable()
        {
            if (_registry != null)
            {
                _registry.OnPrint -= EnqueuePrint;
                _registry.OnPrint += EnqueuePrint;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadHistory();
        }

        void OnDisable()
        {
            if (_registry != null) _registry.OnPrint -= EnqueuePrint;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SaveHistory();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RemoveDuplicateAutoEventSystem();
        }

        void EnqueuePrint(string line)
        {
            _pendingPrints.Enqueue(line ?? "");
            while (_pendingPrints.Count > 500)
            {
                string d;
                _pendingPrints.TryDequeue(out d);
            }
        }

        void BuildAll()
        {
            _font = LoadPackagedFont("Silver");
            if (_font == null) _font = GetBuiltinFont("LegacyRuntime.ttf");
            if (_font == null) _font = GetBuiltinFont("Arial.ttf");
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont("Arial", 14); }
                catch { }
            }
            if (_font == null)
            {
                try
                {
                    var all = Resources.FindObjectsOfTypeAll<Font>();
                    if (all != null && all.Length > 0) _font = all[0];
                }
                catch { }
            }
            if (_font == null)
            {
                try { Debug.LogError("[Console] No font found for overlay, text will be invisible."); }
                catch { }
            }
            EnsureEventSystem();

            var canvasGo = new GameObject("UniversalConsoleOverlay_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;
            _canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel");
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.94f);
            var prt = _panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, 0.25f);
            prt.anchorMax = new Vector2(1, 1);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            var layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildLogArea(_panel.transform);
            BuildSuggestList(_panel.transform);
            BuildInputRow(_panel.transform);
            BuildToggleButton(canvasGo.transform);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel.GetComponent<RectTransform>());
        }

        void BuildLogArea(Transform parent)
        {
            var scrollGo = new GameObject("LogScroll");
            scrollGo.transform.SetParent(parent, false);
            _scroll = scrollGo.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0, 0, 0, 0f);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.sizeDelta = Vector2.zero;
            var le = scrollGo.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = 100f;

            scrollGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.offsetMin = new Vector2(6, 6);
            contentRt.offsetMax = new Vector2(-6, -6);
            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("LogText");
            textGo.transform.SetParent(contentGo.transform, false);
            _logText = textGo.AddComponent<Text>();
            _logText.font = _font;
            _logText.fontSize = 11;
            _logText.lineSpacing = 0.6f;
            _logText.color = new Color(0.85f, 0.95f, 0.85f, 0.78f);
            _logText.supportRichText = true;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Overflow;
            var textLe = textGo.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1f;

            _scroll.content = contentRt;
            _scroll.viewport = scrollRt;
        }

        void BuildInputRow(Transform parent)
        {
            var rowGo = new GameObject("InputRow");
            rowGo.transform.SetParent(parent, false);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 2;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowLe = rowGo.AddComponent<LayoutElement>();
            rowLe.minHeight = 20f;
            rowLe.preferredHeight = 20f;
            rowLe.flexibleHeight = 0f;

            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(rowGo.transform, false);
            var prompt = promptGo.AddComponent<Text>();
            prompt.font = _font;
            prompt.fontSize = 11;
            prompt.color = new Color(0.5f, 1f, 0.5f, 0.85f);
            prompt.text = ">";
            prompt.alignment = TextAnchor.MiddleLeft;
            prompt.raycastTarget = false;
            var promptLe = promptGo.AddComponent<LayoutElement>();
            promptLe.minWidth = 10f;
            promptLe.preferredWidth = 10f;
            promptLe.flexibleWidth = 0f;
            promptLe.minHeight = 20f;
            promptLe.preferredHeight = 20f;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(rowGo.transform, false);
            var bg = inputGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0f);
            _input = inputGo.AddComponent<InputField>();
            _ghost = CreateGhostText(inputGo.transform);
            _input.textComponent = CreateInputText(inputGo.transform);
            _input.placeholder = CreatePlaceholder(inputGo.transform);
            _input.lineType = InputField.LineType.SingleLine;
            _input.caretColor = new Color(0.5f, 1f, 0.5f);
            _input.selectionColor = new Color(0.3f, 0.6f, 1f, 0.5f);
            _input.caretWidth = 2;
            _input.onEndEdit.AddListener(OnSubmit);
            _input.onValueChanged.AddListener(OnInputChanged);
            var inputLe = inputGo.AddComponent<LayoutElement>();
            inputLe.flexibleWidth = 1f;
            inputLe.minHeight = 20f;
            inputLe.preferredHeight = 20f;
        }

        Text CreateGhostText(Transform parent)
        {
            var go = new GameObject("Ghost");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(1, 0);
            rt.offsetMax = new Vector2(-1, 0);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 11;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(1, 1, 1, 0.9f);
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        Text CreateInputText(Transform parent)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(1, 0);
            rt.offsetMax = new Vector2(-1, 0);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 11;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(1, 1, 1, 0.92f);
            t.supportRichText = false;
            return t;
        }

        Text CreatePlaceholder(Transform parent)
        {
            var go = new GameObject("Placeholder");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(1, 0);
            rt.offsetMax = new Vector2(-1, 0);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 11;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(1, 1, 1, 0.35f);
            t.text = "type command... Up/Down select, Tab complete";
            return t;
        }

        void BuildSuggestList(Transform parent)
        {
            var go = new GameObject("Suggest");
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            _suggestLayout = go.AddComponent<LayoutElement>();
            _suggestLayout.flexibleHeight = 0f;
            _suggestLayout.preferredHeight = 0f;
            _suggestLayout.minHeight = 0f;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 0;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _suggestPanel = go;
            _suggestPanel.SetActive(false);
        }

        void ShowSuggest()
        {
            ClearSuggestItems();
            for (int i = 0; i < _suggestData.Count; i++)
            {
                int idx = i;
                var itemGo = new GameObject("Item" + i);
                itemGo.transform.SetParent(_suggestPanel.transform, false);
                var bg = itemGo.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0);
                var btn = itemGo.AddComponent<Button>();
                btn.onClick.AddListener(() => ApplySuggestionIndex(idx, false));
                var le = itemGo.AddComponent<LayoutElement>();
                le.minHeight = 14f;
                le.preferredHeight = 14f;
                le.flexibleHeight = 0f;
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(itemGo.transform, false);
                var lrt = labelGo.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(6, 1);
                lrt.offsetMax = new Vector2(-6, -1);
                var label = labelGo.AddComponent<Text>();
                label.font = _font;
                label.fontSize = 11;
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.raycastTarget = false;
                ConsoleSuggestion s = _suggestData[i];
                label.text = Escape(s.Name) + (string.IsNullOrEmpty(s.Hint) ? "" : " <color=#ffffff66>" + Escape(s.Hint) + "</color>");
                _suggestItems.Add(itemGo);
                _suggestBgs.Add(bg);
            }
            _suggestLayout.preferredHeight = _suggestData.Count * 14f + 8f;
            _suggestPanel.SetActive(true);
            RefreshSuggestHighlight();
        }

        void ClearSuggestItems()
        {
            for (int i = 0; i < _suggestItems.Count; i++)
                if (_suggestItems[i] != null) Destroy(_suggestItems[i]);
            _suggestItems.Clear();
            _suggestBgs.Clear();
        }

        void HideSuggest()
        {
            _suggestIndex = -1;
            _suggestBase = "";
            _suggestTyped = "";
            _suggestData.Clear();
            ClearSuggestItems();
            if (_suggestPanel != null) _suggestPanel.SetActive(false);
            UpdateGhost();
        }

        void RefreshSuggestHighlight()
        {
            for (int i = 0; i < _suggestBgs.Count; i++)
            {
                if (_suggestBgs[i] == null) continue;
                _suggestBgs[i].color = (i == _suggestIndex) ? new Color(0.25f, 0.5f, 0.25f, 0.75f) : new Color(0, 0, 0, 0);
            }
        }

        void OnInputChanged(string text)
        {
            if (_applyingSuggestion || _registry == null) return;
            string token = LastToken(text);
            if (token.Length == 0) { HideSuggest(); return; }
            if (_suggestPanel.activeSelf && token == _suggestBase)
            {
                _suggestTyped = text;
                UpdateGhost();
                return;
            }
            _registry.GetSuggestions(token, _suggestData, 7);
            _suggestBase = token;
            _suggestTyped = text;
            _suggestIndex = -1;
            if (_suggestData.Count == 0) { HideSuggest(); return; }
            ShowSuggest();
            UpdateGhost();
        }

        void ApplySuggestionText(string fullName)
        {
            string cur = _input.text ?? "";
            int cut = cur.Length - LastToken(cur).Length;
            _applyingSuggestion = true;
            _input.text = cur.Substring(0, cut) + fullName;
            _input.caretPosition = _input.text.Length;
            _applyingSuggestion = false;
            _suggestBase = fullName;
            _suggestTyped = _input.text;
        }

        void ApplySuggestionIndex(int index, bool submitAfter)
        {
            if (index < 0 || index >= _suggestData.Count) return;
            _suggestIndex = index;
            ApplySuggestionText(_suggestData[index].Name);
            RefreshSuggestHighlight();
            UpdateGhost();
            ReactivateInput();
            if (submitAfter) Submit(_input.text);
        }

        void RestoreSuggestTyped()
        {
            _suggestIndex = -1;
            _applyingSuggestion = true;
            _input.text = _suggestTyped;
            _input.caretPosition = _input.text.Length;
            _applyingSuggestion = false;
            _suggestBase = LastToken(_suggestTyped);
            RefreshSuggestHighlight();
            UpdateGhost();
            ReactivateInput();
        }

        void UpdateGhost()
        {
            if (_ghost == null || _input == null) return;
            string remainder = "";
            if (_suggestPanel != null && _suggestPanel.activeSelf && _suggestData.Count > 0)
            {
                ConsoleSuggestion target = _suggestIndex >= 0 ? _suggestData[_suggestIndex] : _suggestData[0];
                string cur = _input.text ?? "";
                string token = LastToken(cur);
                if (token.Length > 0 && target.Name.Length > token.Length &&
                    target.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase) && cur.Length <= 32)
                    remainder = target.Name.Substring(token.Length);
            }
            if (remainder.Length == 0) { _ghost.text = ""; return; }
            string typed = _input.text ?? "";
            _ghost.text = "<color=#00000000>" + Escape(typed) + "</color><color=#ffffff55>" + Escape(remainder) + "</color>";
        }

        void BuildToggleButton(Transform canvas)
        {
            var go = new GameObject("ToggleButton");
            go.transform.SetParent(canvas, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.75f);
            var btn = go.AddComponent<Button>();
            _toggleButton = btn;
            btn.onClick.AddListener(() => SetOpen(!_open));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-12, 12);
            rt.sizeDelta = new Vector2(64, 40);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = _font;
            label.fontSize = 11;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.6f, 1f, 0.6f, 0.8f);
            label.text = "~";
        }

        static Font GetBuiltinFont(string name)
        {
            try { return Resources.GetBuiltinResource<Font>(name); }
            catch { return null; }
        }

        static Font LoadPackagedFont(string name)
        {
            try { return Resources.Load<Font>(name); }
            catch { return null; }
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindActiveEventSystem() != null) return;
            var go = new GameObject("EventSystem (auto)");
            go.AddComponent<EventSystem>();
            EnsureInputModule(go);
            _autoEventSystem = go;
        }

        void RemoveDuplicateAutoEventSystem()
        {
            if (_autoEventSystem == null) return;
            EventSystem autoEs = _autoEventSystem.GetComponent<EventSystem>();
            EventSystem other = FindActiveEventSystemExcept(autoEs);
            if (other != null)
            {
                Destroy(_autoEventSystem);
                _autoEventSystem = null;
            }
            else if (autoEs == null)
            {
                _autoEventSystem = null;
            }
        }

        static EventSystem FindActiveEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            return UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
        }

        static EventSystem FindActiveEventSystemExcept(EventSystem except)
        {
#if UNITY_2023_1_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
#else
            var all = UnityEngine.Object.FindObjectsOfType<EventSystem>();
#endif
            foreach (var es in all)
                if (es != null && es != except && es.gameObject.activeInHierarchy) return es;
            return null;
        }

        static void EnsureInputModule(GameObject go)
        {
            if (go.GetComponent<StandaloneInputModule>() != null) return;
            if (FindComponentOfType(go, "UnityEngine.InputSystem.UI.InputSystemUIInputModule") != null) return;
#if ENABLE_INPUT_SYSTEM
            var ismType = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (ismType != null)
            {
                go.AddComponent(ismType);
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#elif !ENABLE_INPUT_SYSTEM
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static Component FindComponentOfType(GameObject go, string typeName)
        {
            var t = FindType(typeName);
            if (t == null) return null;
            return go.GetComponent(t);
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false, false); } catch { }
                if (t != null) return t;
            }
            return Type.GetType(fullName, false, false);
        }

        void OnSubmit(string text)
        {
            bool enter = LegacyDown(KeyCode.Return) || LegacyDown(KeyCode.KeypadEnter)
                || NewKeyPressed("enterKey") || NewKeyPressed("numpadEnterKey");
            bool mobileDone = Application.isMobilePlatform && !string.IsNullOrWhiteSpace(text);
            if (!enter && !mobileDone) return;
            if (_suggestPanel != null && _suggestPanel.activeSelf && _suggestIndex >= 0 && _suggestIndex < _suggestData.Count)
            {
                ApplySuggestionIndex(_suggestIndex, true);
                return;
            }
            if (string.IsNullOrWhiteSpace(text)) { ReactivateInput(); return; }
            Submit(text);
        }

        void Submit(string text)
        {
            HideSuggest();
            PushHistory(text);
            string echo = "<color=#7CFC98CC>></color> " + Escape(text);
            AppendLine(_lines.Count > 0 ? "\n" + echo : echo);
            try
            {
                if (_registry != null) _registry.Execute(text);
            }
            catch (Exception ex)
            {
                AppendLine("<color=red>" + Escape(ex.Message) + "</color>");
            }
            DrainQueues();
            _logDirty = false;
            RefreshLog();
            _input.text = "";
            ReactivateInput();
        }

        void ReactivateInput()
        {
            if (!_open) return;
            _input.Select();
            _input.ActivateInputField();
        }

        void AppendLine(string richLine)
        {
            AddLineCollapsed(richLine);
            RefreshLog();
        }

        void AddLineCollapsed(string richLine)
        {
            if (richLine == _lastLine && _lines.Count > 0)
            {
                _repeatCount++;
                _lines[_lines.Count - 1] = richLine + " <color=#888888AA>x" + _repeatCount + "</color>";
                return;
            }
            _lastLine = richLine;
            _repeatCount = 1;
            _lines.Add(richLine);
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);
        }

        void RefreshLog()
        {
            if (_logText == null) return;
            _logText.text = string.Join("\n", _lines.ToArray());
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        void PushHistory(string cmd)
        {
            _history.Remove(cmd);
            _history.Add(cmd);
            while (_history.Count > MaxHistory) _history.RemoveAt(0);
            _historyIndex = -1;
            SaveHistory();
        }

        void LoadHistory()
        {
            try
            {
                string raw = PlayerPrefs.GetString("UniversalConsole.History", "");
                if (string.IsNullOrEmpty(raw)) return;
                var parts = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                _history.Clear();
                foreach (var p in parts)
                {
                    try { _history.Add(Base64Decode(p)); } catch { }
                    if (_history.Count >= MaxHistory) break;
                }
            }
            catch { }
        }

        void SaveHistory()
        {
            try
            {
                var enc = new List<string>(_history.Count);
                foreach (var h in _history) enc.Add(Base64Encode(h));
                PlayerPrefs.SetString("UniversalConsole.History", string.Join("\n", enc.ToArray()));
            }
            catch { }
        }

        static string Base64Encode(string s)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? ""));
        }

        static string Base64Decode(string s)
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }

        /// <summary>Shows or hides the console panel.</summary>
        public void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.SetActive(open);
            if (_toggleButton != null && _manager != null)
                _toggleButton.gameObject.SetActive(_manager.showToggleButton && !open);
            if (open)
            {
                RemoveDuplicateAutoEventSystem();
                if (EventSystem.current == null && FindActiveEventSystem() == null) EnsureEventSystem();
                DrainQueues();
                _logDirty = false;
                RefreshLog();
                _nextRefreshTime = Time.unscaledTime + 0.15f;
                ReactivateInput();
                if (_manager != null && _manager.pauseWhileOpen) Time.timeScale = 0f;
            }
            else
            {
                HideSuggest();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                if (_manager != null && _manager.pauseWhileOpen) Time.timeScale = 1f;
            }
        }

        void Update()
        {
            DrainQueues();
            CheckToggleHotkey();
            if (!_open) return;
            CheckInputHotkeys();
        }

        void DrainQueues()
        {
            bool any = false;
            string s;
            int budget = MaxPendingPerFrame;
            while (budget-- > 0 && _pendingLogs.TryDequeue(out s)) { AddLineCollapsed(s); any = true; }
            while (_pendingPrints.TryDequeue(out s)) { AddLineCollapsed(s); any = true; }
            if (_pendingLogs.Count > 200)
            {
                while (_pendingLogs.Count > 50)
                {
                    string d;
                    _pendingLogs.TryDequeue(out d);
                }
            }
            if (any)
            {
                while (_lines.Count > MaxLines) _lines.RemoveAt(0);
                if (_open && Time.unscaledTime >= _nextRefreshTime)
                {
                    RefreshLog();
                    _nextRefreshTime = Time.unscaledTime + 0.15f;
                    _logDirty = false;
                }
                else _logDirty = true;
            }
            else if (_logDirty && _open && Time.unscaledTime >= _nextRefreshTime)
            {
                RefreshLog();
                _nextRefreshTime = Time.unscaledTime + 0.15f;
                _logDirty = false;
            }
        }

        void CheckToggleHotkey()
        {
            bool pressed = false;
            if (_manager != null)
            {
                if (LegacyDown(_manager.toggleKey) || LegacyDown(_manager.toggleKeyAlt)) pressed = true;
                if ((LegacyHeld(KeyCode.LeftControl) || LegacyHeld(KeyCode.RightControl)) &&
                    (LegacyHeld(KeyCode.LeftShift) || LegacyHeld(KeyCode.RightShift)) &&
                    LegacyDown(KeyCode.C)) pressed = true;
            }
            else if (LegacyDown(KeyCode.BackQuote) || LegacyDown(KeyCode.F1)) pressed = true;
            if (!pressed) pressed = CheckNewInputSystemHotkey();
            if (pressed) SetOpen(!_open);
        }

        static bool LegacyDown(KeyCode key)
        {
            try { return Input.GetKeyDown(key); }
            catch { return false; }
        }

        static bool LegacyHeld(KeyCode key)
        {
            try { return Input.GetKey(key); }
            catch { return false; }
        }

        static System.Reflection.PropertyInfo _kbCurrent;
        static readonly Dictionary<string, System.Reflection.PropertyInfo> _keyProps = new Dictionary<string, System.Reflection.PropertyInfo>();
        static System.Reflection.PropertyInfo _pressedProp;
        static bool _kbResolved;

        static object CurrentKeyboard()
        {
            if (!_kbResolved)
            {
                _kbResolved = true;
                try
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!asm.GetName().Name.StartsWith("Unity.InputSystem", StringComparison.OrdinalIgnoreCase)) continue;
                        var kbType = asm.GetType("UnityEngine.InputSystem.Keyboard");
                        if (kbType == null) continue;
                        _kbCurrent = kbType.GetProperty("current");
                        break;
                    }
                }
                catch { }
            }
            if (_kbCurrent == null) return null;
            try { return _kbCurrent.GetValue(null, null); }
            catch { return null; }
        }

        static bool NewKeyPressed(string keyPropName)
        {
            try
            {
                object kb = CurrentKeyboard();
                if (kb == null) return false;
                System.Reflection.PropertyInfo kp;
                if (!_keyProps.TryGetValue(keyPropName, out kp))
                {
                    kp = kb.GetType().GetProperty(keyPropName);
                    _keyProps[keyPropName] = kp;
                }
                if (kp == null) return false;
                object key = kp.GetValue(kb, null);
                if (key == null) return false;
                if (_pressedProp == null) _pressedProp = key.GetType().GetProperty("wasPressedThisFrame");
                if (_pressedProp == null) return false;
                return (bool)_pressedProp.GetValue(key, null);
            }
            catch { return false; }
        }

        static bool CheckNewInputSystemHotkey()
        {
            return NewKeyPressed("backquoteKey") || NewKeyPressed("f1Key");
        }

        void CheckInputHotkeys()
        {
            if (_input == null || !_input.isFocused) return;
            bool suggestOpen = _suggestPanel != null && _suggestPanel.activeSelf && _suggestData.Count > 0;
            if (Down(KeyCode.Escape, "escapeKey"))
            {
                if (suggestOpen) HideSuggest();
                return;
            }
            if (Down(KeyCode.Tab, "tabKey"))
            {
                if (suggestOpen)
                    ApplySuggestionIndex(_suggestIndex >= 0 ? _suggestIndex : 0, false);
                return;
            }
            if (Down(KeyCode.UpArrow, "upArrowKey"))
            {
                if (suggestOpen)
                {
                    if (_suggestIndex < 0) ApplySuggestionIndex(_suggestData.Count - 1, false);
                    else if (_suggestIndex == 0) RestoreSuggestTyped();
                    else ApplySuggestionIndex(_suggestIndex - 1, false);
                }
                else if (_history.Count > 0)
                {
                    if (_historyIndex < 0) _historyIndex = _history.Count - 1;
                    else if (_historyIndex > 0) _historyIndex--;
                    _applyingSuggestion = true;
                    _input.text = _history[_historyIndex];
                    _input.caretPosition = _input.text.Length;
                    _applyingSuggestion = false;
                    UpdateGhost();
                }
                return;
            }
            if (Down(KeyCode.DownArrow, "downArrowKey"))
            {
                if (suggestOpen)
                {
                    if (_suggestIndex < 0) ApplySuggestionIndex(0, false);
                    else if (_suggestIndex >= _suggestData.Count - 1) RestoreSuggestTyped();
                    else ApplySuggestionIndex(_suggestIndex + 1, false);
                }
                else if (_history.Count > 0 && _historyIndex >= 0)
                {
                    if (_historyIndex < _history.Count - 1) { _historyIndex++; _applyingSuggestion = true; _input.text = _history[_historyIndex]; _applyingSuggestion = false; }
                    else { _historyIndex = -1; _applyingSuggestion = true; _input.text = ""; _applyingSuggestion = false; }
                    _input.caretPosition = _input.text.Length;
                    UpdateGhost();
                }
            }
        }

        static bool Down(KeyCode legacy, string newSystemKey)
        {
            return LegacyDown(legacy) || NewKeyPressed(newSystemKey);
        }

        static string LastToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.Length - 1;
            while (i >= 0 && s[i] != ' ' && s[i] != '\t') i--;
            return s.Substring(i + 1);
        }
    }
}
