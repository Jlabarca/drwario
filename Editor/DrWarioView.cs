#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using DrWario.Editor.Analysis;
using DrWario.Editor.Analysis.LLM;
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

        private DiagnosticReport _lastReport;
        private readonly LLMConfig _llmConfig = new();

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
            CreateTabButton("History", () => { ShowSection(3); RefreshHistory(); }, tabContainer);
            CreateTabButton("Ask Doctor", () => ShowSection(4), tabContainer);
            CreateTabButton("LLM Settings", () => ShowSection(5), tabContainer);

            _sections = new VisualElement[6];
            _sections[0] = CreateSummarySection();
            _sections[1] = CreateFindingsSection();
            _sections[2] = CreateRecommendationsSection();
            _sections[3] = CreateHistorySection();
            _sections[4] = CreateAskSection();
            _sections[5] = CreateSettingsSection();

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

            UpdateApiKeyVisibility();
            return scroll;
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

            var engine = new AnalysisEngine(_llmConfig);

            if (_llmConfig.IsConfigured)
            {
                // Async path: rules run instantly, then AI runs without blocking
                _statusLabel.text = "Analyzing... (rule-based done, waiting for AI)";
                _lastReport = await engine.AnalyzeAsync(session);

                string aiStatus = engine.AICallSucceeded
                    ? " + AI insights"
                    : $" (AI: {engine.AIError ?? "no findings"})";

                _statusLabel.text = $"Analysis complete. Grade: {_lastReport.OverallGrade} ({_lastReport.HealthScore:F0}/100) | {_lastReport.Findings.Count} findings{aiStatus}.";
            }
            else
            {
                // Sync path: rules only, instant
                _lastReport = engine.Analyze(session);
                _statusLabel.text = $"Analysis complete. Grade: {_lastReport.OverallGrade} ({_lastReport.HealthScore:F0}/100) | {_lastReport.Findings.Count} findings.";
            }

            _statusLabel.style.color = new Color(0.5f, 0.8f, 1f);
            _analyzeBtn.SetEnabled(true);

            // Auto-save to history
            ReportHistory.Save(_lastReport);

            PopulateSummary();
            PopulateFindings();
            PopulateRecommendations();
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
        }

        private void PopulateFindings()
        {
            _findingsContainer.Clear();

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
                var card = new VisualElement();
                card.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
                card.style.marginBottom = 8;
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.paddingLeft = 10;
                card.style.paddingRight = 10;
                card.style.borderLeftWidth = 3;
                card.style.borderLeftColor = SeverityColor(f.Severity);

                // Header row
                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;

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

                _findingsContainer.Add(card);
            }
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

                var capturedPath = r.FilePath;
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
