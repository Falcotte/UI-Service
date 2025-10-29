using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Signals
{
    public class SignalInspectorEditorWindow : EditorWindow
    {
        private object _busInstance;
        private IDictionary _subscribers;

        private bool _autoRefresh = true;
        private double _nextRefreshTime;

        private Vector2 _publishASignalScrollView;
        private Vector2 _subscribersScrollView;

        private float? _pendingScrollTargetHeight;

        private string _publishASignalSearchString = string.Empty;
        private Type _selectedSignalType;

        private string _subscribersSearchString = string.Empty;

        private bool _showActiveList;
        private bool _showAllList;

        private readonly Dictionary<MemberInfo, object> _memberEdits = new();
        private ConstructorInfo _selectedConstructorInfo;

        private readonly Dictionary<ParameterInfo, object> _constructorEdits = new();

        private Type _pendingPingType;
        private double _pingFlashEndTime;

        private enum StructInitMode
        {
            Default,
            Constructor
        }

        private StructInitMode _structInitMode = StructInitMode.Default;

        private List<Type> _allSignalTypes = new();
        private List<Type> _activeSignalTypes = new();

        private const double AutoRefreshInterval = 0.5;

        private const string ShowActiveEditorPrefKey = "SignalBusViewer.ShowActive";
        private const string ShowAllEditorPrefKey = "SignalBusViewer.ShowAll";
        private const string GroupActivePrefixEditorPrefKey = "SignalBusViewer.Group.Active.";
        private const string GroupAllPrefixEditorPrefKey = "SignalBusViewer.Group.All.";

        private const double PingFlashDuration = 0.5;
        private const float PingFlashAmount = 0.3f;

        private static GUIStyle _headerStyle;
        private static GUIStyle _headerLabelStyle;
        private static GUIStyle _backgroundStyle;

        private void OnEnable()
        {
            LoadEditorPrefs();
            RefreshReflection();
            RebuildSignalTypeCaches();

            _nextRefreshTime = EditorApplication.timeSinceStartup + AutoRefreshInterval;
        }

        private void OnDisable()
        {
            SaveEditorPrefs();
        }

        [MenuItem("Angry Koala/Signals/Signal Bus Inspector")]
        private static void Open()
        {
            SignalInspectorEditorWindow window = GetWindow<SignalInspectorEditorWindow>("Signal Bus Inspector");

            window.minSize = new Vector2(1000f, 600f);
            window.Show();
        }

        private void LoadEditorPrefs()
        {
            _showActiveList = EditorPrefs.GetBool(ShowActiveEditorPrefKey, true);
            _showAllList = EditorPrefs.GetBool(ShowAllEditorPrefKey, true);
        }

        private void RefreshReflection()
        {
            try
            {
                Type busType = typeof(SignalBus);
                FieldInfo instanceField = busType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static);
                _busInstance = instanceField != null ? instanceField.GetValue(null) : null;

                FieldInfo subscribersField =
                    busType.GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
                _subscribers = subscribersField != null ? subscribersField.GetValue(_busInstance) as IDictionary : null;

                _activeSignalTypes = GetActiveSignalTypes();
            }
            catch
            {
                _busInstance = null;
                _subscribers = null;
                _activeSignalTypes = new List<Type>();
            }
        }

        private List<Type> GetActiveSignalTypes()
        {
            List<Type> list = new List<Type>();

            if (_subscribers == null)
            {
                return list;
            }

            foreach (DictionaryEntry dictionaryEntry in _subscribers)
            {
                if (dictionaryEntry.Key is Type type)
                {
                    list.Add(type);
                }
            }

            list.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
            return list;
        }

        private void RebuildSignalTypeCaches(bool activeOnly = false)
        {
            if (!activeOnly)
            {
                Type signalType = typeof(ISignal);

                _allSignalTypes = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(assembly =>
                    {
                        try
                        {
                            return assembly.GetTypes();
                        }
                        catch
                        {
                            return Array.Empty<Type>();
                        }
                    })
                    .Where(type => type != null && signalType.IsAssignableFrom(type) && !type.IsAbstract)
                    .Distinct()
                    .OrderBy(type => type.FullName)
                    .ToList();
            }

            _activeSignalTypes = GetActiveSignalTypes();

            if (_selectedSignalType != null &&
                (_allSignalTypes.Contains(_selectedSignalType) || _activeSignalTypes.Contains(_selectedSignalType)))
            {
                return;
            }

            _selectedSignalType = _activeSignalTypes.FirstOrDefault() ?? _allSignalTypes.FirstOrDefault();
            PrepareEditorsForSelectedType();
        }

        private void PrepareEditorsForSelectedType()
        {
            _memberEdits.Clear();
            _constructorEdits.Clear();
            _selectedConstructorInfo = null;

            if (_selectedSignalType == null)
            {
                return;
            }

            bool isValueType = _selectedSignalType.IsValueType;

            if (isValueType)
            {
                ConstructorInfo[] structConstructors =
                    _selectedSignalType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                bool hasParameterizedConstructors = structConstructors.Any(c => c.GetParameters().Length > 0);

                if (_structInitMode == StructInitMode.Constructor && hasParameterizedConstructors)
                {
                    _selectedConstructorInfo = structConstructors
                        .OrderByDescending(constructorInfo => constructorInfo.GetParameters().Length)
                        .First();

                    foreach (ParameterInfo parameterInfo in _selectedConstructorInfo.GetParameters())
                    {
                        _constructorEdits[parameterInfo] = GetDefault(parameterInfo.ParameterType);
                    }
                }
                else
                {
                    object tempInstance = Activator.CreateInstance(_selectedSignalType);
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

                    foreach (FieldInfo fieldInfo in _selectedSignalType.GetFields(flags))
                    {
                        if (!fieldInfo.IsInitOnly)
                        {
                            _memberEdits[fieldInfo] = fieldInfo.GetValue(tempInstance);
                        }
                    }

                    foreach (PropertyInfo propertyInfo in _selectedSignalType.GetProperties(flags))
                    {
                        if (propertyInfo.CanRead && propertyInfo.CanWrite &&
                            propertyInfo.GetIndexParameters().Length == 0)
                        {
                            _memberEdits[propertyInfo] = SafeGetProperty(propertyInfo, tempInstance);
                        }
                    }
                }

                return;
            }

            ConstructorInfo defaultConstructor = _selectedSignalType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);

            if (defaultConstructor != null)
            {
                object tempInstance = null;
                try
                {
                    tempInstance = Activator.CreateInstance(_selectedSignalType, true);
                }
                catch
                {
                }

                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

                foreach (FieldInfo fieldInfo in _selectedSignalType.GetFields(flags))
                {
                    if (!fieldInfo.IsInitOnly)
                    {
                        object value = tempInstance != null
                            ? fieldInfo.GetValue(tempInstance)
                            : GetDefault(fieldInfo.FieldType);
                        _memberEdits[fieldInfo] = value;
                    }
                }

                foreach (PropertyInfo propertyInfo in _selectedSignalType.GetProperties(flags))
                {
                    if (propertyInfo.CanRead && propertyInfo.CanWrite && propertyInfo.GetIndexParameters().Length == 0)
                    {
                        object value = tempInstance != null
                            ? SafeGetProperty(propertyInfo, tempInstance)
                            : GetDefault(propertyInfo.PropertyType);
                        _memberEdits[propertyInfo] = value;
                    }
                }

                return;
            }

            _selectedConstructorInfo = _selectedSignalType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (_selectedConstructorInfo != null)
            {
                foreach (ParameterInfo parameterInfo in _selectedConstructorInfo.GetParameters())
                {
                    _constructorEdits[parameterInfo] = GetDefault(parameterInfo.ParameterType);
                }
            }
        }

        private void SaveEditorPrefs()
        {
            EditorPrefs.SetBool(ShowActiveEditorPrefKey, _showActiveList);
            EditorPrefs.SetBool(ShowAllEditorPrefKey, _showAllList);
        }

        private void Update()
        {
            if (!_autoRefresh || !(EditorApplication.timeSinceStartup >= _nextRefreshTime))
            {
                return;
            }

            RefreshReflection();
            RebuildSignalTypeCaches(activeOnly: true);
            Repaint();

            _nextRefreshTime = EditorApplication.timeSinceStartup + AutoRefreshInterval;
        }

        private void OnGUI()
        {
            SetGUIStyles();
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.44f)))
                {
                    DrawPublishPanelHeader();

                    GUILayout.Space(4f);

                    _publishASignalScrollView =
                        EditorGUILayout.BeginScrollView(_publishASignalScrollView, GUILayout.ExpandHeight(true));

                    DrawPublishPanelBodyScroll();

                    EditorGUILayout.EndScrollView();

                    GUILayout.Space(4f);

                    DrawPublishControls();
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    DrawSubscribersPanelHeader();

                    if (_pendingScrollTargetHeight.HasValue && Event.current.type == EventType.Layout)
                    {
                        _subscribersScrollView.y = _pendingScrollTargetHeight.Value;
                        _pendingScrollTargetHeight = null;
                        Repaint();
                    }

                    _subscribersScrollView =
                        EditorGUILayout.BeginScrollView(_subscribersScrollView, GUILayout.ExpandHeight(true));

                    DrawSubscribersPanelBodyScroll();

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void SetGUIStyles()
        {
            if (_headerStyle != null && _headerLabelStyle != null && _backgroundStyle != null)
            {
                return;
            }

            _headerStyle = GetGUIStyle("RL Header", new GUIStyle(EditorStyles.toolbar));
            _headerStyle = new GUIStyle(_headerStyle)
            {
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(2, 2, 6, 0),
                stretchHeight = false
            };

            _headerLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Bold
            };

            _backgroundStyle = GetGUIStyle("RL Background", new GUIStyle(EditorStyles.helpBox));
            _backgroundStyle = new GUIStyle(_backgroundStyle)
            {
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(2, 2, 0, 2),
                stretchHeight = false
            };
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshReflection();
                RebuildSignalTypeCaches();
            }

            GUILayout.FlexibleSpace();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto-refresh", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPublishPanelHeader()
        {
            DrawPinnedHeader("Publish a Signal", ref _publishASignalSearchString);
        }

        private void DrawPinnedHeader(string title, ref string searchText)
        {
            float titleHeight = EditorGUIUtility.singleLineHeight;
            Rect titleRect = GUILayoutUtility.GetRect(1, titleHeight, GUILayout.ExpandWidth(true));

            Rect paddedRect = new Rect(titleRect.x + 3, titleRect.y, titleRect.width - 3, titleRect.height);
            EditorGUI.LabelField(paddedRect, title, EditorStyles.boldLabel);

            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            using (new EditorGUILayout.HorizontalScope())
            {
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 54f;

                searchText = EditorGUILayout.TextField(
                    "Search",
                    searchText,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                GUILayout.Space(10);

                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        private void DrawPublishPanelBodyScroll()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSectionHeaderWithExpandCollapseButtons(
                        $"Active Signal Types ({_activeSignalTypes.Count})",
                        ref _showActiveList,
                        onExpandAll: () => SetAllGroupsExpanded(true, true),
                        onCollapseAll: () => SetAllGroupsExpanded(true, false));

                    if (_showActiveList)
                    {
                        EditorGUILayout.Space(2);
                        DrawGroupedSignalTypePickList(_activeSignalTypes, isActiveList: true);
                    }
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSectionHeaderWithExpandCollapseButtons(
                        $"All Signal Types ({_allSignalTypes.Count})",
                        ref _showAllList,
                        onExpandAll: () => SetAllGroupsExpanded(false, true),
                        onCollapseAll: () => SetAllGroupsExpanded(false, false));

                    if (_showAllList)
                    {
                        EditorGUILayout.Space(2);
                        DrawGroupedSignalTypePickList(_allSignalTypes, isActiveList: false);
                    }
                }
            }
        }

        private static void DrawSectionHeaderWithExpandCollapseButtons(
            string title, ref bool show, Action onExpandAll, Action onCollapseAll)
        {
            const float headerHeight = 22f;
            const float expandAllButtonWidth = 84f;
            const float collapseAllButtonWidth = 90f;
            const float paddingLeft = 4f;
            const float paddingRight = 4f;
            const float spacing = 6f;

            Rect headerRect = EditorGUILayout.GetControlRect(false, headerHeight);
            headerRect.y = Mathf.Round(headerRect.y);

            GUI.Box(headerRect, GUIContent.none, EditorStyles.helpBox);

            float foldoutSize = EditorGUIUtility.singleLineHeight;

            Rect foldoutRect = new Rect(
                headerRect.x + paddingLeft,
                headerRect.y + ((headerHeight - foldoutSize) * 0.5f),
                foldoutSize,
                foldoutSize);

            Rect labelRect = new Rect(
                foldoutRect.xMax + 4f,
                headerRect.y + ((headerHeight - EditorGUIUtility.singleLineHeight) * 0.5f),
                headerRect.width - (foldoutRect.width + expandAllButtonWidth + collapseAllButtonWidth + paddingLeft +
                                    paddingRight + (spacing * 2f) + 12f),
                EditorGUIUtility.singleLineHeight);

            float buttonsY = headerRect.y + ((headerHeight - EditorGUIUtility.singleLineHeight) * 0.5f);

            Rect collapseAllButton = new Rect(
                headerRect.xMax - paddingRight - collapseAllButtonWidth,
                buttonsY,
                collapseAllButtonWidth,
                EditorGUIUtility.singleLineHeight);

            Rect expandAllButton = new Rect(
                collapseAllButton.x - spacing - expandAllButtonWidth,
                buttonsY,
                expandAllButtonWidth,
                EditorGUIUtility.singleLineHeight);

            bool newShow = EditorGUI.Foldout(foldoutRect, show, GUIContent.none, true);
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

            if (GUI.Button(expandAllButton, "Expand all", EditorStyles.miniButton))
            {
                onExpandAll?.Invoke();
            }

            if (GUI.Button(collapseAllButton, "Collapse all", EditorStyles.miniButton))
            {
                onCollapseAll?.Invoke();
            }

            if (Event.current.type == EventType.MouseDown &&
                new Rect(headerRect.x, headerRect.y, expandAllButton.x - headerRect.x, headerRect.height)
                    .Contains(Event.current.mousePosition) &&
                Event.current.button == 0)
            {
                newShow = !show;
                Event.current.Use();
            }

            if (newShow != show)
            {
                show = newShow;
            }
        }

        private void SetAllGroupsExpanded(bool isActiveList, bool expand)
        {
            foreach (string key in GetCurrentGroupKeys(isActiveList))
            {
                SetGroupFoldoutState(isActiveList, key, expand);
            }
        }

        private IEnumerable<string> GetCurrentGroupKeys(bool isActiveList)
        {
            List<Type> signalTypes = isActiveList ? _activeSignalTypes : _allSignalTypes;
            return signalTypes?
                       .Select(GetGroupKeyForType)
                       .Distinct(StringComparer.Ordinal)
                       .OrderBy(signalTypeName => signalTypeName, StringComparer.Ordinal)
                   ?? Enumerable.Empty<string>();
        }

        private void SetGroupFoldoutState(bool isActiveList, string groupKey, bool expanded)
        {
            string prefKey = (isActiveList ? GroupActivePrefixEditorPrefKey : GroupAllPrefixEditorPrefKey) +
                             SanitizeEditorPrefGroupKey(groupKey);

            EditorPrefs.SetBool(prefKey, expanded);
        }

        private bool GetGroupFoldoutState(bool isActiveList, string groupKey)
        {
            string prefKey = (isActiveList ? GroupActivePrefixEditorPrefKey : GroupAllPrefixEditorPrefKey) +
                             SanitizeEditorPrefGroupKey(groupKey);

            return EditorPrefs.GetBool(prefKey, true);
        }

        private static string GetGroupKeyForType(Type type)
        {
            if (type.DeclaringType != null && !string.IsNullOrEmpty(type.DeclaringType.FullName))
            {
                return type.DeclaringType.FullName;
            }

            if (!string.IsNullOrEmpty(type.Namespace))
            {
                return type.Namespace;
            }

            return "(global)";
        }

        private void DrawGroupedSignalTypePickList(List<Type> types, bool isActiveList)
        {
            if (types == null || types.Count == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                return;
            }

            IEnumerable<Type> filtered = types;

            if (!string.IsNullOrEmpty(_publishASignalSearchString))
            {
                filtered = filtered.Where(type =>
                    (type.FullName != null
                        ? type.FullName.IndexOf(_publishASignalSearchString, StringComparison.OrdinalIgnoreCase)
                        : -1) >= 0);
            }

            string GroupKey(Type type)
            {
                if (type.DeclaringType != null && !string.IsNullOrEmpty(type.DeclaringType.FullName))
                {
                    return type.DeclaringType.FullName;
                }

                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    return type.Namespace;
                }

                return "(global)";
            }

            IOrderedEnumerable<IGrouping<string, Type>> grouped = filtered
                .GroupBy(GroupKey)
                .OrderBy(grouping => grouping.Key, StringComparer.Ordinal);

            foreach (IGrouping<string, Type> group in grouped)
            {
                string groupKey = group.Key;
                bool expanded = GetGroupFoldoutState(isActiveList, groupKey);
                int count = group.Count();

                float headerHeight = Mathf.Max((_headerStyle.fixedHeight > 0 ? _headerStyle.fixedHeight : 20f),
                    EditorGUIUtility.singleLineHeight + 6f);
                Rect headerRect = GUILayoutUtility.GetRect(1, headerHeight, GUILayout.ExpandWidth(true));

                if (Event.current.type == EventType.Repaint)
                {
                    _headerStyle.Draw(headerRect, GUIContent.none, false, false, false, false);
                }

                float lift = -1.0f;

                float foldoutWidth = 16f;
                Rect foldoutRect = new Rect(
                    headerRect.x + 6f,
                    headerRect.y + ((headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f) + lift,
                    foldoutWidth,
                    EditorGUIUtility.singleLineHeight);

                bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);

                Rect labelRect = new Rect(
                    foldoutRect.xMax + 4f,
                    headerRect.y + ((headerRect.height - EditorGUIUtility.singleLineHeight) * 0.5f) + lift - 1f,
                    headerRect.width - (foldoutRect.width + 12f),
                    EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(labelRect, $"{groupKey.Replace('+', '.')} ({count})", _headerLabelStyle);

                if (Event.current.type == EventType.MouseDown &&
                    headerRect.Contains(Event.current.mousePosition) &&
                    Event.current.button == 0)
                {
                    newExpanded = !expanded;
                    Event.current.Use();
                }

                if (newExpanded != expanded)
                {
                    expanded = newExpanded;
                    SetGroupFoldoutState(isActiveList, groupKey, expanded);
                }

                GUILayout.Space(-(EditorGUIUtility.standardVerticalSpacing + 2));

                if (expanded)
                {
                    using (new GUILayout.VerticalScope(_backgroundStyle, GUILayout.ExpandWidth(true)))
                    {
                        EditorGUI.indentLevel++;

                        foreach (Type selectedSignalType in
                                 group.OrderBy(type => type.FullName, StringComparer.Ordinal))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                bool wasSelected = (selectedSignalType == _selectedSignalType);

                                string shortName = selectedSignalType.Name;
                                int plus = shortName.LastIndexOf('+');
                                if (plus >= 0)
                                {
                                    shortName = shortName.Substring(plus + 1);
                                }

                                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
                                {
                                    wordWrap = false,
                                    clipping = TextClipping.Clip,
                                    alignment = TextAnchor.MiddleLeft
                                };

                                GUIContent content = new GUIContent(
                                    shortName,
                                    selectedSignalType.FullName != null
                                        ? selectedSignalType.FullName.Replace('+', '.')
                                        : shortName);

                                Rect rowRect = GUILayoutUtility.GetRect(
                                    content, buttonStyle, GUILayout.ExpandWidth(true),
                                    GUILayout.Height(EditorGUIUtility.singleLineHeight));

                                bool mouseUpInRow =
                                    Event.current.type == EventType.MouseUp &&
                                    Event.current.button == 0 &&
                                    rowRect.Contains(Event.current.mousePosition);

                                if (mouseUpInRow)
                                {
                                    if (!wasSelected)
                                    {
                                        _selectedSignalType = selectedSignalType;
                                        PrepareEditorsForSelectedType();
                                        wasSelected = true;
                                    }

                                    _pendingPingType = selectedSignalType;
                                    _pingFlashEndTime = EditorApplication.timeSinceStartup + PingFlashDuration;
                                    _pendingScrollTargetHeight = null;

                                    Repaint();
                                }

                                if (Event.current.type == EventType.Repaint)
                                {
                                    bool isHover = rowRect.Contains(Event.current.mousePosition);
                                    bool isActive = GUIUtility.hotControl != 0 && isHover;
                                    bool on = wasSelected;
                                    bool focus = false;

                                    buttonStyle.Draw(rowRect, content, isHover, isActive, on, focus);
                                }

                                GUILayout.FlexibleSpace();
                            }
                        }

                        EditorGUI.indentLevel--;
                    }

                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                }
                else
                {
                    const float capHeight = 5f;
                    const float overlap = 0f;
                    const float inset = 0f;

                    Rect capRect = GUILayoutUtility.GetRect(1, capHeight, GUILayout.ExpandWidth(true));
                    capRect = new Rect(capRect.x + inset, capRect.y - overlap, capRect.width - (inset * 2f),
                        capRect.height + overlap);

                    if (Event.current.type == EventType.Repaint)
                    {
                        _backgroundStyle.Draw(capRect, GUIContent.none, false, false, false, false);
                    }

                    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing + 2);
                }
            }
        }

        private void DrawPublishControls()
        {
            if (_selectedSignalType == null)
            {
                EditorGUILayout.HelpBox("Select a signal type to edit parameters and publish.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                _selectedSignalType.FullName != null ? _selectedSignalType.FullName.Replace('+', '.') : string.Empty,
                EditorStyles.helpBox);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool isValueType = _selectedSignalType.IsValueType;

                ConstructorInfo defaultConstructorForClass = !isValueType
                    ? _selectedSignalType.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null)
                    : null;

                if (isValueType)
                {
                    ConstructorInfo[] structConstructors =
                        _selectedSignalType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                    bool hasParameterizedConstructors = structConstructors.Any(c => c.GetParameters().Length > 0);

                    if (hasParameterizedConstructors)
                    {
                        EditorGUILayout.LabelField("Initialization", EditorStyles.miniBoldLabel);

                        StructInitMode newMode = (StructInitMode)GUILayout.Toolbar((int)_structInitMode,
                            new[] { "Default", "Constructor" });
                        if (newMode != _structInitMode)
                        {
                            _structInitMode = newMode;
                            PrepareEditorsForSelectedType();
                        }

                        EditorGUILayout.Space(4);
                    }
                    else
                    {
                        _structInitMode = StructInitMode.Default;
                    }

                    if (_structInitMode == StructInitMode.Constructor && hasParameterizedConstructors)
                    {
                        EditorGUILayout.LabelField("Constructor Parameters", EditorStyles.miniBoldLabel);
                        DrawConstructorEditors();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Public Fields / Properties (struct defaults to zero-initialized)",
                            EditorStyles.miniBoldLabel);
                        DrawMemberEditors();
                    }
                }
                else
                {
                    if (defaultConstructorForClass != null)
                    {
                        EditorGUILayout.LabelField("Public Fields / Properties", EditorStyles.miniBoldLabel);
                        DrawMemberEditors();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Constructor Parameters", EditorStyles.miniBoldLabel);
                        DrawConstructorEditors();
                    }
                }
            }

            EditorGUILayout.Space(2);

            using (new EditorGUI.DisabledScope(_selectedSignalType == null))
            {
                if (GUILayout.Button("Publish", GUILayout.Height(36f)))
                {
                    TryPublishSelectedSignalInstance();
                }
            }

            EditorGUILayout.Space(2);
        }

        private void DrawConstructorEditors()
        {
            ConstructorInfo[] constructors =
                _selectedSignalType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (constructors.Length == 0)
            {
                EditorGUILayout.HelpBox("Type has no public constructors and no default constructor.",
                    MessageType.Error);
                return;
            }

            string[] labels = constructors.Select(GetConstructorSignature).ToArray();
            int currentIndex = Array.IndexOf(constructors, _selectedConstructorInfo);

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int newIndex = EditorGUILayout.Popup("Constructor", currentIndex, labels);
            if (newIndex != currentIndex)
            {
                _selectedConstructorInfo = constructors[newIndex];
                _constructorEdits.Clear();

                foreach (ParameterInfo parameterInfo in _selectedConstructorInfo.GetParameters())
                {
                    _constructorEdits[parameterInfo] = GetDefault(parameterInfo.ParameterType);
                }
            }

            EditorGUILayout.Space(4);

            if (_selectedConstructorInfo == null)
            {
                _selectedConstructorInfo = constructors[newIndex];
            }

            foreach (ParameterInfo parameterInfo in _selectedConstructorInfo.GetParameters())
            {
                object current = _constructorEdits.TryGetValue(parameterInfo, out object value)
                    ? value
                    : GetDefault(parameterInfo.ParameterType);

                if (TryDrawEditorValueField(parameterInfo.Name, parameterInfo.ParameterType, current, out object next))
                {
                    _constructorEdits[parameterInfo] = next;
                }
            }
        }

        private void DrawMemberEditors()
        {
            foreach (KeyValuePair<MemberInfo, object> keyValuePair in _memberEdits.ToList())
            {
                MemberInfo member = keyValuePair.Key;
                object value = keyValuePair.Value;

                string label = member.Name;
                Type valueType =
                    member is FieldInfo fieldInfo ? fieldInfo.FieldType :
                    member is PropertyInfo propertyInfo ? propertyInfo.PropertyType : null;

                if (valueType == null)
                {
                    return;
                }

                if (TryDrawEditorValueField(label, valueType, value, out object newValue))
                {
                    _memberEdits[member] = newValue;
                }
            }
        }

        private bool TryDrawEditorValueField(string label, Type type, object currentValue, out object newValue)
        {
            newValue = currentValue;

            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                type = underlying;

                if (currentValue == null)
                {
                    currentValue = GetDefault(type);
                }
            }

            string nicifiedLabel = ObjectNames.NicifyVariableName(label);

            EditorGUI.BeginChangeCheck();

            if (type == typeof(Vector2))
            {
                Vector2 value = currentValue is Vector2 v ? v : default;
                newValue = EditorGUILayout.Vector2Field(nicifiedLabel, value);

                return EditorGUI.EndChangeCheck();
            }

            if (type == typeof(Vector3))
            {
                Vector3 value = currentValue is Vector3 v ? v : default;
                newValue = EditorGUILayout.Vector3Field(nicifiedLabel, value);

                return EditorGUI.EndChangeCheck();
            }

            if (type == typeof(Vector4))
            {
                Vector4 value = currentValue is Vector4 v ? v : default;
                newValue = EditorGUILayout.Vector4Field(nicifiedLabel, value);

                return EditorGUI.EndChangeCheck();
            }

            if (type == typeof(Color))
            {
                Color value = currentValue is Color c ? c : default;
                newValue = EditorGUILayout.ColorField(nicifiedLabel, value);

                return EditorGUI.EndChangeCheck();
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                UnityEngine.Object value = currentValue as UnityEngine.Object;
                newValue = EditorGUILayout.ObjectField(nicifiedLabel, value, type, true);

                return EditorGUI.EndChangeCheck();
            }

            if (type.IsEnum)
            {
                object enumValue = currentValue ?? Enum.GetValues(type).GetValue(0);
                Enum next = EditorGUILayout.EnumPopup(nicifiedLabel, (Enum)enumValue);
                newValue = Enum.ToObject(type, Convert.ChangeType(next, Enum.GetUnderlyingType(type)));

                return EditorGUI.EndChangeCheck();
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                {
                    bool value = currentValue is bool b ? b : default;
                    newValue = EditorGUILayout.Toggle(nicifiedLabel, value);
                    break;
                }
                case TypeCode.Int32:
                {
                    int value = currentValue is int i ? i : default;
                    newValue = EditorGUILayout.IntField(nicifiedLabel, value);
                    break;
                }
                case TypeCode.Single:
                {
                    float value = currentValue is float f ? f : default;
                    newValue = EditorGUILayout.FloatField(nicifiedLabel, value);
                    break;
                }
                case TypeCode.Double:
                {
                    double value = currentValue is double d ? d : default;
                    newValue = EditorGUILayout.DoubleField(nicifiedLabel, value);
                    break;
                }
                case TypeCode.Int64:
                {
                    long value = currentValue is long l ? l : default;
                    newValue = EditorGUILayout.LongField(nicifiedLabel, value);
                    break;
                }
                case TypeCode.String:
                {
                    string value = currentValue as string ?? string.Empty;
                    newValue = EditorGUILayout.TextField(nicifiedLabel, value);
                    break;
                }
                default:
                {
                    string fallbackText = currentValue != null ? currentValue.ToString() : string.Empty;
                    string editedText = EditorGUILayout.TextField($"{nicifiedLabel} ({type.Name})", fallbackText);

                    if (type == typeof(Guid))
                    {
                        newValue = Guid.TryParse(editedText, out Guid parsedGuid) ? parsedGuid : currentValue;
                        break;
                    }

                    if (type == typeof(TimeSpan))
                    {
                        newValue = TimeSpan.TryParse(editedText, out TimeSpan parsedTimeSpan)
                            ? parsedTimeSpan
                            : currentValue;
                        break;
                    }
                    
                    try
                    {
                        newValue = ConvertFromString(type, editedText, currentValue);
                    }
                    catch
                    {
                        newValue = currentValue;
                    }

                    break;
                }
            }
            
            return EditorGUI.EndChangeCheck();
        }

        private void TryPublishSelectedSignalInstance()
        {
            if (_selectedSignalType == null)
            {
                Debug.LogWarning("No signal type selected.");
                return;
            }

            object instance;
            bool isValueType = _selectedSignalType.IsValueType;

            try
            {
                if (isValueType)
                {
                    if (_structInitMode == StructInitMode.Constructor)
                    {
                        if (_selectedConstructorInfo == null)
                        {
                            Debug.LogError($"No constructor selected for struct {_selectedSignalType.FullName}.");
                            return;
                        }

                        object[] parameters = _selectedConstructorInfo.GetParameters()
                            .Select(parameterInfo => _constructorEdits.TryGetValue(parameterInfo, out object value)
                                ? value
                                : GetDefault(parameterInfo.ParameterType))
                            .ToArray();

                        instance = _selectedConstructorInfo.Invoke(parameters);
                    }
                    else
                    {
                        instance = Activator.CreateInstance(_selectedSignalType);
                        ApplyMemberEdits(ref instance);
                    }
                }
                else
                {
                    ConstructorInfo defaultConstructor = _selectedSignalType.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);

                    if (defaultConstructor != null)
                    {
                        instance = Activator.CreateInstance(_selectedSignalType, true);
                        ApplyMemberEdits(ref instance);
                    }
                    else if (_selectedConstructorInfo != null)
                    {
                        object[] parameters = _selectedConstructorInfo.GetParameters()
                            .Select(parameterInfo => _constructorEdits.TryGetValue(parameterInfo, out object value)
                                ? value
                                : GetDefault(parameterInfo.ParameterType))
                            .ToArray();

                        instance = _selectedConstructorInfo.Invoke(parameters);
                    }
                    else
                    {
                        Debug.LogError(
                            $"Cannot construct {_selectedSignalType.FullName}: no usable constructor found.");
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to construct {_selectedSignalType.FullName}: {exception.Message}");
                return;
            }

            try
            {
                MethodInfo publishGeneric = typeof(SignalBus).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(methodInfo =>
                        methodInfo.Name == "Publish" &&
                        methodInfo.IsGenericMethodDefinition &&
                        methodInfo.GetParameters().Length == 1);

                if (publishGeneric == null)
                {
                    Debug.LogError("Could not find SignalBus.Publish<T>(T) method.");
                    return;
                }

                MethodInfo publishClosed = publishGeneric.MakeGenericMethod(_selectedSignalType);
                publishClosed.Invoke(null, new[] { instance });

                Debug.Log($"Published signal: {_selectedSignalType.FullName}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to publish {_selectedSignalType.FullName}: {exception.Message}");
            }
        }

        private void ApplyMemberEdits(ref object boxedInstance)
        {
            if (boxedInstance == null)
            {
                return;
            }

            foreach (KeyValuePair<MemberInfo, object> keyValuePair in _memberEdits)
            {
                MemberInfo member = keyValuePair.Key;
                object value = keyValuePair.Value;

                if (member is FieldInfo fieldInfo)
                {
                    fieldInfo.SetValue(boxedInstance, value);
                }
                else if (member is PropertyInfo propertyInfo && propertyInfo.CanWrite)
                {
                    try
                    {
                        propertyInfo.SetValue(boxedInstance, value);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void DrawSubscribersPanelHeader()
        {
            DrawPinnedHeader("Subscribers", ref _subscribersSearchString);
        }

        private void DrawSubscribersPanelBodyScroll()
        {
            if (_subscribers == null)
            {
                EditorGUILayout.HelpBox("Couldn’t access SignalBus._subscribers (domain reload or reflection failed).",
                    MessageType.Warning);
                return;
            }

            if (_subscribers.Count == 0)
            {
                EditorGUILayout.HelpBox("No active subscribers.", MessageType.Info);
                return;
            }

            foreach (DictionaryEntry dictionaryEntry in _subscribers)
            {
                Type signalType = dictionaryEntry.Key as Type;
                object collection = dictionaryEntry.Value;

                bool isPingTarget = (_pendingPingType != null && signalType == _pendingPingType);

                string fullName = signalType?.FullName?.Replace('+', '.') ?? "(null)";
                string shortName = signalType?.Name ?? "(null)";

                List<(string targetLabel, string methodLabel, UnityEngine.Object pingObj)> rows = new();

                try
                {
                    FieldInfo callbacksField = collection.GetType()
                        .GetField("_callbacks", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (callbacksField?.GetValue(collection) is IEnumerable callbacksEnumerable)
                    {
                        foreach (object item in callbacksEnumerable)
                        {
                            Delegate del = item as Delegate;
                            if (del == null)
                            {
                                rows.Add(("<unknown>", "<unknown>", null));
                                continue;
                            }

                            MethodInfo method = del.Method;
                            object target = del.Target;

                            string targetLabel;
                            UnityEngine.Object pingObject = null;

                            if (target is UnityEngine.Object unityObject)
                            {
                                pingObject = unityObject;
                                targetLabel = $"{unityObject.name} ({unityObject.GetType().Name})";
                            }
                            else if (target != null)
                            {
                                targetLabel = target.GetType().FullName?.Replace('+', '.') ?? "null";
                            }
                            else
                            {
                                targetLabel = method?.DeclaringType?.FullName?.Replace('+', '.') ?? "static";
                            }

                            string methodLabel = method != null ? method.Name : "<no method>";
                            rows.Add((targetLabel, methodLabel, pingObject));
                        }
                    }
                }
                catch
                {
                }

                bool showThisSignal =
                    ContainsSubstringIgnoreCase(fullName, _subscribersSearchString) ||
                    ContainsSubstringIgnoreCase(shortName, _subscribersSearchString) ||
                    rows.Any(row =>
                        ContainsSubstringIgnoreCase(row.targetLabel, _subscribersSearchString) ||
                        ContainsSubstringIgnoreCase(row.methodLabel, _subscribersSearchString));

                if (!showThisSignal)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(fullName, EditorStyles.boldLabel);

                    if (isPingTarget && Event.current.type == EventType.Repaint)
                    {
                        Rect headerRect = GUILayoutUtility.GetLastRect();
                        _pendingScrollTargetHeight = Mathf.Max(headerRect.y - 6f, 0f);
                        _pendingPingType = null;
                        Repaint();
                    }

                    double now = EditorApplication.timeSinceStartup;
                    bool shouldFlash = (_selectedSignalType == signalType) && (now <= _pingFlashEndTime);

                    if (shouldFlash)
                    {
                        Rect headerRect = GUILayoutUtility.GetLastRect();

                        float life01 = Mathf.Clamp01((float)((_pingFlashEndTime - now) / PingFlashDuration));
                        float phase01 = 1f - life01;

                        float alpha = PingFlashAmount * Mathf.Sin(phase01 * Mathf.PI);

                        if (alpha > 0f)
                        {
                            Color pingColor = GetPingHighlightColor(alpha);
                            Rect rect = new Rect(headerRect.x - 2, headerRect.y - 1, headerRect.width + 4,
                                headerRect.height + 2);
                            EditorGUI.DrawRect(rect, pingColor);
                            Repaint();
                        }
                    }

                    int count = 0;

                    try
                    {
                        PropertyInfo propertyInfo = collection.GetType()
                            .GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                        if (propertyInfo != null)
                        {
                            count = (int)propertyInfo.GetValue(collection);
                        }
                    }
                    catch
                    {
                    }

                    EditorGUILayout.LabelField("Subscribers", count.ToString());

                    if (rows.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No readable callbacks (empty or reflection failed).",
                            MessageType.None);
                    }
                    else
                    {
                        foreach ((string targetLabel, string methodLabel, UnityEngine.Object pingObject) in rows)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField($"• {targetLabel}.{methodLabel}");
                                if (pingObject != null)
                                {
                                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                    {
                                        EditorGUIUtility.PingObject(pingObject);
                                    }
                                }
                            }
                        }
                    }
                }

                GUILayout.Space(4);
            }
        }

        #region Utility

        private static object GetDefault(Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }

        private static object SafeGetProperty(PropertyInfo propertyInfo, object instance)
        {
            try
            {
                return propertyInfo.GetValue(instance);
            }
            catch
            {
                return GetDefault(propertyInfo.PropertyType);
            }
        }

        private static GUIStyle GetGUIStyle(string styleName, GUIStyle fallback)
        {
            GUIStyle guiStyle = GUI.skin != null ? GUI.skin.FindStyle(styleName) : null;
            if (guiStyle != null)
            {
                return guiStyle;
            }

            guiStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)?.FindStyle(styleName);
            if (guiStyle != null)
            {
                return guiStyle;
            }

            guiStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene)?.FindStyle(styleName);
            if (guiStyle != null)
            {
                return guiStyle;
            }

            guiStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Game)?.FindStyle(styleName);
            if (guiStyle != null)
            {
                return guiStyle;
            }

            return fallback;
        }

        private static string SanitizeEditorPrefGroupKey(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "(global)";
            }

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }

            return raw.Replace(' ', '_');
        }

        private static string GetConstructorSignature(ConstructorInfo constructorInfo)
        {
            ParameterInfo[] parameterInfos = constructorInfo.GetParameters();

            IEnumerable<string> parts = parameterInfos.Select(parameterInfo =>
                $"{parameterInfo.ParameterType.Name} {parameterInfo.Name}");

            return $"({string.Join(", ", parts)})";
        }

        private static object ConvertFromString(Type type, string str, object fallback)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return bool.TryParse(str, out bool b) ? b : fallback;
                case TypeCode.Byte:
                    return byte.TryParse(str, out byte by) ? by : fallback;
                case TypeCode.SByte:
                    return sbyte.TryParse(str, out sbyte sb) ? sb : fallback;
                case TypeCode.Int16:
                    return short.TryParse(str, out short s) ? s : fallback;
                case TypeCode.UInt16:
                    return ushort.TryParse(str, out ushort us) ? us : fallback;
                case TypeCode.Int32:
                    return int.TryParse(str, out int i) ? i : fallback;
                case TypeCode.UInt32:
                    return uint.TryParse(str, out uint ui) ? ui : fallback;
                case TypeCode.Int64:
                    return long.TryParse(str, out long l) ? l : fallback;
                case TypeCode.UInt64:
                    return ulong.TryParse(str, out ulong ul) ? ul : fallback;
                case TypeCode.Single:
                    return float.TryParse(str, out float f) ? f : fallback;
                case TypeCode.Double:
                    return double.TryParse(str, out double d) ? d : fallback;
                case TypeCode.Decimal:
                    return decimal.TryParse(str, out decimal dec) ? dec : fallback;
                case TypeCode.DateTime:
                    return DateTime.TryParse(str, out DateTime dt) ? dt : fallback;
                case TypeCode.String:
                    return str;
                default:
                    if (type == typeof(Guid))
                    {
                        return Guid.TryParse(str, out Guid guid) ? guid : fallback;
                    }

                    if (type == typeof(TimeSpan))
                    {
                        return TimeSpan.TryParse(str, out TimeSpan ts) ? ts : fallback;
                    }

                    return fallback;
            }
        }

        private static bool ContainsSubstringIgnoreCase(string stringToSearch, string substring)
        {
            if (string.IsNullOrEmpty(substring))
            {
                return true;
            }

            if (string.IsNullOrEmpty(stringToSearch))
            {
                return false;
            }

            return stringToSearch.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color GetPingHighlightColor(float alpha)
        {
            Color pingColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 0.85f, 0.2f, 1f)
                : new Color(1f, 0.80f, 0.0f, 1f);

            pingColor.a = alpha;
            return pingColor;
        }

        #endregion
    }
}