#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.LLM;
using DrWario.Editor.UI;
using DrWario.Runtime;

namespace DrWario.Editor
{
    public class DrWarioView : VisualElement
    {
        private VisualElement[] _sections;
        private List<Button> _tabButtons = new();

        private Label _statusLabel;
        private Label _gradeLabel;
        private VisualElement _categoryContainer;
        private VisualElement _findingsContainer;
        private VisualElement _recommendationsContainer;
        private VisualElement _settingsContainer;
        private VisualElement _historyContainer;
        private VisualElement _askContainer;
        private TextField _askInput;
        private Label _askResponseLabel;
        private Button _askBtn;
        private Button _copyResponseBtn;
        private Button _copyPromptBtn;
        private string _lastFullPrompt;
        private VisualElement _sparklineElement;
        private float[] _sparklineData;
        private Button _startBtn, _stopBtn, _analyzeBtn;
        private TimelineElement _timelineElement;

        private DiagnosticReport _lastReport;
        private readonly LLMConfig _llmConfig = new();
        private AnalysisEngine _analysisEngine;
        private bool _isStreaming;
        private Label _streamingIndicator;
        private HashSet<VisualElement> _expandedCards = new();
        private VisualElement _rulesContainer;

        // Comparison state
        private DiagnosticReport _selectedCompareReport;
        private string _selectedCompareLabel;

        // LLM settings fields
        private PopupField<string> _providerField;
        private TextField _apiKeyField;
        private TextField _modelField;
        private TextField _endpointField;
        private Toggle _enabledToggle;
        private Label _llmStatusLabel;

        public DrWarioView()
        {
            // Header
            var header = new Label("DrWario");
            header.style.fontSize = 20;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2;
            Add(header);

            var subtitle = new Label("Runtime Performance Diagnostics");
            subtitle.style.fontSize = 11;
            subtitle.style.color = new Color(0.6f, 0.6f, 0.6f);
            subtitle.style.marginBottom = 10;
            Add(subtitle);

            // Toolbar
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 10;
            Add(toolbar);

            _startBtn = new Button(OnStartProfiling) { text = "Start Profiling" };
            _startBtn.style.height = 28;
            _startBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.45f, 0.2f));
            toolbar.Add(_startBtn);

            _stopBtn = new Button(OnStopProfiling) { text = "Stop Profiling" };
            _stopBtn.style.height = 28;
            _stopBtn.SetEnabled(false);
            toolbar.Add(_stopBtn);

            _analyzeBtn = new Button(OnAnalyze) { text = "Analyze" };
            _analyzeBtn.style.height = 28;
            _analyzeBtn.SetEnabled(false);
            toolbar.Add(_analyzeBtn);

            var exportJsonBtn = new Button(OnExportJson) { text = "Export JSON" };
            exportJsonBtn.style.height = 28;
            toolbar.Add(exportJsonBtn);

            var exportTextBtn = new Button(OnExportText) { text = "Export Text" };
            exportTextBtn.style.height = 28;
            toolbar.Add(exportTextBtn);

            var exportHtmlBtn = new Button(OnExportHtml) { text = "Export HTML" };
            exportHtmlBtn.style.height = 28;
            toolbar.Add(exportHtmlBtn);

            // Status
            _statusLabel = new Label("Idle. Enter Play Mode and click Start Profiling.");
            _statusLabel.style.marginBottom = 10;
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            Add(_statusLabel);

            // Sub-tabs
            var tabContainer = new VisualElement();
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.marginBottom = 5;
            tabContainer.style.borderBottomWidth = 1;
            tabContainer.style.borderBottomColor = Color.gray;
            Add(tabContainer);

            var contentArea = new VisualElement();
            contentArea.style.flexGrow = 1;
            Add(contentArea);

            CreateTabButton("Summary", () => ShowSection(0), tabContainer);
            CreateTabButton("Findings", () => ShowSection(1), tabContainer);
            CreateTabButton("Recommendations", () => ShowSection(2), tabContainer);
            CreateTabButton("Timeline", () => { ShowSection(3); PopulateTimeline(); }, tabContainer);
            CreateTabButton("History", () => { ShowSection(4); RefreshHistory(); }, tabContainer);
            CreateTabButton("Ask Doctor", () => ShowSection(5), tabContainer);
            CreateTabButton("LLM Settings", () => ShowSection(6), tabContainer);

            _sections = new VisualElement[7];
            _sections[0] = CreateSummarySection();
            _sections[1] = CreateFindingsSection();
            _sections[2] = CreateRecommendationsSection();
            _sections[3] = CreateTimelineSection();
            _sections[4] = CreateHistorySection();
            _sections[5] = CreateAskSection();
            _sections[6] = CreateSettingsSection();

            foreach (var s in _sections) contentArea.Add(s);
            ShowSection(0);
        }

        // -- Tab helpers --

        private void CreateTabButton(string label, System.Action onClick, VisualElement parent)
        {
            var btn = new Button(onClick) { text = label };
            btn.style.flexGrow = 1;
            btn.style.height = 25;
            btn.style.borderBottomWidth = 0;
            parent.Add(btn);
            _tabButtons.Add(btn);
        }

        private void ShowSection(int index)
        {
            for (int i = 0; i < _sections.Length; i++)
            {
                _sections[i].style.display = (i == index) ? DisplayStyle.Flex : DisplayStyle.None;
                _tabButtons[i].style.backgroundColor = new StyleColor(
                    i == index ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.2f, 0.2f, 0.2f));
                _tabButtons[i].style.unityFontStyleAndWeight =
                    i == index ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        // -- Sections --

        private VisualElement CreateSummarySection()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            // Grade display
            _gradeLabel = new Label("--");
            _gradeLabel.style.fontSize = 64;
            _gradeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _gradeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _gradeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _gradeLabel.style.marginTop = 20;
            _gradeLabel.style.marginBottom = 10;
            scroll.Add(_gradeLabel);

            var scoreSubtitle = new Label("Run a profiling session to see your grade");
            scoreSubtitle.name = "score-subtitle";
            scoreSubtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            scoreSubtitle.style.fontSize = 12;
            scoreSubtitle.style.color = new Color(0.6f, 0.6f, 0.6f);
            scoreSubtitle.style.marginBottom = 20;
            scroll.Add(scoreSubtitle);

            // Category cards
            _categoryContainer = new VisualElement();
            _categoryContainer.style.flexDirection = FlexDirection.Row;
            _categoryContainer.style.flexWrap = Wrap.Wrap;
            _categoryContainer.style.justifyContent = Justify.Center;
            scroll.Add(_categoryContainer);

            // Frame time sparkline
            var sparklineHeader = new Label("Frame Time (ms)");
            sparklineHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            sparklineHeader.style.fontSize = 12;
            sparklineHeader.style.marginTop = 15;
            sparklineHeader.style.marginBottom = 4;
            sparklineHeader.style.marginLeft = 10;
            scroll.Add(sparklineHeader);

            _sparklineElement = new VisualElement();
            _sparklineElement.style.height = 80;
            _sparklineElement.style.marginLeft = 10;
            _sparklineElement.style.marginRight = 10;
            _sparklineElement.style.marginBottom = 10;
            _sparklineElement.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            _sparklineElement.generateVisualContent += OnDrawSparkline;
            scroll.Add(_sparklineElement);

            return scroll;
        }

        private VisualElement CreateFindingsSection()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            _findingsContainer = new VisualElement();
            _findingsContainer.style.paddingTop = 10;
            _findingsContainer.style.paddingBottom = 10;
            _findingsContainer.style.paddingLeft = 10;
            _findingsContainer.style.paddingRight = 10;
            scroll.Add(_findingsContainer);
            return scroll;
        }

        private VisualElement CreateRecommendationsSection()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            _recommendationsContainer = new VisualElement();
            _recommendationsContainer.style.paddingTop = 10;
            _recommendationsContainer.style.paddingBottom = 10;
            _recommendationsContainer.style.paddingLeft = 10;
            _recommendationsContainer.style.paddingRight = 10;
            scroll.Add(_recommendationsContainer);
            return scroll;
        }

        private VisualElement CreateTimelineSection()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.paddingTop = 10;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingBottom = 10;

            var header = new Label("Event Timeline");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 13;
            header.style.marginBottom = 4;
            container.Add(header);

            var legend = new VisualElement();
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.flexWrap = Wrap.Wrap;
            legend.style.marginBottom = 6;

            var legendItems = new (string label, Color color)[]
            {
                ("CPU Spikes", new Color(0.267f, 0.533f, 1f)),
                ("GC Alloc", new Color(1f, 0.533f, 0.267f)),
                ("Boot", new Color(0.267f, 0.733f, 0.267f)),
                ("Assets", new Color(0.667f, 0.267f, 1f)),
                ("Network", new Color(0.533f, 0.533f, 0.533f))
            };

            foreach (var (label, color) in legendItems)
            {
                var item = new VisualElement();
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;
                item.style.marginRight = 12;

                var swatch = new VisualElement();
                swatch.style.width = 10;
                swatch.style.height = 10;
                swatch.style.backgroundColor = new StyleColor(color);
                swatch.style.borderBottomLeftRadius = 2;
                swatch.style.borderBottomRightRadius = 2;
                swatch.style.borderTopLeftRadius = 2;
                swatch.style.borderTopRightRadius = 2;
                swatch.style.marginRight = 4;
                item.Add(swatch);

                var lbl = new Label(label);
                lbl.style.fontSize = 10;
                lbl.style.color = new Color(0.7f, 0.7f, 0.7f);
                item.Add(lbl);

                legend.Add(item);
            }
            container.Add(legend);

            var hint = new Label("Scroll to zoom, drag to pan");
            hint.style.fontSize = 10;
            hint.style.color = new Color(0.5f, 0.5f, 0.5f);
            hint.style.marginBottom = 6;
            container.Add(hint);

            _timelineElement = new TimelineElement();
            _timelineElement.style.height = 250;
            _timelineElement.style.flexGrow = 1;
            container.Add(_timelineElement);

            return container;
        }

        private void PopulateTimeline()
        {
            if (_timelineElement == null) return;

            var session = RuntimeCollector.ActiveSession;
            if (session == null || session.FrameCount == 0)
            {
                _timelineElement.SetEvents(null);
                return;
            }

            var events = TimelineEventBuilder.Build(session);
            _timelineElement.SetEvents(events);
        }

        private VisualElement CreateSettingsSection()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            _settingsContainer = new VisualElement();
            _settingsContainer.style.paddingTop = 10;
            _settingsContainer.style.paddingBottom = 10;
            _settingsContainer.style.paddingLeft = 10;
            _settingsContainer.style.paddingRight = 10;
            scroll.Add(_settingsContainer);

            // Auto-start toggle
            var autoStartToggle = new Toggle("Auto-start profiling on Play Mode")
                { value = DrWarioPlayModeHook.AutoStartEnabled };
            autoStartToggle.RegisterValueChangedCallback(evt =>
                DrWarioPlayModeHook.AutoStartEnabled = evt.newValue);
            autoStartToggle.style.marginBottom = 15;
            _settingsContainer.Add(autoStartToggle);

            var settingsHeader = new Label("LLM Configuration");
            settingsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            settingsHeader.style.fontSize = 14;
            settingsHeader.style.marginBottom = 10;
            _settingsContainer.Add(settingsHeader);

            var desc = new Label("Connect an AI model for deeper performance analysis. " +
                                 "Rule-based analysis always runs. LLM findings are additive.");
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.fontSize = 11;
            desc.style.color = new Color(0.6f, 0.6f, 0.6f);
            desc.style.marginBottom = 15;
            _settingsContainer.Add(desc);

            // Enable toggle
            _enabledToggle = new Toggle("Enable AI Analysis") { value = _llmConfig.Enabled };
            _enabledToggle.RegisterValueChangedCallback(evt => _llmConfig.Enabled = evt.newValue);
            _enabledToggle.style.marginBottom = 10;
            _settingsContainer.Add(_enabledToggle);

            // Provider dropdown
            var providerNames = new List<string> { "Claude", "OpenAI", "Ollama", "Custom" };
            _providerField = new PopupField<string>("Provider", providerNames, (int)_llmConfig.Provider);
            _providerField.RegisterValueChangedCallback(evt =>
            {
                _llmConfig.Provider = (LLMProvider)providerNames.IndexOf(evt.newValue);
                _modelField.value = _llmConfig.DefaultModelId;
                _endpointField.value = _llmConfig.DefaultEndpoint;
                _llmConfig.ModelId = _llmConfig.DefaultModelId;
                _llmConfig.Endpoint = _llmConfig.DefaultEndpoint;
                UpdateApiKeyVisibility();
            });
            _providerField.style.marginBottom = 5;
            _settingsContainer.Add(_providerField);

            // Model
            _modelField = new TextField("Model") { value = _llmConfig.ModelId };
            _modelField.RegisterValueChangedCallback(evt => _llmConfig.ModelId = evt.newValue);
            _modelField.style.marginBottom = 5;
            _settingsContainer.Add(_modelField);

            // API Key
            _apiKeyField = new TextField("API Key") { value = _llmConfig.HasApiKey ? "********" : "", isPasswordField = true };
            _apiKeyField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != "********")
                    _llmConfig.ApiKey = evt.newValue;
            });
            _apiKeyField.style.marginBottom = 5;
            _settingsContainer.Add(_apiKeyField);

            // Endpoint
            _endpointField = new TextField("Endpoint") { value = _llmConfig.Endpoint };
            _endpointField.RegisterValueChangedCallback(evt => _llmConfig.Endpoint = evt.newValue);
            _endpointField.style.marginBottom = 10;
            _settingsContainer.Add(_endpointField);

            // Buttons row
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginBottom = 10;

            var testBtn = new Button(OnTestConnection) { text = "Test Connection" };
            testBtn.style.height = 28;
            btnRow.Add(testBtn);

            var resetBtn = new Button(() =>
            {
                _llmConfig.ResetToDefaults();
                _modelField.value = _llmConfig.ModelId;
                _endpointField.value = _llmConfig.Endpoint;
            }) { text = "Reset Defaults" };
            resetBtn.style.height = 28;
            btnRow.Add(resetBtn);

            _settingsContainer.Add(btnRow);

            // Status
            _llmStatusLabel = new Label(_llmConfig.IsConfigured ? "Configured and ready." : "Not configured. AI analysis disabled.");
            _llmStatusLabel.style.fontSize = 11;
            _llmStatusLabel.style.color = _llmConfig.IsConfigured ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.7f, 0.7f, 0.7f);
            _settingsContainer.Add(_llmStatusLabel);

            // Info card
            var infoCard = new VisualElement();
            infoCard.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            infoCard.style.marginTop = 15;
            infoCard.style.paddingTop = 10;
            infoCard.style.paddingBottom = 10;
            infoCard.style.paddingLeft = 10;
            infoCard.style.paddingRight = 10;
            infoCard.style.borderLeftWidth = 3;
            infoCard.style.borderLeftColor = new Color(0.4f, 0.7f, 0.9f);

            infoCard.Add(new Label("How it works") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });
            infoCard.Add(new Label(
                "1. Rule-based analysis always runs (6 built-in rules: GC, CPU, Boot, Memory, Assets, Network)\n" +
                "2. When enabled, profiling metadata is sent to the LLM (~1000 tokens)\n" +
                "3. The LLM returns additional findings with deeper correlations\n" +
                "4. No source code is ever sent — only statistical summaries\n" +
                "5. If the LLM is unavailable, the report still works with rules only")
            { style = { whiteSpace = WhiteSpace.Normal, fontSize = 11, color = new Color(0.7f, 0.7f, 0.7f) } });

            _settingsContainer.Add(infoCard);

            // Rule Management section
            var rulesSeparator = new VisualElement();
            rulesSeparator.style.height = 1;
            rulesSeparator.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            rulesSeparator.style.marginTop = 20;
            rulesSeparator.style.marginBottom = 10;
            _settingsContainer.Add(rulesSeparator);

            var rulesHeader = new Label("Rule Management");
            rulesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            rulesHeader.style.fontSize = 14;
            rulesHeader.style.marginBottom = 5;
            _settingsContainer.Add(rulesHeader);

            var rulesDesc = new Label("Enable or disable individual analysis rules. " +
                                     "For configurable rules, adjust the detection threshold.");
            rulesDesc.style.whiteSpace = WhiteSpace.Normal;
            rulesDesc.style.fontSize = 11;
            rulesDesc.style.color = new Color(0.6f, 0.6f, 0.6f);
            rulesDesc.style.marginBottom = 10;
            _settingsContainer.Add(rulesDesc);

            _rulesContainer = new VisualElement();
            _settingsContainer.Add(_rulesContainer);
            PopulateRulesUI();

            UpdateApiKeyVisibility();
            return scroll;
        }

        private void PopulateRulesUI()
        {
            if (_rulesContainer == null) return;
            _rulesContainer.Clear();

            // Use a temporary engine to get all registered rules
            var tempEngine = _analysisEngine ?? new AnalysisEngine();
            var rules = tempEngine.RegisteredRules;

            foreach (var rule in rules)
            {
                var ruleRow = new VisualElement();
                ruleRow.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
                ruleRow.style.marginBottom = 4;
                ruleRow.style.paddingTop = 6;
                ruleRow.style.paddingBottom = 6;
                ruleRow.style.paddingLeft = 10;
                ruleRow.style.paddingRight = 10;
                ruleRow.style.borderLeftWidth = 2;
                ruleRow.style.borderLeftColor = RuleConfig.IsEnabled(rule.RuleId)
                    ? new Color(0.4f, 0.8f, 0.4f)
                    : new Color(0.4f, 0.4f, 0.4f);

                // Top row: toggle + rule name + category
                var topRow = new VisualElement();
                topRow.style.flexDirection = FlexDirection.Row;
                topRow.style.alignItems = Align.Center;

                string ruleId = rule.RuleId;
                var capturedRow = ruleRow;

                var enableToggle = new Toggle { value = RuleConfig.IsEnabled(ruleId) };
                enableToggle.style.marginRight = 8;
                enableToggle.RegisterValueChangedCallback(evt =>
                {
                    RuleConfig.SetEnabled(ruleId, evt.newValue);
                    capturedRow.style.borderLeftColor = evt.newValue
                        ? new Color(0.4f, 0.8f, 0.4f)
                        : new Color(0.4f, 0.4f, 0.4f);
                });
                topRow.Add(enableToggle);

                var ruleNameLabel = new Label(rule.RuleId);
                ruleNameLabel.style.flexGrow = 1;
                ruleNameLabel.style.fontSize = 11;
                ruleNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                topRow.Add(ruleNameLabel);

                var ruleCatLabel = new Label($"[{rule.Category}]");
                ruleCatLabel.style.fontSize = 10;
                ruleCatLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                topRow.Add(ruleCatLabel);

                ruleRow.Add(topRow);

                // Threshold slider for IConfigurableRule
                if (rule is IConfigurableRule configurable)
                {
                    var thresholdRow = new VisualElement();
                    thresholdRow.style.flexDirection = FlexDirection.Row;
                    thresholdRow.style.alignItems = Align.Center;
                    thresholdRow.style.marginTop = 4;
                    thresholdRow.style.marginLeft = 28;

                    var thresholdLabel = new Label(configurable.ThresholdLabel + ":");
                    thresholdLabel.style.fontSize = 10;
                    thresholdLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    thresholdLabel.style.width = 120;
                    thresholdRow.Add(thresholdLabel);

                    float currentThreshold = RuleConfig.GetThreshold(ruleId, configurable.DefaultThreshold);

                    var valueLabel = new Label(currentThreshold.ToString("F1"));
                    valueLabel.style.fontSize = 10;
                    valueLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    valueLabel.style.width = 50;
                    valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;

                    var slider = new Slider(configurable.MinThreshold, configurable.MaxThreshold);
                    slider.value = currentThreshold;
                    slider.style.flexGrow = 1;
                    slider.style.marginLeft = 4;
                    slider.style.marginRight = 4;

                    string capturedRuleId = ruleId;
                    var capturedValueLabel = valueLabel;
                    slider.RegisterValueChangedCallback(evt =>
                    {
                        RuleConfig.SetThreshold(capturedRuleId, evt.newValue);
                        capturedValueLabel.text = evt.newValue.ToString("F1");
                    });

                    thresholdRow.Add(slider);
                    thresholdRow.Add(valueLabel);

                    var resetThresholdBtn = new Button(() =>
                    {
                        RuleConfig.SetThreshold(capturedRuleId, configurable.DefaultThreshold);
                        slider.value = configurable.DefaultThreshold;
                    }) { text = "Reset" };
                    resetThresholdBtn.style.height = 18;
                    resetThresholdBtn.style.fontSize = 9;
                    thresholdRow.Add(resetThresholdBtn);

                    ruleRow.Add(thresholdRow);
                }

                _rulesContainer.Add(ruleRow);
            }

            // Reset All button
            var resetAllBtn = new Button(() =>
            {
                foreach (var rule in rules)
                    RuleConfig.ResetRule(rule.RuleId);
                PopulateRulesUI();
            }) { text = "Reset All Rules to Defaults" };
            resetAllBtn.style.height = 24;
            resetAllBtn.style.marginTop = 8;
            _rulesContainer.Add(resetAllBtn);
        }

        private void UpdateApiKeyVisibility()
        {
            bool needsKey = _llmConfig.Provider != LLMProvider.Ollama;
            _apiKeyField.style.display = needsKey ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private async void OnTestConnection()
        {
            if (!_llmConfig.IsConfigured)
            {
                _llmStatusLabel.text = "Enable AI Analysis and provide an API key first.";
                _llmStatusLabel.style.color = new Color(0.9f, 0.5f, 0.2f);
                return;
            }

            _llmStatusLabel.text = "Testing connection...";
            _llmStatusLabel.style.color = new Color(0.8f, 0.8f, 0.4f);

            var client = new LLMClient(_llmConfig);
            bool ok = await client.TestConnectionAsync();

            _llmStatusLabel.text = ok
                ? $"Connection successful! ({_llmConfig.Provider} / {_llmConfig.ModelId})"
                : "Connection failed. Check API key and endpoint.";
            _llmStatusLabel.style.color = ok
                ? new Color(0.4f, 0.8f, 0.4f)
                : new Color(0.9f, 0.25f, 0.25f);
        }

        // -- Actions --

        private void OnStartProfiling()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("DrWario", "Enter Play Mode before starting profiling.", "OK");
                return;
            }

            RuntimeCollector.StartSession();

            _startBtn.SetEnabled(false);
            _stopBtn.SetEnabled(true);
            _analyzeBtn.SetEnabled(false);
            _statusLabel.text = "Recording... Collecting frame data.";
            _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
        }

        private void OnStopProfiling()
        {
            RuntimeCollector.StopSession();

            _startBtn.SetEnabled(true);
            _stopBtn.SetEnabled(false);
            _analyzeBtn.SetEnabled(true);

            var session = RuntimeCollector.ActiveSession;
            _statusLabel.text = session != null
                ? $"Stopped. {session.FrameCount} frames captured. Click Analyze."
                : "Stopped. No data captured.";
            _statusLabel.style.color = new Color(0.8f, 0.8f, 0.4f);
        }

        private async void OnAnalyze()
        {
            var session = RuntimeCollector.ActiveSession;
            if (session == null || session.FrameCount == 0)
            {
                EditorUtility.DisplayDialog("DrWario", "No profiling data available. Start and stop a session first.", "OK");
                return;
            }

            _analyzeBtn.SetEnabled(false);
            _statusLabel.text = "Analyzing...";
            _statusLabel.style.color = new Color(0.8f, 0.8f, 0.4f);

            _analysisEngine = new AnalysisEngine(_llmConfig);

            bool useStreaming = _llmConfig.IsConfigured &&
                (_llmConfig.Provider == LLMProvider.Claude || _llmConfig.Provider == LLMProvider.OpenAI);

            if (useStreaming)
            {
                // Streaming path: rules run instantly, then AI findings arrive progressively
                _statusLabel.text = "Analyzing... (rule-based done, streaming AI findings)";
                _isStreaming = true;
                ShowStreamingIndicator(true);

                // Show deterministic findings immediately
                _lastReport = _analysisEngine.Analyze(session);
                PopulateSummary();
                PopulateFindings();
                PopulateRecommendations();

                // Subscribe to streaming findings
                _analysisEngine.OnStreamingFindingReceived += OnStreamingFinding;

                try
                {
                    _lastReport = await _analysisEngine.AnalyzeStreamingAsync(session);
                }
                finally
                {
                    _analysisEngine.OnStreamingFindingReceived -= OnStreamingFinding;
                    _isStreaming = false;
                    ShowStreamingIndicator(false);
                }

                string aiStatus = _analysisEngine.AICallSucceeded
                    ? " + AI insights"
                    : $" (AI: {_analysisEngine.AIError ?? "no findings"})";

                _statusLabel.text = $"Analysis complete. Grade: {_lastReport.OverallGrade} ({_lastReport.HealthScore:F0}/100) | {_lastReport.Findings.Count} findings{aiStatus}.";

                // Final full UI refresh with deduplicated findings
                PopulateSummary();
                PopulateFindings();
                PopulateRecommendations();
                PopulateTimeline();
            }
            else if (_llmConfig.IsConfigured)
            {
                // Async path (Ollama/Custom): rules run instantly, then AI runs without blocking
                _statusLabel.text = "Analyzing... (rule-based done, waiting for AI)";
                _lastReport = await _analysisEngine.AnalyzeAsync(session);

                string aiStatus = _analysisEngine.AICallSucceeded
                    ? " + AI insights"
                    : $" (AI: {_analysisEngine.AIError ?? "no findings"})";

                _statusLabel.text = $"Analysis complete. Grade: {_lastReport.OverallGrade} ({_lastReport.HealthScore:F0}/100) | {_lastReport.Findings.Count} findings{aiStatus}.";
                PopulateSummary();
                PopulateFindings();
                PopulateRecommendations();
                PopulateTimeline();
            }
            else
            {
                // Sync path: rules only, instant
                _lastReport = _analysisEngine.Analyze(session);
                _statusLabel.text = $"Analysis complete. Grade: {_lastReport.OverallGrade} ({_lastReport.HealthScore:F0}/100) | {_lastReport.Findings.Count} findings.";
                PopulateSummary();
                PopulateFindings();
                PopulateRecommendations();
                PopulateTimeline();
            }

            _statusLabel.style.color = new Color(0.5f, 0.8f, 1f);
            _analyzeBtn.SetEnabled(true);

            // Auto-save to history
            ReportHistory.Save(_lastReport);
        }

        private void ShowStreamingIndicator(bool show)
        {
            if (_streamingIndicator == null)
            {
                _streamingIndicator = new Label("Streaming...");
                _streamingIndicator.style.fontSize = 11;
                _streamingIndicator.style.color = new Color(0.4f, 0.8f, 0.9f);
                _streamingIndicator.style.unityFontStyleAndWeight = FontStyle.Italic;
                _streamingIndicator.style.marginBottom = 4;
            }

            if (show)
            {
                if (_streamingIndicator.parent == null)
                    _findingsContainer.Insert(0, _streamingIndicator);
                _streamingIndicator.style.display = DisplayStyle.Flex;
            }
            else
            {
                _streamingIndicator.style.display = DisplayStyle.None;
            }
        }

        private void OnStreamingFinding(DiagnosticFinding finding)
        {
            // Add the finding card to the UI progressively
            var card = CreateFindingCard(finding);
            _findingsContainer.Add(card);
        }

        private void OnExportJson()
        {
            if (_lastReport == null) { EditorUtility.DisplayDialog("DrWario", "Run analysis first.", "OK"); return; }
            var path = EditorUtility.SaveFilePanel("Export DrWario Report", "", "drwario-report", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, _lastReport.ExportJson());
                Debug.Log($"[DrWario] Report exported to {path}");
            }
        }

        private void OnExportText()
        {
            if (_lastReport == null) { EditorUtility.DisplayDialog("DrWario", "Run analysis first.", "OK"); return; }
            var path = EditorUtility.SaveFilePanel("Export DrWario Report", "", "drwario-report", "txt");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, _lastReport.ExportText());
                Debug.Log($"[DrWario] Report exported to {path}");
            }
        }

        private void OnExportHtml()
        {
            if (_lastReport == null) { EditorUtility.DisplayDialog("DrWario", "Run analysis first.", "OK"); return; }
            var session = RuntimeCollector.ActiveSession;
            if (session == null || session.FrameCount == 0)
            {
                EditorUtility.DisplayDialog("DrWario", "No active profiling session. HTML export requires session data.", "OK");
                return;
            }
            var path = EditorUtility.SaveFilePanel("Export DrWario HTML Report", "", "drwario-report", "html");
            if (!string.IsNullOrEmpty(path))
            {
                var html = HtmlReportBuilder.Build(_lastReport, session);
                System.IO.File.WriteAllText(path, html);
                Debug.Log($"[DrWario] HTML report exported to {path}");
                _statusLabel.text = $"HTML report exported to {path}";
                _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
            }
        }

        // -- Populate UI --

        private void PopulateSummary()
        {
            // Grade
            _gradeLabel.text = _lastReport.OverallGrade.ToString();
            _gradeLabel.style.color = GradeColor(_lastReport.OverallGrade);

            var scoreLabel = this.Q<Label>("score-subtitle");
            if (scoreLabel != null)
                scoreLabel.text = $"Health Score: {_lastReport.HealthScore:F0}/100 | {_lastReport.Findings.Count} findings";

            // Sparkline data
            var session = RuntimeCollector.ActiveSession;
            if (session != null && session.FrameCount > 0)
            {
                var frames = session.GetFrames();
                _sparklineData = new float[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                    _sparklineData[i] = frames[i].CpuFrameTimeMs;
                _sparklineElement.MarkDirtyRepaint();
            }

            // Rendering stats card (from ProfilerRecorder data)
            if (session != null && session.FrameCount > 0)
            {
                var frames = session.GetFrames();
                bool hasRenderData = frames.Any(f => f.DrawCalls > 0);

                // Bottleneck indicator
                float avgCpu = frames.Average(f => f.CpuFrameTimeMs);
                float avgGpu = frames.Where(f => f.GpuFrameTimeMs > 0).Select(f => f.GpuFrameTimeMs).DefaultIfEmpty(0).Average();

                var statsCard = this.Q<VisualElement>("render-stats-card");
                if (statsCard == null)
                {
                    statsCard = new VisualElement();
                    statsCard.name = "render-stats-card";
                    statsCard.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.20f));
                    statsCard.style.marginBottom = 10;
                    statsCard.style.paddingTop = 8;
                    statsCard.style.paddingBottom = 8;
                    statsCard.style.paddingLeft = 10;
                    statsCard.style.paddingRight = 10;
                    statsCard.style.borderLeftWidth = 3;
                    // Insert after sparkline (before category container's parent)
                    var parent = _categoryContainer.parent;
                    int catIdx = parent.IndexOf(_categoryContainer);
                    parent.Insert(catIdx, statsCard);
                }
                statsCard.Clear();

                string bottleneck;
                Color bottleneckColor;
                if (avgGpu > 0 && avgCpu > 0)
                {
                    if (avgGpu > avgCpu * 1.3f) { bottleneck = "GPU-bound"; bottleneckColor = new Color(0.9f, 0.5f, 0.2f); }
                    else if (avgCpu > avgGpu * 1.3f) { bottleneck = "CPU-bound"; bottleneckColor = new Color(0.9f, 0.7f, 0.2f); }
                    else { bottleneck = "Balanced"; bottleneckColor = new Color(0.4f, 0.8f, 0.4f); }
                }
                else { bottleneck = "GPU data N/A"; bottleneckColor = new Color(0.5f, 0.5f, 0.5f); }

                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;
                headerRow.style.marginBottom = 4;

                var bnLabel = new Label($"Bottleneck: {bottleneck}");
                bnLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                bnLabel.style.color = bottleneckColor;
                bnLabel.style.fontSize = 12;
                headerRow.Add(bnLabel);

                var timingLabel = new Label($"  CPU: {avgCpu:F1}ms  GPU: {(avgGpu > 0 ? $"{avgGpu:F1}ms" : "N/A")}");
                timingLabel.style.fontSize = 10;
                timingLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                timingLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                headerRow.Add(timingLabel);

                statsCard.Add(headerRow);
                statsCard.style.borderLeftColor = bottleneckColor;

                if (hasRenderData)
                {
                    int avgDraw = (int)frames.Where(f => f.DrawCalls > 0).Average(f => f.DrawCalls);
                    int avgBatch = (int)frames.Where(f => f.Batches > 0).Average(f => f.Batches);
                    int avgSetPass = (int)frames.Where(f => f.SetPassCalls > 0).Average(f => f.SetPassCalls);
                    long avgTris = (long)frames.Where(f => f.Triangles > 0).Average(f => f.Triangles);

                    string renderInfo = $"Draw Calls: {avgDraw}  Batches: {avgBatch}  SetPass: {avgSetPass}  Triangles: {avgTris:N0}";
                    var renderLabel = new Label(renderInfo);
                    renderLabel.style.fontSize = 10;
                    renderLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    statsCard.Add(renderLabel);
                }
            }

            // Category cards
            _categoryContainer.Clear();
            foreach (var kv in _lastReport.CategoryGrades)
            {
                var card = new VisualElement();
                card.style.width = 130;
                card.style.minHeight = 80;
                card.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
                card.style.marginRight = 8;
                card.style.marginBottom = 8;
                card.style.paddingTop = 10;
                card.style.paddingBottom = 10;
                card.style.paddingLeft = 10;
                card.style.paddingRight = 10;
                card.style.borderLeftWidth = 3;
                card.style.borderLeftColor = GradeColor(kv.Value);

                var gradeLabel = new Label(kv.Value.ToString());
                gradeLabel.style.fontSize = 28;
                gradeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                gradeLabel.style.color = GradeColor(kv.Value);
                card.Add(gradeLabel);

                var catLabel = new Label(kv.Key);
                catLabel.style.fontSize = 12;
                catLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                card.Add(catLabel);

                int count = _lastReport.Findings.Count(f => f.Category == kv.Key);
                var countLabel = new Label($"{count} finding{(count != 1 ? "s" : "")}");
                countLabel.style.fontSize = 10;
                countLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                card.Add(countLabel);

                _categoryContainer.Add(card);
            }

            // Charts
            AddCharts();
        }

        private void AddCharts()
        {
            var session = RuntimeCollector.ActiveSession;
            if (session == null || session.FrameCount == 0) return;

            var frames = session.GetFrames();
            var parent = _categoryContainer.parent;

            // Remove old charts container if re-populating
            var oldCharts = parent.Q<VisualElement>("charts-container");
            if (oldCharts != null) parent.Remove(oldCharts);

            var chartsContainer = new VisualElement();
            chartsContainer.name = "charts-container";
            chartsContainer.style.marginTop = 8;
            parent.Add(chartsContainer);

            var chartsTitle = new Label("Performance Charts");
            chartsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            chartsTitle.style.fontSize = 13;
            chartsTitle.style.marginBottom = 6;
            chartsContainer.Add(chartsTitle);

            // Memory Trajectory Line Chart
            var memChart = new LineChart();
            memChart.style.height = 120;
            memChart.style.marginBottom = 8;

            int step = Math.Max(1, frames.Length / 12);
            float startTime = frames[0].Timestamp;
            var memValues = new List<float>();
            var memTimes = new List<float>();
            for (int i = 0; i < frames.Length; i += step)
            {
                memValues.Add(frames[i].TotalHeapBytes / (1024f * 1024f));
                memTimes.Add(frames[i].Timestamp - startTime);
            }
            memChart.SetData(memValues.ToArray(), memTimes.ToArray(), "Heap (MB)", new Color(0.3f, 0.7f, 1f));

            chartsContainer.Add(CreateSectionLabel("Memory Trajectory"));
            chartsContainer.Add(memChart);

            // Frame Time Distribution Histogram
            var histogram = new Histogram();
            histogram.style.height = 120;
            histogram.style.marginBottom = 8;
            var cpuTimes = new float[frames.Length];
            for (int i = 0; i < frames.Length; i++) cpuTimes[i] = frames[i].CpuFrameTimeMs;
            histogram.SetData(cpuTimes, 20);

            chartsContainer.Add(CreateSectionLabel("Frame Time Distribution (ms)"));
            chartsContainer.Add(histogram);

            // GC Allocation Bar Chart
            bool hasGc = frames.Any(f => f.GcAllocBytes > 0);
            if (hasGc)
            {
                var gcChart = new BarChart();
                gcChart.style.height = 100;
                gcChart.style.marginBottom = 8;
                gcChart.HighlightThreshold = 1024;

                // Downsample GC data to ~100 bars max
                int gcStep = Math.Max(1, frames.Length / 100);
                var gcValues = new float[frames.Length / gcStep + 1];
                for (int i = 0, j = 0; i < frames.Length && j < gcValues.Length; i += gcStep, j++)
                    gcValues[j] = frames[i].GcAllocBytes;
                gcChart.SetData(gcValues, null, new Color(0.9f, 0.6f, 0.2f));

                chartsContainer.Add(CreateSectionLabel("GC Allocations (bytes/frame)"));
                chartsContainer.Add(gcChart);
            }

            // CPU vs GPU Comparison Bar Chart
            float avgCpuTime = frames.Average(f => f.CpuFrameTimeMs);
            float avgGpuTime = frames.Where(f => f.GpuFrameTimeMs > 0).Select(f => f.GpuFrameTimeMs).DefaultIfEmpty(0).Average();
            if (avgGpuTime > 0)
            {
                var cpuSorted = frames.Select(f => f.CpuFrameTimeMs).OrderBy(t => t).ToArray();
                var gpuSorted = frames.Where(f => f.GpuFrameTimeMs > 0).Select(f => f.GpuFrameTimeMs).OrderBy(t => t).ToArray();

                var cpuGpuChart = new BarChart();
                cpuGpuChart.style.height = 100;
                cpuGpuChart.style.marginBottom = 8;
                float cpuP95 = cpuSorted[(int)(cpuSorted.Length * 0.95f)];
                float gpuP95 = gpuSorted.Length > 0 ? gpuSorted[(int)(gpuSorted.Length * 0.95f)] : 0;

                cpuGpuChart.SetGroupedData(
                    new[] { new[] { avgCpuTime, cpuP95 }, new[] { avgGpuTime, gpuP95 } },
                    new[] { "CPU", "GPU" },
                    new[] { new Color(0.4f, 0.7f, 1f), new Color(0.4f, 0.9f, 0.4f) }
                );

                chartsContainer.Add(CreateSectionLabel("CPU vs GPU (Avg, P95)"));
                chartsContainer.Add(cpuGpuChart);
            }
        }

        private void PopulateFindings()
        {
            _findingsContainer.Clear();
            _expandedCards.Clear();

            if (_lastReport.Findings.Count == 0)
            {
                _findingsContainer.Add(new Label("No issues found. Your application is running clean!")
                    { style = { color = Color.green, marginTop = 10, fontSize = 14 } });
                return;
            }

            // Copy Report button
            var copyReportBtn = new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = _lastReport.ExportText();
                Debug.Log("[DrWario] Report copied to clipboard.");
                _statusLabel.text = "Report copied to clipboard.";
                _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
            }) { text = "Copy Report to Clipboard" };
            copyReportBtn.style.height = 24;
            copyReportBtn.style.marginBottom = 8;
            _findingsContainer.Add(copyReportBtn);

            foreach (var f in _lastReport.Findings.OrderByDescending(f => f.Severity))
            {
                var card = CreateFindingCard(f);
                _findingsContainer.Add(card);
            }

            // Data tables section
            AddDataTables();
        }

        private VisualElement CreateFindingCard(DiagnosticFinding f)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            card.style.marginBottom = 8;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = SeverityColor(f.Severity);

            // Header row (always visible, clickable to expand/collapse)
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;

            var expandArrow = new Label(">");
            expandArrow.style.fontSize = 10;
            expandArrow.style.width = 14;
            expandArrow.style.color = new Color(0.6f, 0.6f, 0.6f);
            expandArrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(expandArrow);

            var severityBadge = new Label(f.Severity.ToString().ToUpper());
            severityBadge.style.fontSize = 9;
            severityBadge.style.color = SeverityColor(f.Severity);
            severityBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            severityBadge.style.width = 60;
            headerRow.Add(severityBadge);

            var titleLabel = new Label(f.Title);
            titleLabel.style.flexGrow = 1;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(titleLabel);

            var catBadge = new Label($"[{f.Category}]");
            catBadge.style.fontSize = 10;
            catBadge.style.color = new Color(0.5f, 0.5f, 0.5f);
            headerRow.Add(catBadge);

            card.Add(headerRow);

            var descLabel = new Label(f.Description);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginTop = 4;
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            card.Add(descLabel);

            // Clickable source references
            AddSourceReferences(card, f);

            // Expandable detail section (hidden by default)
            var detailSection = new VisualElement();
            detailSection.name = "finding-detail";
            detailSection.style.display = DisplayStyle.None;
            detailSection.style.marginTop = 6;
            detailSection.style.paddingTop = 6;
            detailSection.style.borderTopWidth = 1;
            detailSection.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);

            // Affected frames list
            if (f.AffectedFrames != null && f.AffectedFrames.Length > 0)
            {
                var framesHeader = new Label("Affected Frames:");
                framesHeader.style.fontSize = 10;
                framesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                framesHeader.style.color = new Color(0.7f, 0.7f, 0.7f);
                framesHeader.style.marginBottom = 2;
                detailSection.Add(framesHeader);

                int displayCount = System.Math.Min(f.AffectedFrames.Length, 20);
                var frameIndices = new System.Text.StringBuilder();
                for (int i = 0; i < displayCount; i++)
                {
                    if (i > 0) frameIndices.Append(", ");
                    frameIndices.Append(f.AffectedFrames[i]);
                }
                if (f.AffectedFrames.Length > 20)
                    frameIndices.Append($" ... (+{f.AffectedFrames.Length - 20} more)");

                var framesLabel = new Label(frameIndices.ToString());
                framesLabel.style.whiteSpace = WhiteSpace.Normal;
                framesLabel.style.fontSize = 10;
                framesLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                framesLabel.style.marginBottom = 4;
                detailSection.Add(framesLabel);
            }

            // "Show in Profiler" link for frame-specific findings
            if (f.FrameIndex >= 0)
            {
                int frameIdx = f.FrameIndex;
                var profilerLink = new Label("Show in Profiler");
                profilerLink.style.color = new Color(0.4f, 0.7f, 1f);
                profilerLink.style.fontSize = 10;
                profilerLink.style.unityFontStyleAndWeight = FontStyle.Bold;
                profilerLink.style.marginBottom = 4;
                profilerLink.tooltip = $"Navigate Unity Profiler to frame {frameIdx}";
                profilerLink.AddManipulator(new Clickable(() =>
                {
                    ProfilerBridgeEditor.NavigateToFrame(frameIdx);
                }));
                detailSection.Add(profilerLink);
            }

            // Related findings from same category
            if (_lastReport != null)
            {
                var related = _lastReport.Findings
                    .Where(other => other.Category == f.Category && other.Title != f.Title)
                    .Take(5)
                    .ToList();

                if (related.Count > 0)
                {
                    var relatedHeader = new Label("Related Findings:");
                    relatedHeader.style.fontSize = 10;
                    relatedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    relatedHeader.style.color = new Color(0.7f, 0.7f, 0.7f);
                    relatedHeader.style.marginTop = 4;
                    relatedHeader.style.marginBottom = 2;
                    detailSection.Add(relatedHeader);

                    foreach (var rel in related)
                    {
                        var relLabel = new Label($"  {SeverityIcon(rel.Severity)} {rel.Title}");
                        relLabel.style.fontSize = 10;
                        relLabel.style.color = SeverityColor(rel.Severity);
                        detailSection.Add(relLabel);
                    }
                }
            }

            card.Add(detailSection);

            // Click handler for expand/collapse
            var capturedCard = card;
            var capturedDetail = detailSection;
            var capturedArrow = expandArrow;
            headerRow.AddManipulator(new Clickable(() =>
            {
                bool isExpanded = _expandedCards.Contains(capturedCard);
                if (isExpanded)
                {
                    _expandedCards.Remove(capturedCard);
                    capturedDetail.style.display = DisplayStyle.None;
                    capturedArrow.text = ">";
                }
                else
                {
                    _expandedCards.Add(capturedCard);
                    capturedDetail.style.display = DisplayStyle.Flex;
                    capturedArrow.text = "v";
                }
            }));

            return card;
        }

        private void AddDataTables()
        {
            var session = RuntimeCollector.ActiveSession;
            if (session == null) return;

            var frames = session.GetFrames();

            // Section header
            var tablesHeader = new Label("Data Tables");
            tablesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            tablesHeader.style.fontSize = 14;
            tablesHeader.style.marginTop = 16;
            tablesHeader.style.marginBottom = 8;
            _findingsContainer.Add(tablesHeader);

            // Slowest Frames table
            if (frames.Length > 0)
            {
                _findingsContainer.Add(CreateSectionLabel("Slowest Frames"));
                var slowFrames = new List<FrameSample>(frames);
                slowFrames.Sort((a, b) => b.CpuFrameTimeMs.CompareTo(a.CpuFrameTimeMs));
                if (slowFrames.Count > 50) slowFrames.RemoveRange(50, slowFrames.Count - 50);

                var table = DataTableBuilder.Create(
                    new[]
                    {
                        new DataTableBuilder.ColumnDef { Title = "Frame#", Width = 60, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "CPU (ms)", Width = 80, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "GPU (ms)", Width = 80, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Draw Calls", Width = 80, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "GC (bytes)", Width = 80, Sortable = true },
                    },
                    slowFrames,
                    (element, row, col) =>
                    {
                        var label = element as Label;
                        if (label == null || row >= slowFrames.Count) return;
                        var f = slowFrames[row];
                        label.text = col switch
                        {
                            0 => System.Array.IndexOf(frames, f).ToString(),
                            1 => f.CpuFrameTimeMs.ToString("F1"),
                            2 => f.GpuFrameTimeMs.ToString("F1"),
                            3 => f.DrawCalls.ToString(),
                            4 => f.GcAllocBytes.ToString(),
                            _ => ""
                        };
                    },
                    (colIndex, asc) =>
                    {
                        var sorted = new List<FrameSample>(slowFrames);
                        sorted.Sort((a, b) =>
                        {
                            float va = colIndex switch { 1 => a.CpuFrameTimeMs, 2 => a.GpuFrameTimeMs, 3 => a.DrawCalls, 4 => a.GcAllocBytes, _ => 0 };
                            float vb = colIndex switch { 1 => b.CpuFrameTimeMs, 2 => b.GpuFrameTimeMs, 3 => b.DrawCalls, 4 => b.GcAllocBytes, _ => 0 };
                            return asc ? va.CompareTo(vb) : vb.CompareTo(va);
                        });
                        return sorted;
                    },
                    180);
                _findingsContainer.Add(table);
            }

            // GC Allocation Frames table
            if (frames.Length > 0)
            {
                var gcFrames = new List<FrameSample>(frames.Where(f => f.GcAllocBytes > 0));
                gcFrames.Sort((a, b) => b.GcAllocBytes.CompareTo(a.GcAllocBytes));
                if (gcFrames.Count > 50) gcFrames.RemoveRange(50, gcFrames.Count - 50);

                if (gcFrames.Count > 0)
                {
                    _findingsContainer.Add(CreateSectionLabel("Top GC Allocation Frames"));
                    var gcTable = DataTableBuilder.Create(
                        new[]
                        {
                            new DataTableBuilder.ColumnDef { Title = "Frame#", Width = 60, Sortable = true },
                            new DataTableBuilder.ColumnDef { Title = "GC Bytes", Width = 90, Sortable = true },
                            new DataTableBuilder.ColumnDef { Title = "GC Count", Width = 80, Sortable = true },
                            new DataTableBuilder.ColumnDef { Title = "CPU (ms)", Width = 80, Sortable = true },
                        },
                        gcFrames,
                        (element, row, col) =>
                        {
                            var label = element as Label;
                            if (label == null || row >= gcFrames.Count) return;
                            var f = gcFrames[row];
                            label.text = col switch
                            {
                                0 => System.Array.IndexOf(frames, f).ToString(),
                                1 => f.GcAllocBytes.ToString(),
                                2 => f.GcAllocCount.ToString(),
                                3 => f.CpuFrameTimeMs.ToString("F1"),
                                _ => ""
                            };
                        },
                        (colIndex, asc) =>
                        {
                            var sorted = new List<FrameSample>(gcFrames);
                            sorted.Sort((a, b) =>
                            {
                                float va = colIndex switch { 1 => a.GcAllocBytes, 2 => a.GcAllocCount, 3 => a.CpuFrameTimeMs, _ => 0 };
                                float vb = colIndex switch { 1 => b.GcAllocBytes, 2 => b.GcAllocCount, 3 => b.CpuFrameTimeMs, _ => 0 };
                                return asc ? va.CompareTo(vb) : vb.CompareTo(va);
                            });
                            return sorted;
                        },
                        160);
                    _findingsContainer.Add(gcTable);
                }
            }

            // Asset Load Times table
            if (session.AssetLoads.Count > 0)
            {
                _findingsContainer.Add(CreateSectionLabel("Asset Load Times"));
                var assetLoads = new List<AssetLoadTiming>(session.AssetLoads);
                assetLoads.Sort((a, b) => b.DurationMs.CompareTo(a.DurationMs));

                var assetTable = DataTableBuilder.Create(
                    new[]
                    {
                        new DataTableBuilder.ColumnDef { Title = "Asset Key", Width = 200, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Duration (ms)", Width = 100, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Size (bytes)", Width = 100, Sortable = true },
                    },
                    assetLoads,
                    (element, row, col) =>
                    {
                        var label = element as Label;
                        if (label == null || row >= assetLoads.Count) return;
                        var a = assetLoads[row];
                        label.text = col switch
                        {
                            0 => a.AssetKey ?? "",
                            1 => a.DurationMs.ToString(),
                            2 => a.SizeBytes.ToString(),
                            _ => ""
                        };
                    },
                    (colIndex, asc) =>
                    {
                        var sorted = new List<AssetLoadTiming>(assetLoads);
                        sorted.Sort((a, b) =>
                        {
                            long va = colIndex switch { 1 => a.DurationMs, 2 => a.SizeBytes, _ => 0 };
                            long vb = colIndex switch { 1 => b.DurationMs, 2 => b.SizeBytes, _ => 0 };
                            return asc ? va.CompareTo(vb) : vb.CompareTo(va);
                        });
                        return sorted;
                    },
                    140);
                _findingsContainer.Add(assetTable);
            }

            // Boot Stages table
            if (session.BootStages.Count > 0)
            {
                _findingsContainer.Add(CreateSectionLabel("Boot Stages"));
                var stages = new List<BootStageTiming>(session.BootStages);

                var bootTable = DataTableBuilder.Create(
                    new[]
                    {
                        new DataTableBuilder.ColumnDef { Title = "Stage Name", Width = 180, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Duration (ms)", Width = 100, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Status", Width = 80, Sortable = false },
                    },
                    stages,
                    (element, row, col) =>
                    {
                        var label = element as Label;
                        if (label == null || row >= stages.Count) return;
                        var s = stages[row];
                        label.text = col switch
                        {
                            0 => s.StageName ?? "",
                            1 => s.DurationMs.ToString(),
                            2 => s.Success ? "OK" : "FAILED",
                            _ => ""
                        };
                        if (col == 2)
                            label.style.color = s.Success ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.25f, 0.25f);
                    },
                    (colIndex, asc) =>
                    {
                        var sorted = new List<BootStageTiming>(stages);
                        if (colIndex == 1)
                            sorted.Sort((a, b) => asc ? a.DurationMs.CompareTo(b.DurationMs) : b.DurationMs.CompareTo(a.DurationMs));
                        return sorted;
                    },
                    120);
                _findingsContainer.Add(bootTable);
            }

            // Network Events table
            if (session.NetworkEvents.Count > 0)
            {
                _findingsContainer.Add(CreateSectionLabel("Network Events"));
                var events = new List<NetworkEvent>(session.NetworkEvents);

                var netTable = DataTableBuilder.Create(
                    new[]
                    {
                        new DataTableBuilder.ColumnDef { Title = "Timestamp", Width = 80, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Type", Width = 80, Sortable = false },
                        new DataTableBuilder.ColumnDef { Title = "Bytes", Width = 80, Sortable = true },
                        new DataTableBuilder.ColumnDef { Title = "Latency (ms)", Width = 90, Sortable = true },
                    },
                    events,
                    (element, row, col) =>
                    {
                        var label = element as Label;
                        if (label == null || row >= events.Count) return;
                        var e = events[row];
                        label.text = col switch
                        {
                            0 => e.Timestamp.ToString("F2"),
                            1 => e.Type.ToString(),
                            2 => e.Bytes.ToString(),
                            3 => e.LatencyMs.ToString("F1"),
                            _ => ""
                        };
                    },
                    (colIndex, asc) =>
                    {
                        var sorted = new List<NetworkEvent>(events);
                        sorted.Sort((a, b) =>
                        {
                            float va = colIndex switch { 0 => a.Timestamp, 2 => a.Bytes, 3 => a.LatencyMs, _ => 0 };
                            float vb = colIndex switch { 0 => b.Timestamp, 2 => b.Bytes, 3 => b.LatencyMs, _ => 0 };
                            return asc ? va.CompareTo(vb) : vb.CompareTo(va);
                        });
                        return sorted;
                    },
                    140);
                _findingsContainer.Add(netTable);
            }
        }

        private Label CreateSectionLabel(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginTop = 12,
                    marginBottom = 4,
                    color = new Color(0.8f, 0.8f, 0.8f)
                }
            };
        }

        private void PopulateRecommendations()
        {
            _recommendationsContainer.Clear();

            if (_lastReport.Findings.Count == 0)
            {
                _recommendationsContainer.Add(new Label("No recommendations. Everything looks good!")
                    { style = { color = Color.green, marginTop = 10, fontSize = 14 } });
                return;
            }

            // Copy Recommendations button
            var copyRecsBtn = new Button(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"DrWario Recommendations — Grade: {_lastReport.OverallGrade} ({_lastReport.HealthScore:F0}/100)");
                sb.AppendLine();
                foreach (var f in _lastReport.Findings.OrderByDescending(f => f.Severity))
                {
                    sb.AppendLine($"[{f.Severity}] {f.Title} ({f.Category})");
                    sb.AppendLine($"  → {f.Recommendation}");
                    sb.AppendLine();
                }
                EditorGUIUtility.systemCopyBuffer = sb.ToString();
                Debug.Log("[DrWario] Recommendations copied to clipboard.");
                _statusLabel.text = "Recommendations copied to clipboard.";
                _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
            }) { text = "Copy Recommendations to Clipboard" };
            copyRecsBtn.style.height = 24;
            copyRecsBtn.style.marginBottom = 8;
            _recommendationsContainer.Add(copyRecsBtn);

            // Group by category
            var grouped = _lastReport.Findings
                .OrderByDescending(f => f.Severity)
                .GroupBy(f => f.Category);

            foreach (var group in grouped)
            {
                var catHeader = new Label(group.Key);
                catHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                catHeader.style.fontSize = 14;
                catHeader.style.marginTop = 10;
                catHeader.style.marginBottom = 5;
                _recommendationsContainer.Add(catHeader);

                foreach (var f in group)
                {
                    var row = new VisualElement();
                    row.style.marginBottom = 6;
                    row.style.paddingLeft = 10;

                    var title = new Label($"{SeverityIcon(f.Severity)} {f.Title}");
                    title.style.fontSize = 11;
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                    title.style.color = SeverityColor(f.Severity);
                    row.Add(title);

                    var rec = new Label($"   -> {f.Recommendation}");
                    rec.style.whiteSpace = WhiteSpace.Normal;
                    rec.style.fontSize = 11;
                    rec.style.color = new Color(0.7f, 0.7f, 0.7f);
                    row.Add(rec);

                    _recommendationsContainer.Add(row);
                }
            }
        }

        // -- Ask the Doctor --

        private VisualElement CreateAskSection()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            _askContainer = new VisualElement();
            _askContainer.style.paddingTop = 10;
            _askContainer.style.paddingBottom = 10;
            _askContainer.style.paddingLeft = 10;
            _askContainer.style.paddingRight = 10;
            scroll.Add(_askContainer);

            var header = new Label("Ask the Doctor");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 14;
            header.style.marginBottom = 5;
            _askContainer.Add(header);

            var desc = new Label("Ask a free-form question about your profiling data. " +
                                 "The AI will analyze your current session and report to provide an answer.");
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.fontSize = 11;
            desc.style.color = new Color(0.6f, 0.6f, 0.6f);
            desc.style.marginBottom = 10;
            _askContainer.Add(desc);

            _askInput = new TextField();
            _askInput.multiline = true;
            _askInput.style.minHeight = 60;
            _askInput.style.marginBottom = 8;
            _askInput.style.whiteSpace = WhiteSpace.Normal;
            _askInput.value = "";
            _askContainer.Add(_askInput);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginBottom = 10;

            _askBtn = new Button(OnAskDoctor) { text = "Ask" };
            _askBtn.style.height = 28;
            _askBtn.style.width = 100;
            _askBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.35f, 0.55f));
            btnRow.Add(_askBtn);

            _copyPromptBtn = new Button(OnCopyPrompt) { text = "Copy Prompt" };
            _copyPromptBtn.style.height = 28;
            _copyPromptBtn.tooltip = "Copy the full prompt (system context + profiling data + question) to clipboard for use with any LLM";
            btnRow.Add(_copyPromptBtn);

            var clearBtn = new Button(() =>
            {
                _askInput.value = "";
                _askResponseLabel.text = "";
                _askResponseLabel.style.display = DisplayStyle.None;
                _copyResponseBtn.style.display = DisplayStyle.None;
            }) { text = "Clear" };
            clearBtn.style.height = 28;
            btnRow.Add(clearBtn);

            _askContainer.Add(btnRow);

            // Example questions
            var examplesCard = new VisualElement();
            examplesCard.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            examplesCard.style.marginBottom = 10;
            examplesCard.style.paddingTop = 8;
            examplesCard.style.paddingBottom = 8;
            examplesCard.style.paddingLeft = 10;
            examplesCard.style.paddingRight = 10;
            examplesCard.style.borderLeftWidth = 3;
            examplesCard.style.borderLeftColor = new Color(0.4f, 0.7f, 0.9f);

            examplesCard.Add(new Label("Example questions:")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginBottom = 4 } });

            string[] examples =
            {
                "What is causing my frame drops?",
                "Is there a memory leak? What should I investigate?",
                "Which boot stage should I optimize first?",
                "How can I reduce GC allocations in my game loop?",
                "Are my asset load times acceptable for mobile?"
            };

            foreach (var ex in examples)
            {
                var exRow = new VisualElement();
                exRow.style.flexDirection = FlexDirection.Row;
                exRow.style.marginBottom = 2;

                var exBtn = new Button(() => _askInput.value = ex) { text = ex };
                exBtn.style.height = 22;
                exBtn.style.fontSize = 10;
                exBtn.style.flexGrow = 1;
                exBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                exBtn.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.18f));
                exRow.Add(exBtn);

                var capturedEx = ex;
                var exCopyBtn = new Button(() => OnCopyPromptForQuestion(capturedEx)) { text = "Copy" };
                exCopyBtn.style.height = 22;
                exCopyBtn.style.width = 42;
                exCopyBtn.style.fontSize = 9;
                exCopyBtn.tooltip = "Copy full prompt for this question to clipboard";
                exRow.Add(exCopyBtn);

                examplesCard.Add(exRow);
            }

            _askContainer.Add(examplesCard);

            // Response area with copy button
            var responseHeader = new VisualElement();
            responseHeader.style.flexDirection = FlexDirection.Row;
            responseHeader.style.justifyContent = Justify.FlexEnd;
            responseHeader.style.marginBottom = 2;

            _copyResponseBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_askResponseLabel.text))
                {
                    EditorGUIUtility.systemCopyBuffer = _askResponseLabel.text;
                    Debug.Log("[DrWario] Response copied to clipboard.");
                }
            }) { text = "Copy Response" };
            _copyResponseBtn.style.height = 22;
            _copyResponseBtn.style.fontSize = 10;
            _copyResponseBtn.style.display = DisplayStyle.None;
            responseHeader.Add(_copyResponseBtn);

            var copyLastPromptBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_lastFullPrompt))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastFullPrompt;
                    Debug.Log("[DrWario] Last prompt copied to clipboard.");
                }
            }) { text = "Copy Last Prompt" };
            copyLastPromptBtn.style.height = 22;
            copyLastPromptBtn.style.fontSize = 10;
            copyLastPromptBtn.style.display = DisplayStyle.None;
            copyLastPromptBtn.name = "copy-last-prompt-btn";
            responseHeader.Add(copyLastPromptBtn);

            _askContainer.Add(responseHeader);

            _askResponseLabel = new Label("");
            _askResponseLabel.style.whiteSpace = WhiteSpace.Normal;
            _askResponseLabel.style.fontSize = 12;
            _askResponseLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            _askResponseLabel.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            _askResponseLabel.style.paddingTop = 10;
            _askResponseLabel.style.paddingBottom = 10;
            _askResponseLabel.style.paddingLeft = 10;
            _askResponseLabel.style.paddingRight = 10;
            _askResponseLabel.style.minHeight = 100;
            _askResponseLabel.style.display = DisplayStyle.None;
            _askResponseLabel.selection.isSelectable = true;
            _askContainer.Add(_askResponseLabel);

            return scroll;
        }

        private void OnCopyPrompt()
        {
            string question = _askInput.value;
            if (string.IsNullOrWhiteSpace(question))
            {
                EditorUtility.DisplayDialog("DrWario", "Enter a question first.", "OK");
                return;
            }

            var session = RuntimeCollector.ActiveSession;
            string prompt = LLMPromptBuilder.BuildFullPromptForClipboard(session, _lastReport, question);
            EditorGUIUtility.systemCopyBuffer = prompt;
            Debug.Log($"[DrWario] Full prompt copied to clipboard ({prompt.Length} chars).");
            _statusLabel.text = $"Prompt copied to clipboard ({prompt.Length} chars, ~{prompt.Length / 4} tokens est.)";
            _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
        }

        private void OnCopyPromptForQuestion(string question)
        {
            var session = RuntimeCollector.ActiveSession;
            string prompt = LLMPromptBuilder.BuildFullPromptForClipboard(session, _lastReport, question);
            EditorGUIUtility.systemCopyBuffer = prompt;
            Debug.Log($"[DrWario] Prompt for \"{question}\" copied to clipboard ({prompt.Length} chars).");
            _statusLabel.text = $"Prompt copied ({prompt.Length} chars). Paste into any LLM chat.";
            _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
        }

        private async void OnAskDoctor()
        {
            if (string.IsNullOrWhiteSpace(_askInput.value))
            {
                EditorUtility.DisplayDialog("DrWario", "Please enter a question.", "OK");
                return;
            }

            if (!_llmConfig.IsConfigured)
            {
                // If LLM not configured, copy prompt to clipboard instead
                OnCopyPrompt();
                EditorUtility.DisplayDialog("DrWario",
                    "LLM is not configured. The full prompt has been copied to your clipboard — paste it into any LLM chat (Claude, ChatGPT, etc.).\n\nTo use the built-in AI, go to LLM Settings and configure a provider.", "OK");
                return;
            }

            _askBtn.SetEnabled(false);
            _askResponseLabel.style.display = DisplayStyle.Flex;
            _askResponseLabel.text = "Thinking...";
            _askResponseLabel.style.color = new Color(0.8f, 0.8f, 0.4f);
            _copyResponseBtn.style.display = DisplayStyle.None;

            string systemPrompt = LLMPromptBuilder.BuildAskDoctorSystemPrompt();

            var userSb = new System.Text.StringBuilder();

            // Attach profiling context if available
            var session = RuntimeCollector.ActiveSession;
            if (session != null && session.FrameCount > 0)
            {
                var findings = _lastReport?.Findings ?? new List<DiagnosticFinding>();
                string profilingData = LLMPromptBuilder.BuildUserPrompt(session, findings);
                userSb.AppendLine("=== PROFILING DATA ===");
                userSb.AppendLine(profilingData);
                userSb.AppendLine();
            }

            if (_lastReport != null)
            {
                userSb.AppendLine("=== ANALYSIS REPORT ===");
                userSb.AppendLine($"Grade: {_lastReport.OverallGrade} | Score: {_lastReport.HealthScore:F0}/100");
                foreach (var kv in _lastReport.CategoryGrades)
                    userSb.AppendLine($"  [{kv.Value}] {kv.Key}");
                userSb.AppendLine();
                foreach (var f in _lastReport.Findings.OrderByDescending(f => f.Severity))
                {
                    userSb.AppendLine($"- [{f.Severity}] {f.Title}: {f.Description}");
                    userSb.AppendLine($"  Recommendation: {f.Recommendation}");
                }
                userSb.AppendLine();
            }

            userSb.AppendLine("=== QUESTION ===");
            userSb.AppendLine(_askInput.value);

            // Store full prompt for copy
            _lastFullPrompt = "=== SYSTEM ===\n" + systemPrompt + "\n\n" + userSb;

            var client = new LLMClient(_llmConfig);
            var response = await client.SendAsync(systemPrompt, userSb.ToString());

            _askBtn.SetEnabled(true);

            if (response.IsSuccess)
            {
                _askResponseLabel.text = response.Content;
                _askResponseLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
                _copyResponseBtn.style.display = DisplayStyle.Flex;
                var lastPromptBtn = this.Q<Button>("copy-last-prompt-btn");
                if (lastPromptBtn != null) lastPromptBtn.style.display = DisplayStyle.Flex;
            }
            else
            {
                _askResponseLabel.text = $"Error: {response.ErrorMessage}";
                _askResponseLabel.style.color = new Color(0.9f, 0.25f, 0.25f);
                _copyResponseBtn.style.display = DisplayStyle.None;
            }
        }

        // -- History --

        private VisualElement CreateHistorySection()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            _historyContainer = new VisualElement();
            _historyContainer.style.paddingTop = 10;
            _historyContainer.style.paddingBottom = 10;
            _historyContainer.style.paddingLeft = 10;
            _historyContainer.style.paddingRight = 10;
            scroll.Add(_historyContainer);
            return scroll;
        }

        private void RefreshHistory()
        {
            _historyContainer.Clear();
            _selectedCompareReport = null;
            _selectedCompareLabel = null;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 10;

            var refreshBtn = new Button(RefreshHistory) { text = "Refresh" };
            refreshBtn.style.height = 24;
            toolbar.Add(refreshBtn);

            var clearBtn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("DrWario", "Delete all saved reports?", "Delete All", "Cancel"))
                {
                    ReportHistory.ClearAll();
                    RefreshHistory();
                }
            }) { text = "Clear All" };
            clearBtn.style.height = 24;
            toolbar.Add(clearBtn);

            _historyContainer.Add(toolbar);

            var reports = ReportHistory.ListReports();
            if (reports.Count == 0)
            {
                _historyContainer.Add(new Label("No saved reports yet. Run an analysis to create one.")
                    { style = { color = new Color(0.6f, 0.6f, 0.6f), marginTop = 10 } });
                return;
            }

            // Instruction label for comparison
            if (reports.Count >= 2)
            {
                var compareHint = new Label("Click \"Compare\" on a report to select it as the baseline, then click \"Compare\" on another report.");
                compareHint.style.fontSize = 10;
                compareHint.style.color = new Color(0.5f, 0.5f, 0.5f);
                compareHint.style.marginBottom = 6;
                compareHint.name = "compare-hint";
                _historyContainer.Add(compareHint);
            }

            foreach (var r in reports)
            {
                var card = new VisualElement();
                card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
                card.style.marginBottom = 6;
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.paddingLeft = 10;
                card.style.paddingRight = 10;
                card.style.borderLeftWidth = 3;
                card.style.borderLeftColor = GradeColor(r.Grade);
                card.style.flexDirection = FlexDirection.Row;

                var gradeLabel = new Label(r.Grade.ToString());
                gradeLabel.style.fontSize = 22;
                gradeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                gradeLabel.style.color = GradeColor(r.Grade);
                gradeLabel.style.width = 40;
                card.Add(gradeLabel);

                var infoCol = new VisualElement();
                infoCol.style.flexGrow = 1;

                var dateLabel = new Label(r.GeneratedAt ?? r.FileName);
                dateLabel.style.fontSize = 11;
                dateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                infoCol.Add(dateLabel);

                var detailLabel = new Label($"{r.HealthScore:F0}/100 | {r.FindingsCount} findings | {r.Platform}");
                detailLabel.style.fontSize = 10;
                detailLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                infoCol.Add(detailLabel);

                card.Add(infoCol);

                // Compare button
                var capturedPath = r.FilePath;
                var capturedLabel = r.GeneratedAt ?? r.FileName;

                if (reports.Count >= 2)
                {
                    var compareBtn = new Button(() => OnCompareClicked(capturedPath, capturedLabel)) { text = "Compare" };
                    compareBtn.style.width = 60;
                    compareBtn.style.height = 24;
                    compareBtn.style.marginRight = 4;
                    compareBtn.style.fontSize = 10;
                    card.Add(compareBtn);
                }

                var deleteBtn = new Button(() =>
                {
                    ReportHistory.DeleteReport(capturedPath);
                    RefreshHistory();
                }) { text = "X" };
                deleteBtn.style.width = 24;
                deleteBtn.style.height = 24;
                card.Add(deleteBtn);

                _historyContainer.Add(card);
            }
        }

        private void OnCompareClicked(string filePath, string label)
        {
            var report = ReportHistory.LoadReport(filePath);
            if (report == null)
            {
                EditorUtility.DisplayDialog("DrWario", "Failed to load report for comparison.", "OK");
                return;
            }

            if (_selectedCompareReport == null)
            {
                // First selection (Report A - the baseline/older report)
                _selectedCompareReport = report;
                _selectedCompareLabel = label;

                var hint = _historyContainer.Q<Label>("compare-hint");
                if (hint != null)
                {
                    hint.text = $"Baseline selected: {label} (Grade {report.OverallGrade}). Now click \"Compare\" on another report.";
                    hint.style.color = new Color(0.4f, 0.8f, 0.4f);
                }
            }
            else
            {
                // Second selection (Report B - the newer report to compare against)
                var comparison = new ReportComparison(_selectedCompareReport, report);
                ShowComparisonView(comparison, _selectedCompareLabel, label);
            }
        }

        private void ShowComparisonView(ReportComparison comparison, string labelA, string labelB)
        {
            _historyContainer.Clear();

            // Back button
            var backBtn = new Button(RefreshHistory) { text = "Back to History" };
            backBtn.style.height = 28;
            backBtn.style.marginBottom = 12;
            backBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.3f));
            _historyContainer.Add(backBtn);

            // Platform/version mismatch warning
            bool platformMismatch = comparison.ReportA.Session.Platform != comparison.ReportB.Session.Platform;
            bool versionMismatch = comparison.ReportA.Session.UnityVersion != comparison.ReportB.Session.UnityVersion;
            if (platformMismatch || versionMismatch)
            {
                var warnBanner = new Label(
                    platformMismatch && versionMismatch
                        ? $"Warning: Reports are from different platforms ({comparison.ReportA.Session.Platform} vs {comparison.ReportB.Session.Platform}) and Unity versions ({comparison.ReportA.Session.UnityVersion} vs {comparison.ReportB.Session.UnityVersion}). Comparison may not be meaningful."
                        : platformMismatch
                            ? $"Warning: Reports are from different platforms ({comparison.ReportA.Session.Platform} vs {comparison.ReportB.Session.Platform}). Comparison may not be meaningful."
                            : $"Warning: Reports are from different Unity versions ({comparison.ReportA.Session.UnityVersion} vs {comparison.ReportB.Session.UnityVersion}). Results may differ."
                );
                warnBanner.style.backgroundColor = new StyleColor(new Color(0.5f, 0.4f, 0.1f));
                warnBanner.style.color = new Color(1f, 0.9f, 0.5f);
                warnBanner.style.paddingTop = 6;
                warnBanner.style.paddingBottom = 6;
                warnBanner.style.paddingLeft = 10;
                warnBanner.style.paddingRight = 10;
                warnBanner.style.marginBottom = 12;
                warnBanner.style.fontSize = 11;
                warnBanner.style.whiteSpace = WhiteSpace.Normal;
                _historyContainer.Add(warnBanner);
            }

            // Header
            var compHeader = new Label("Report Comparison");
            compHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            compHeader.style.fontSize = 16;
            compHeader.style.marginBottom = 4;
            _historyContainer.Add(compHeader);

            var subHeader = new Label($"{labelA}  vs  {labelB}");
            subHeader.style.fontSize = 11;
            subHeader.style.color = new Color(0.6f, 0.6f, 0.6f);
            subHeader.style.marginBottom = 12;
            _historyContainer.Add(subHeader);

            // Overall grade delta
            var overallCard = new VisualElement();
            overallCard.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            overallCard.style.paddingTop = 12;
            overallCard.style.paddingBottom = 12;
            overallCard.style.paddingLeft = 16;
            overallCard.style.paddingRight = 16;
            overallCard.style.marginBottom = 12;
            overallCard.style.borderLeftWidth = 4;

            float scoreDelta = comparison.OverallGradeDelta;
            bool improved = scoreDelta > 0;
            overallCard.style.borderLeftColor = improved
                ? new Color(0.3f, 0.85f, 0.4f)
                : (scoreDelta < 0 ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.5f, 0.5f, 0.5f));

            var gradeRow = new VisualElement();
            gradeRow.style.flexDirection = FlexDirection.Row;
            gradeRow.style.alignItems = Align.Center;

            var gradeALabel = new Label(comparison.ReportA.OverallGrade.ToString());
            gradeALabel.style.fontSize = 48;
            gradeALabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            gradeALabel.style.color = GradeColor(comparison.ReportA.OverallGrade);
            gradeRow.Add(gradeALabel);

            var arrowLabel = new Label("  ->  ");
            arrowLabel.style.fontSize = 24;
            arrowLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            gradeRow.Add(arrowLabel);

            var gradeBLabel = new Label(comparison.ReportB.OverallGrade.ToString());
            gradeBLabel.style.fontSize = 48;
            gradeBLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            gradeBLabel.style.color = GradeColor(comparison.ReportB.OverallGrade);
            gradeRow.Add(gradeBLabel);

            string deltaSign = scoreDelta >= 0 ? "+" : "";
            var overallDeltaLabel = new Label($"  ({deltaSign}{scoreDelta:F0})");
            overallDeltaLabel.style.fontSize = 20;
            overallDeltaLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            overallDeltaLabel.style.color = improved
                ? new Color(0.3f, 0.85f, 0.4f)
                : (scoreDelta < 0 ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.5f, 0.5f, 0.5f));
            gradeRow.Add(overallDeltaLabel);

            overallCard.Add(gradeRow);

            var scoreDetail = new Label($"Health Score: {comparison.ReportA.HealthScore:F0} -> {comparison.ReportB.HealthScore:F0}");
            scoreDetail.style.fontSize = 11;
            scoreDetail.style.color = new Color(0.6f, 0.6f, 0.6f);
            scoreDetail.style.marginTop = 4;
            overallCard.Add(scoreDetail);

            _historyContainer.Add(overallCard);

            // Per-category grade deltas
            if (comparison.CategoryDeltas.Count > 0)
            {
                _historyContainer.Add(CreateSectionLabel("Category Grades"));

                var catContainer = new VisualElement();
                catContainer.style.flexDirection = FlexDirection.Row;
                catContainer.style.flexWrap = Wrap.Wrap;
                catContainer.style.marginBottom = 12;

                foreach (var kvp in comparison.CategoryDeltas)
                {
                    var gd = kvp.Value;
                    var catCard = new VisualElement();
                    catCard.style.width = 130;
                    catCard.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
                    catCard.style.marginRight = 8;
                    catCard.style.marginBottom = 8;
                    catCard.style.paddingTop = 8;
                    catCard.style.paddingBottom = 8;
                    catCard.style.paddingLeft = 10;
                    catCard.style.paddingRight = 10;
                    catCard.style.borderLeftWidth = 3;

                    bool catImproved = gd.ScoreDelta > 0;
                    catCard.style.borderLeftColor = catImproved
                        ? new Color(0.3f, 0.85f, 0.4f)
                        : (gd.ScoreDelta < 0 ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.5f, 0.5f, 0.5f));

                    var catGradeRow = new VisualElement();
                    catGradeRow.style.flexDirection = FlexDirection.Row;
                    catGradeRow.style.alignItems = Align.Center;

                    var catGradeA = new Label(gd.GradeA.ToString());
                    catGradeA.style.fontSize = 20;
                    catGradeA.style.unityFontStyleAndWeight = FontStyle.Bold;
                    catGradeA.style.color = GradeColor(gd.GradeA);
                    catGradeRow.Add(catGradeA);

                    var catArrow = new Label(" -> ");
                    catArrow.style.fontSize = 12;
                    catArrow.style.color = new Color(0.5f, 0.5f, 0.5f);
                    catGradeRow.Add(catArrow);

                    var catGradeB = new Label(gd.GradeB.ToString());
                    catGradeB.style.fontSize = 20;
                    catGradeB.style.unityFontStyleAndWeight = FontStyle.Bold;
                    catGradeB.style.color = GradeColor(gd.GradeB);
                    catGradeRow.Add(catGradeB);

                    catCard.Add(catGradeRow);

                    var catName = new Label(gd.Category);
                    catName.style.fontSize = 11;
                    catName.style.color = new Color(0.7f, 0.7f, 0.7f);
                    catName.style.marginTop = 2;
                    catCard.Add(catName);

                    catContainer.Add(catCard);
                }

                _historyContainer.Add(catContainer);
            }

            // Metric deltas
            _historyContainer.Add(CreateSectionLabel("Metric Deltas"));

            var metricsCard = new VisualElement();
            metricsCard.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.22f));
            metricsCard.style.paddingTop = 10;
            metricsCard.style.paddingBottom = 10;
            metricsCard.style.paddingLeft = 12;
            metricsCard.style.paddingRight = 12;
            metricsCard.style.marginBottom = 12;

            var md = comparison.MetricDeltas;
            AddMetricDeltaRow(metricsCard, "Avg CPU Time", comparison.ReportA.AvgCpuTimeMs, comparison.ReportB.AvgCpuTimeMs, md.AvgCpuTimeDelta, "ms", true);
            AddMetricDeltaRow(metricsCard, "P95 CPU Time", comparison.ReportA.P95CpuTimeMs, comparison.ReportB.P95CpuTimeMs, md.P95CpuTimeDelta, "ms", true);
            AddMetricDeltaRow(metricsCard, "GC Alloc Rate", comparison.ReportA.AvgGcAllocBytes, comparison.ReportB.AvgGcAllocBytes, md.GcRateDelta, "B/frame", true);
            AddMetricDeltaRow(metricsCard, "Memory Slope", comparison.ReportA.MemorySlope, comparison.ReportB.MemorySlope, md.MemorySlopeDelta, "B/frame", true);
            AddMetricDeltaRow(metricsCard, "Draw Calls", comparison.ReportA.AvgDrawCalls, comparison.ReportB.AvgDrawCalls, md.DrawCallsDelta, "", true);

            _historyContainer.Add(metricsCard);

            // Finding diffs
            if (comparison.FindingDiffs.Count > 0)
            {
                int fixedCount = comparison.FindingDiffs.Count(d => d.Status == FindingDiffStatus.Fixed);
                int newCount = comparison.FindingDiffs.Count(d => d.Status == FindingDiffStatus.New);
                int persistsCount = comparison.FindingDiffs.Count(d => d.Status == FindingDiffStatus.Persists);

                _historyContainer.Add(CreateSectionLabel($"Finding Changes ({fixedCount} fixed, {newCount} new, {persistsCount} persists)"));

                // Fixed findings (green)
                foreach (var diff in comparison.FindingDiffs.Where(d => d.Status == FindingDiffStatus.Fixed)
                             .OrderByDescending(d => d.Finding.Severity))
                {
                    AddFindingDiffCard(diff, new Color(0.3f, 0.85f, 0.4f), "FIXED");
                }

                // New findings (red)
                foreach (var diff in comparison.FindingDiffs.Where(d => d.Status == FindingDiffStatus.New)
                             .OrderByDescending(d => d.Finding.Severity))
                {
                    AddFindingDiffCard(diff, new Color(0.9f, 0.25f, 0.25f), "NEW");
                }

                // Persisting findings (gray)
                foreach (var diff in comparison.FindingDiffs.Where(d => d.Status == FindingDiffStatus.Persists)
                             .OrderByDescending(d => d.Finding.Severity))
                {
                    AddFindingDiffCard(diff, new Color(0.5f, 0.5f, 0.5f), "PERSISTS");
                }
            }
            else
            {
                _historyContainer.Add(new Label("No findings to compare.")
                    { style = { color = new Color(0.6f, 0.6f, 0.6f), marginTop = 6 } });
            }
        }

        private void AddMetricDeltaRow(VisualElement container, string metricName, float valueA, float valueB, float delta, string unit, bool lowerIsBetter)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;
            row.style.alignItems = Align.Center;

            var nameLabel = new Label(metricName);
            nameLabel.style.width = 120;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            row.Add(nameLabel);

            string valuesText = string.IsNullOrEmpty(unit)
                ? $"{valueA:F1} -> {valueB:F1}"
                : $"{valueA:F2} -> {valueB:F2} {unit}";
            var valuesLabel = new Label(valuesText);
            valuesLabel.style.flexGrow = 1;
            valuesLabel.style.fontSize = 11;
            valuesLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            row.Add(valuesLabel);

            string sign = delta >= 0 ? "+" : "";
            bool isImprovement = lowerIsBetter ? delta < 0 : delta > 0;
            bool isRegression = lowerIsBetter ? delta > 0 : delta < 0;

            var deltaLbl = new Label($"{sign}{delta:F2}{(string.IsNullOrEmpty(unit) ? "" : " " + unit)}");
            deltaLbl.style.width = 100;
            deltaLbl.style.fontSize = 11;
            deltaLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            deltaLbl.style.unityTextAlign = TextAnchor.MiddleRight;
            deltaLbl.style.color = isImprovement
                ? new Color(0.3f, 0.85f, 0.4f)
                : (isRegression ? new Color(0.9f, 0.25f, 0.25f) : new Color(0.5f, 0.5f, 0.5f));
            row.Add(deltaLbl);

            container.Add(row);
        }

        private void AddFindingDiffCard(FindingDiff diff, Color statusColor, string statusText)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            card.style.marginBottom = 6;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = statusColor;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var badge = new Label(statusText);
            badge.style.fontSize = 9;
            badge.style.color = statusColor;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.width = 60;
            headerRow.Add(badge);

            var sevBadge = new Label(diff.Finding.Severity.ToString().ToUpper());
            sevBadge.style.fontSize = 9;
            sevBadge.style.color = SeverityColor(diff.Finding.Severity);
            sevBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            sevBadge.style.width = 55;
            headerRow.Add(sevBadge);

            var title = new Label(diff.Finding.Title);
            title.style.flexGrow = 1;
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(title);

            var catLabel = new Label($"[{diff.Finding.Category}]");
            catLabel.style.fontSize = 10;
            catLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            headerRow.Add(catLabel);

            card.Add(headerRow);

            if (!string.IsNullOrEmpty(diff.Finding.Description))
            {
                var desc = new Label(diff.Finding.Description);
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.marginTop = 3;
                desc.style.fontSize = 10;
                desc.style.color = new Color(0.65f, 0.65f, 0.65f);
                card.Add(desc);
            }

            _historyContainer.Add(card);
        }

        // -- Sparkline --

        private void OnDrawSparkline(MeshGenerationContext ctx)
        {
            if (_sparklineData == null || _sparklineData.Length < 2) return;

            var rect = _sparklineElement.contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;

            var painter = ctx.painter2D;
            float w = rect.width;
            float h = rect.height;
            float padding = 2f;

            // Compute range
            float maxVal = 0f;
            for (int i = 0; i < _sparklineData.Length; i++)
                if (_sparklineData[i] > maxVal) maxVal = _sparklineData[i];
            if (maxVal < 16.67f) maxVal = 16.67f; // min scale to 60fps line
            maxVal *= 1.1f; // 10% headroom

            // Draw 60fps threshold line (16.67ms)
            float thresholdY = h - padding - ((16.67f / maxVal) * (h - padding * 2));
            painter.strokeColor = new Color(0.3f, 0.6f, 0.3f, 0.5f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, thresholdY));
            painter.LineTo(new Vector2(w, thresholdY));
            painter.Stroke();

            // Draw 30fps threshold line (33.33ms) if visible
            if (33.33f < maxVal)
            {
                float threshold30Y = h - padding - ((33.33f / maxVal) * (h - padding * 2));
                painter.strokeColor = new Color(0.9f, 0.7f, 0.2f, 0.4f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, threshold30Y));
                painter.LineTo(new Vector2(w, threshold30Y));
                painter.Stroke();
            }

            // Downsample to fit width (max ~300 points)
            int maxPoints = Mathf.Min(_sparklineData.Length, (int)w);
            int step = Mathf.Max(1, _sparklineData.Length / maxPoints);

            // Draw frame time line
            painter.lineWidth = 1.5f;
            painter.BeginPath();

            bool first = true;
            for (int i = 0; i < _sparklineData.Length; i += step)
            {
                float x = (float)i / (_sparklineData.Length - 1) * w;
                float val = _sparklineData[i];
                float y = h - padding - ((val / maxVal) * (h - padding * 2));

                if (first)
                {
                    painter.MoveTo(new Vector2(x, y));
                    first = false;
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }

            painter.strokeColor = new Color(0.4f, 0.7f, 0.9f, 0.9f);
            painter.Stroke();
        }

        // -- Helpers --

        private static Color GradeColor(char grade) => grade switch
        {
            'A' => new Color(0.3f, 0.85f, 0.4f),
            'B' => new Color(0.5f, 0.8f, 0.3f),
            'C' => new Color(0.9f, 0.8f, 0.2f),
            'D' => new Color(0.9f, 0.5f, 0.2f),
            _ => new Color(0.9f, 0.25f, 0.25f)
        };

        /// <summary>
        /// Adds clickable script/asset reference links to a finding card.
        /// </summary>
        private static void AddSourceReferences(VisualElement card, DiagnosticFinding f)
        {
            if (string.IsNullOrEmpty(f.ScriptPath) && string.IsNullOrEmpty(f.AssetPath))
                return;

            var refRow = new VisualElement();
            refRow.style.flexDirection = FlexDirection.Row;
            refRow.style.flexWrap = Wrap.Wrap;
            refRow.style.marginTop = 4;

            if (!string.IsNullOrEmpty(f.ScriptPath))
            {
                string scriptDisplay = f.ScriptLine > 0 ? $"{f.ScriptPath}:{f.ScriptLine}" : f.ScriptPath;
                var scriptLink = new Label($"Script: {scriptDisplay}");
                scriptLink.style.color = new Color(0.4f, 0.7f, 1f);
                scriptLink.style.fontSize = 10;
                scriptLink.style.marginRight = 12;
                scriptLink.style.unityFontStyleAndWeight = FontStyle.Italic;
                scriptLink.style.cursor = StyleKeyword.Auto; // indicate clickability

                // Validate path exists before making clickable
                string path = f.ScriptPath;
                int line = f.ScriptLine;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                {
                    scriptLink.AddManipulator(new Clickable(() =>
                    {
                        AssetDatabase.OpenAsset(asset, line);
                    }));
                    scriptLink.tooltip = $"Click to open {path} at line {line}";
                }
                else
                {
                    scriptLink.style.color = new Color(0.5f, 0.5f, 0.5f);
                    scriptLink.tooltip = "File not found";
                }

                refRow.Add(scriptLink);
            }

            if (!string.IsNullOrEmpty(f.AssetPath))
            {
                var assetLink = new Label($"Asset: {f.AssetPath}");
                assetLink.style.color = new Color(0.4f, 0.7f, 1f);
                assetLink.style.fontSize = 10;
                assetLink.style.unityFontStyleAndWeight = FontStyle.Italic;

                string assetPath = f.AssetPath;
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset != null)
                {
                    assetLink.AddManipulator(new Clickable(() =>
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }));
                    assetLink.tooltip = $"Click to highlight {assetPath} in Project window";
                }
                else
                {
                    assetLink.style.color = new Color(0.5f, 0.5f, 0.5f);
                    assetLink.tooltip = "Asset not found";
                }

                refRow.Add(assetLink);
            }

            card.Add(refRow);
        }

        private static Color SeverityColor(Severity s) => s switch
        {
            Severity.Critical => new Color(0.9f, 0.25f, 0.25f),
            Severity.Warning => new Color(0.9f, 0.7f, 0.2f),
            _ => new Color(0.4f, 0.7f, 0.9f)
        };

        private static string SeverityIcon(Severity s) => s switch
        {
            Severity.Critical => "[!!]",
            Severity.Warning => "[! ]",
            _ => "[i ]"
        };
    }
}
#endif
