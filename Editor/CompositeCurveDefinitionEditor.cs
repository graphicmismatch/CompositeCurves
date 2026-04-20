using System;
using UnityEditor;
using UnityEngine;

namespace CompositeCurves.Editor
{
    [CustomEditor(typeof(CompositeCurveDefinition))]
    internal sealed class CompositeCurveDefinitionEditor : UnityEditor.Editor
    {
        private const int PreviewSamples = 128;
        private const float PreviewPadding = 8f;
        private static Vector2 s_previewDragStart;

        public override void OnInspectorGUI()
        {
            var curve = (CompositeCurveDefinition)target;
            curve.EnsureIdentifiers();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Composite Curve", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Curve Id", curve.CurveId);
            }

            EditorGUI.BeginChangeCheck();
            var outsideRangeMode = (CompositeCurveOutsideRangeMode)EditorGUILayout.EnumPopup("Outside Range", curve.OutsideRangeMode);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(curve, "Change Outside Range Mode");
                curve.OutsideRangeMode = outsideRangeMode;
                MarkCurveChanged(curve, false);
            }

            DrawDefinitionVariables(curve);

            EditorGUILayout.Space();
            DrawToolbar(curve);
            EditorGUILayout.Space();
            DrawValidation(curve);
            EditorGUILayout.Space();
            DrawSegments(curve);
            EditorGUILayout.Space();
            DrawPreview(curve);
        }

        private static void DrawToolbar(CompositeCurveDefinition curve)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Preset Segment"))
                {
                    Undo.RegisterCompleteObjectUndo(curve, "Add Preset Segment");
                    var segment = CreateNewSegment(curve, false);
                    curve.Segments.Add(segment);
                    MarkCurveChanged(curve, false);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Add Custom Segment"))
                {
                    Undo.RegisterCompleteObjectUndo(curve, "Add Custom Segment");
                    var segment = CreateNewSegment(curve, true);
                    curve.Segments.Add(segment);
                    MarkCurveChanged(curve, true);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sort Segments"))
                {
                    Undo.RegisterCompleteObjectUndo(curve, "Sort Composite Curve Segments");
                    curve.SortSegmentsByDomain();
                    MarkCurveChanged(curve, false);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Regenerate Custom Curves"))
                {
                    CompositeCurveCodeGenerator.RegenerateAll();
                }
            }
        }

        private static void DrawDefinitionVariables(CompositeCurveDefinition curve)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Definition Variables", EditorStyles.miniBoldLabel);

            var variables = curve.Variables ?? Array.Empty<CompositeCurveVariable>();
            if (variables.Length == 0)
            {
                EditorGUILayout.HelpBox("This curve definition has no variables. Add variables here to share them across all segments.", MessageType.Info);
            }

            var updatedVariables = new System.Collections.Generic.List<CompositeCurveVariable>(variables.Length);
            var changed = false;

            for (var i = 0; i < variables.Length; i++)
            {
                var variable = variables[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var name = EditorGUILayout.TextField(variable.Name);
                    var valueRect = GUILayoutUtility.GetRect(
                        GUIContent.none,
                        EditorStyles.numberField,
                        GUILayout.MaxWidth(140f));
                    var value = DrawScrollableFloatField(valueRect, variable.Value);
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        changed = true;
                        continue;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        variable.Name = EnsureSharedSuffix(name);
                        variable.Value = value;
                        changed = true;
                    }
                }

                updatedVariables.Add(variable);
            }

            if (GUILayout.Button("Add Variable"))
            {
                changed = true;
                updatedVariables.Add(new CompositeCurveVariable(EnsureSharedSuffix($"var{updatedVariables.Count}"), 0f));
            }

            if (changed)
            {
                Undo.RecordObject(curve, "Edit Curve Definition Variables");
                var compacted = CompactDefinitionVariables(updatedVariables.ToArray());
                curve.SetVariables(compacted);
                MarkCurveChanged(curve, false);
            }
        }

        private static CompositeCurveVariable[] CompactDefinitionVariables(CompositeCurveVariable[] variables)
        {
            if (variables == null || variables.Length == 0)
            {
                return Array.Empty<CompositeCurveVariable>();
            }

            var count = 0;
            for (var i = 0; i < variables.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(variables[i].Name))
                {
                    count++;
                }
            }

            if (count == variables.Length)
            {
                return variables;
            }

            var compacted = new CompositeCurveVariable[count];
            var cursor = 0;
            for (var i = 0; i < variables.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(variables[i].Name))
                {
                    continue;
                }

                compacted[cursor++] = variables[i];
            }

            return compacted;
        }

        private static string EnsureSharedSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            if (string.Equals(name, "__seed__", StringComparison.Ordinal)
                || string.Equals(name, "__seed__shared", StringComparison.Ordinal))
            {
                return "__seed__shared";
            }

            return name.EndsWith("_shared", StringComparison.Ordinal) ? name : name + "_shared";
        }

        private static void DrawSegments(CompositeCurveDefinition curve)
        {
            var segments = curve.Segments;
            if (segments == null || segments.Count == 0)
            {
                EditorGUILayout.HelpBox("Add one or more segments to define the piecewise curve.", MessageType.Info);
                return;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                var foldoutKey = $"CompositeCurves.{curve.CurveId}.{segment.SegmentId}.Expanded";
                var label = string.IsNullOrWhiteSpace(segment.DisplayName) ? $"Segment {i + 1}" : segment.DisplayName;
                var expanded = SessionState.GetBool(foldoutKey, true);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    expanded = EditorGUILayout.Foldout(expanded, $"{i + 1}. {label}", true);
                    SessionState.SetBool(foldoutKey, expanded);

                    if (!expanded)
                    {
                        continue;
                    }

                    DrawSegmentFields(curve, segment, i);
                }
            }
        }

        private static void DrawSegmentFields(CompositeCurveDefinition curve, CompositeCurveSegment segment, int index)
        {
            var previousMode = segment.Mode;
            var previousPreset = segment.Preset;

            EditorGUI.BeginChangeCheck();
            var displayName = EditorGUILayout.TextField("Name", segment.DisplayName);
            var enabled = EditorGUILayout.Toggle("Enabled", segment.Enabled);
            var startX = EditorGUILayout.FloatField("Start X", segment.StartX);
            var endX = EditorGUILayout.FloatField("End X", segment.EndX);
            var startInclusion = (CompositeCurveBoundaryInclusion)EditorGUILayout.EnumPopup("Start Bound", segment.StartInclusion);
            var endInclusion = (CompositeCurveBoundaryInclusion)EditorGUILayout.EnumPopup("End Bound", segment.EndInclusion);
            var mode = (CompositeCurveSegmentMode)EditorGUILayout.EnumPopup("Mode", segment.Mode);

            var preset = segment.Preset;
            var customExpression = segment.CustomExpression;

            if (mode == CompositeCurveSegmentMode.Preset)
            {
                preset = (CompositeCurvePreset)EditorGUILayout.EnumPopup("Preset", segment.Preset);
            }
            else
            {
                EditorGUILayout.LabelField("Custom Expression", EditorStyles.miniBoldLabel);
                customExpression = EditorGUILayout.TextArea(segment.CustomExpression, GUILayout.MinHeight(44f));
                EditorGUILayout.HelpBox("Use x plus any variables you define below. Common aliases like sin(), cos(), pow(), and pi are translated to Mathf.* in generated code.", MessageType.None);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(curve, "Edit Composite Curve Segment");
                segment.DisplayName = displayName;
                segment.Enabled = enabled;
                segment.StartX = startX;
                segment.EndX = endX;
                segment.StartInclusion = startInclusion;
                segment.EndInclusion = endInclusion;
                segment.Mode = mode;
                segment.Preset = preset;
                segment.CustomExpression = customExpression;

                if (mode != previousMode || (mode == CompositeCurveSegmentMode.Preset && preset != previousPreset))
                {
                    segment.ResetVariablesToDefaults();
                }

                segment.PrepareRuntimeCache();
                MarkCurveChanged(curve, mode == CompositeCurveSegmentMode.Custom);
            }

            DrawVariables(curve, segment);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (segment.Mode == CompositeCurveSegmentMode.Preset && GUILayout.Button("Apply Preset Defaults"))
                {
                    Undo.RecordObject(curve, "Apply Composite Curve Preset Defaults");
                    segment.ResetVariablesToDefaults();
                    MarkCurveChanged(curve, false);
                }

                if (GUILayout.Button("Duplicate"))
                {
                    Undo.RegisterCompleteObjectUndo(curve, "Duplicate Composite Curve Segment");
                    var clone = segment.Clone();
                    clone.EnsureIdentifier(CreateId);
                    curve.Segments.Insert(index + 1, clone);
                    MarkCurveChanged(curve, segment.Mode == CompositeCurveSegmentMode.Custom);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Remove"))
                {
                    Undo.RegisterCompleteObjectUndo(curve, "Remove Composite Curve Segment");
                    curve.Segments.RemoveAt(index);
                    MarkCurveChanged(curve, true);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void DrawVariables(CompositeCurveDefinition curve, CompositeCurveSegment segment)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Variables", EditorStyles.miniBoldLabel);

            var variables = segment.Variables ?? Array.Empty<CompositeCurveVariable>();
            if (variables.Length == 0)
            {
                EditorGUILayout.HelpBox("This segment has no variables. Add one if the curve formula needs configurable parameters.", MessageType.Info);
            }

            var updatedVariables = new System.Collections.Generic.List<CompositeCurveVariable>(variables.Length);
            var changed = false;

            for (var i = 0; i < variables.Length; i++)
            {
                var variable = variables[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var name = EditorGUILayout.TextField(variable.Name);
                    var valueRect = GUILayoutUtility.GetRect(
                        GUIContent.none,
                        EditorStyles.numberField,
                        GUILayout.MaxWidth(140f));
                    var value = DrawScrollableFloatField(valueRect, variable.Value);
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        changed = true;
                        continue;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        variable.Name = name;
                        variable.Value = value;
                        changed = true;
                    }
                }

                updatedVariables.Add(variable);
            }

            if (GUILayout.Button("Add Variable"))
            {
                changed = true;
                updatedVariables.Add(new CompositeCurveVariable($"var{updatedVariables.Count}", 0f));
            }

            if (changed)
            {
                Undo.RecordObject(curve, "Edit Composite Curve Variables");

                var compacted = CompactVariables(updatedVariables.ToArray());
                segment.SetVariables(compacted);
                MarkCurveChanged(curve, segment.Mode == CompositeCurveSegmentMode.Custom);
            }
        }

        private static CompositeCurveVariable[] CompactVariables(CompositeCurveVariable[] variables)
        {
            if (variables == null || variables.Length == 0)
            {
                return Array.Empty<CompositeCurveVariable>();
            }

            var count = 0;
            for (var i = 0; i < variables.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(variables[i].Name))
                {
                    count++;
                }
            }

            if (count == variables.Length)
            {
                return variables;
            }

            var compacted = new CompositeCurveVariable[count];
            var cursor = 0;
            for (var i = 0; i < variables.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(variables[i].Name))
                {
                    continue;
                }

                compacted[cursor++] = variables[i];
            }

            return compacted;
        }

        private static void DrawValidation(CompositeCurveDefinition curve)
        {
            var segments = curve.Segments;
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                if (segment.EndX < segment.StartX)
                {
                    EditorGUILayout.HelpBox($"Segment '{segment.DisplayName}' ends before it starts. Swap or fix its domain.", MessageType.Error);
                }

                if (Mathf.Approximately(segment.StartX, segment.EndX)
                    && (segment.StartInclusion == CompositeCurveBoundaryInclusion.Exclusive
                    || segment.EndInclusion == CompositeCurveBoundaryInclusion.Exclusive))
                {
                    EditorGUILayout.HelpBox(
                        $"Segment '{segment.DisplayName}' has zero width and an exclusive bound, so it can never match any x value.",
                        MessageType.Warning);
                }

                if (segment.Mode == CompositeCurveSegmentMode.Custom && string.IsNullOrWhiteSpace(segment.CustomExpression))
                {
                    EditorGUILayout.HelpBox($"Custom segment '{segment.DisplayName}' needs an expression before code generation.", MessageType.Warning);
                }
            }

            for (var i = 1; i < segments.Count; i++)
            {
                var previous = segments[i - 1];
                var current = segments[i];
                if (previous == null || current == null)
                {
                    continue;
                }

                if (current.StartX < previous.EndX)
                {
                    EditorGUILayout.HelpBox(
                        $"Segments '{previous.DisplayName}' and '{current.DisplayName}' overlap. Sort them and make their domains non-overlapping for deterministic evaluation.",
                        MessageType.Warning);
                }

                if (Mathf.Approximately(current.StartX, previous.EndX)
                    && previous.EndInclusion == CompositeCurveBoundaryInclusion.Inclusive
                    && current.StartInclusion == CompositeCurveBoundaryInclusion.Inclusive)
                {
                    EditorGUILayout.HelpBox(
                        $"Segments '{previous.DisplayName}' and '{current.DisplayName}' both include the shared boundary at x = {current.StartX:0.###}. Evaluation is deterministic, but the earlier segment will win on that exact x value.",
                        MessageType.Info);
                }
            }
        }

        private static void DrawPreview(CompositeCurveDefinition curve)
        {
            curve.RebuildRuntimeCache();

            var segments = curve.Segments;
            if (segments == null || segments.Count == 0)
            {
                var emptyRect = GUILayoutUtility.GetRect(10f, 180f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(emptyRect, new Color(0.12f, 0.12f, 0.12f, 1f));
                EditorGUI.LabelField(emptyRect, "Preview unavailable: add segments first.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (!TryBuildFittedPreviewState(curve, segments, out var fittedState, out var dataState))
            {
                var emptyRect = GUILayoutUtility.GetRect(10f, 180f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(emptyRect, new Color(0.12f, 0.12f, 0.12f, 1f));
                EditorGUI.LabelField(emptyRect, "Preview unavailable: enable at least one segment.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var previewState = LoadPreviewState(curve.CurveId);
            if (!previewState.IsValid)
            {
                previewState = fittedState;
                SavePreviewState(curve.CurveId, previewState);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                if (GUILayout.Button("Reset View", GUILayout.Width(90f)))
                {
                    previewState = fittedState;
                    SavePreviewState(curve.CurveId, previewState);
                }
            }

            EditorGUILayout.LabelField(
                "Drag to pan. Scroll to zoom. Shift+Scroll zooms X only. Alt+Scroll zooms Y only.",
                EditorStyles.miniLabel);

            var rect = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
            HandlePreviewInput(curve.CurveId, rect, ref previewState);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            Handles.BeginGUI();
            DrawPreviewGrid(rect, previewState);
            DrawPreviewAxes(rect, previewState);

            Handles.color = new Color(0.2f, 0.7f, 1f, 1f);
            var values = new Vector3[PreviewSamples];
            curve.FillPreviewSamples(previewState.MinX, previewState.MaxX, values);

            for (var i = 1; i < values.Length; i++)
            {
                var from = GraphToRect(values[i - 1], rect, previewState);
                var to = GraphToRect(values[i], rect, previewState);
                Handles.DrawAAPolyLine(2f, from, to);
            }

            Handles.EndGUI();

            var statsRect = new Rect(rect.x + PreviewPadding, rect.y + PreviewPadding, rect.width - (PreviewPadding * 2f), 18f);
            EditorGUI.LabelField(
                statsRect,
                $"View X [{previewState.MinX:0.###}, {previewState.MaxX:0.###}]  Y [{previewState.MinY:0.###}, {previewState.MaxY:0.###}]",
                EditorStyles.whiteMiniLabel);

            var dataRect = new Rect(rect.x + PreviewPadding, rect.yMax - 22f, rect.width - (PreviewPadding * 2f), 18f);
            EditorGUI.LabelField(
                dataRect,
                $"Data X [{dataState.MinX:0.###}, {dataState.MaxX:0.###}]  Y [{dataState.MinY:0.###}, {dataState.MaxY:0.###}]",
                EditorStyles.whiteMiniLabel);
        }

        private static bool TryBuildFittedPreviewState(
            CompositeCurveDefinition curve,
            System.Collections.Generic.List<CompositeCurveSegment> segments,
            out PreviewState fittedState,
            out PreviewState dataState)
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null || !segment.Enabled)
                {
                    continue;
                }

                minX = Mathf.Min(minX, segment.StartX);
                maxX = Mathf.Max(maxX, segment.EndX);
            }

            if (float.IsNaN(minX) || float.IsInfinity(minX) || float.IsNaN(maxX) || float.IsInfinity(maxX))
            {
                fittedState = new PreviewState();
                dataState = new PreviewState();
                return false;
            }

            if (Mathf.Approximately(minX, maxX))
            {
                maxX = minX + 1f;
            }

            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            var values = new Vector3[PreviewSamples];
            curve.FillPreviewSamples(minX, maxX, values);
            for (var i = 0; i < values.Length; i++)
            {
                var y = values[i].y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            if (Mathf.Approximately(minY, maxY))
            {
                minY -= 1f;
                maxY += 1f;
            }

            dataState = new PreviewState(minX, maxX, minY, maxY);
            fittedState = AddPreviewPadding(dataState);
            return true;
        }

        private static PreviewState AddPreviewPadding(PreviewState state)
        {
            var xPadding = Mathf.Max(state.Width * 0.1f, 0.1f);
            var yPadding = Mathf.Max(state.Height * 0.1f, 0.1f);
            return new PreviewState(
                state.MinX - xPadding,
                state.MaxX + xPadding,
                state.MinY - yPadding,
                state.MaxY + yPadding);
        }

        private static void HandlePreviewInput(string curveId, Rect rect, ref PreviewState state)
        {
            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (rect.Contains(currentEvent.mousePosition) && (currentEvent.button == 0 || currentEvent.button == 2))
                    {
                        GUIUtility.hotControl = controlId;
                        s_previewDragStart = currentEvent.mousePosition;
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        var delta = currentEvent.mousePosition - s_previewDragStart;
                        PanPreview(rect, delta, ref state);
                        s_previewDragStart = currentEvent.mousePosition;
                        SavePreviewState(curveId, state);
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (rect.Contains(currentEvent.mousePosition))
                    {
                        ZoomPreview(rect, currentEvent.mousePosition, currentEvent.delta.y, currentEvent.shift, currentEvent.alt, ref state);
                        SavePreviewState(curveId, state);
                        GUI.changed = true;
                        currentEvent.Use();
                    }
                    break;
            }
        }

        private static void PanPreview(Rect rect, Vector2 delta, ref PreviewState state)
        {
            var width = Mathf.Max(1f, rect.width - (PreviewPadding * 2f));
            var height = Mathf.Max(1f, rect.height - (PreviewPadding * 2f));
            var deltaX = (-delta.x / width) * state.Width;
            var deltaY = (delta.y / height) * state.Height;
            state = new PreviewState(
                state.MinX + deltaX,
                state.MaxX + deltaX,
                state.MinY + deltaY,
                state.MaxY + deltaY);
        }

        private static void ZoomPreview(
            Rect rect,
            Vector2 mousePosition,
            float scrollDelta,
            bool xOnly,
            bool yOnly,
            ref PreviewState state)
        {
            var zoomFactor = Mathf.Pow(1.08f, scrollDelta);
            var normalizedX = Mathf.Clamp01((mousePosition.x - (rect.xMin + PreviewPadding)) / Mathf.Max(1f, rect.width - (PreviewPadding * 2f)));
            var normalizedY = Mathf.Clamp01((mousePosition.y - (rect.yMin + PreviewPadding)) / Mathf.Max(1f, rect.height - (PreviewPadding * 2f)));
            var anchorX = Mathf.Lerp(state.MinX, state.MaxX, normalizedX);
            var anchorY = Mathf.Lerp(state.MaxY, state.MinY, normalizedY);

            var nextMinX = xOnly || !yOnly ? anchorX + ((state.MinX - anchorX) * zoomFactor) : state.MinX;
            var nextMaxX = xOnly || !yOnly ? anchorX + ((state.MaxX - anchorX) * zoomFactor) : state.MaxX;
            var nextMinY = yOnly || !xOnly ? anchorY + ((state.MinY - anchorY) * zoomFactor) : state.MinY;
            var nextMaxY = yOnly || !xOnly ? anchorY + ((state.MaxY - anchorY) * zoomFactor) : state.MaxY;

            state = EnsurePreviewStateValid(new PreviewState(nextMinX, nextMaxX, nextMinY, nextMaxY));
        }

        private static PreviewState EnsurePreviewStateValid(PreviewState state)
        {
            const float minimumSpan = 0.001f;

            if (state.Width < minimumSpan)
            {
                var centerX = (state.MinX + state.MaxX) * 0.5f;
                state.MinX = centerX - (minimumSpan * 0.5f);
                state.MaxX = centerX + (minimumSpan * 0.5f);
            }

            if (state.Height < minimumSpan)
            {
                var centerY = (state.MinY + state.MaxY) * 0.5f;
                state.MinY = centerY - (minimumSpan * 0.5f);
                state.MaxY = centerY + (minimumSpan * 0.5f);
            }

            return state;
        }

        private static void DrawPreviewGrid(Rect rect, PreviewState state)
        {
            Handles.color = new Color(0.22f, 0.22f, 0.22f, 1f);
            for (var line = 1; line < 4; line++)
            {
                var x = Mathf.Lerp(rect.xMin + PreviewPadding, rect.xMax - PreviewPadding, line / 4f);
                Handles.DrawLine(new Vector3(x, rect.yMin + PreviewPadding), new Vector3(x, rect.yMax - PreviewPadding));
            }

            for (var line = 1; line < 4; line++)
            {
                var y = Mathf.Lerp(rect.yMin + PreviewPadding, rect.yMax - PreviewPadding, line / 4f);
                Handles.DrawLine(new Vector3(rect.xMin + PreviewPadding, y), new Vector3(rect.xMax - PreviewPadding, y));
            }
        }

        private static void DrawPreviewAxes(Rect rect, PreviewState state)
        {
            Handles.color = new Color(0.45f, 0.45f, 0.45f, 1f);

            if (state.MinX <= 0f && state.MaxX >= 0f)
            {
                var x = Mathf.Lerp(rect.xMin + PreviewPadding, rect.xMax - PreviewPadding, Mathf.InverseLerp(state.MinX, state.MaxX, 0f));
                Handles.DrawLine(new Vector3(x, rect.yMin + PreviewPadding), new Vector3(x, rect.yMax - PreviewPadding));
            }

            if (state.MinY <= 0f && state.MaxY >= 0f)
            {
                var y = Mathf.Lerp(rect.yMax - PreviewPadding, rect.yMin + PreviewPadding, Mathf.InverseLerp(state.MinY, state.MaxY, 0f));
                Handles.DrawLine(new Vector3(rect.xMin + PreviewPadding, y), new Vector3(rect.xMax - PreviewPadding, y));
            }
        }

        private static PreviewState LoadPreviewState(string curveId)
        {
            var keyPrefix = GetPreviewKeyPrefix(curveId);
            var initialized = SessionState.GetBool(keyPrefix + ".Initialized", false);
            if (!initialized)
            {
                return new PreviewState();
            }

            return new PreviewState(
                SessionState.GetFloat(keyPrefix + ".MinX", 0f),
                SessionState.GetFloat(keyPrefix + ".MaxX", 1f),
                SessionState.GetFloat(keyPrefix + ".MinY", -1f),
                SessionState.GetFloat(keyPrefix + ".MaxY", 1f));
        }

        private static void SavePreviewState(string curveId, PreviewState state)
        {
            var keyPrefix = GetPreviewKeyPrefix(curveId);
            SessionState.SetBool(keyPrefix + ".Initialized", true);
            SessionState.SetFloat(keyPrefix + ".MinX", state.MinX);
            SessionState.SetFloat(keyPrefix + ".MaxX", state.MaxX);
            SessionState.SetFloat(keyPrefix + ".MinY", state.MinY);
            SessionState.SetFloat(keyPrefix + ".MaxY", state.MaxY);
        }

        private static string GetPreviewKeyPrefix(string curveId)
        {
            return $"CompositeCurves.{curveId}.Preview";
        }

        private static Vector3 GraphToRect(Vector3 point, Rect rect, PreviewState state)
        {
            var normalizedX = Mathf.InverseLerp(state.MinX, state.MaxX, point.x);
            var normalizedY = Mathf.InverseLerp(state.MinY, state.MaxY, point.y);
            return new Vector3(
                Mathf.Lerp(rect.xMin + PreviewPadding, rect.xMax - PreviewPadding, normalizedX),
                Mathf.Lerp(rect.yMax - PreviewPadding, rect.yMin + PreviewPadding, normalizedY),
                0f);
        }

        private struct PreviewState
        {
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;

            public PreviewState(float minX, float maxX, float minY, float maxY)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
            }

            public float Width => MaxX - MinX;
            public float Height => MaxY - MinY;
            public bool IsValid => MaxX > MinX && MaxY > MinY;
        }

        private static float DrawScrollableFloatField(Rect rect, float value)
        {
            var updatedValue = EditorGUI.FloatField(rect, value);
            var currentEvent = Event.current;

            if (currentEvent == null || currentEvent.type != EventType.ScrollWheel || !rect.Contains(currentEvent.mousePosition))
            {
                return updatedValue;
            }

            var step = GetScrollStep(updatedValue, currentEvent);
            updatedValue -= Mathf.Sign(currentEvent.delta.y) * step;
            currentEvent.Use();
            GUI.changed = true;
            return updatedValue;
        }

        private static float GetScrollStep(float currentValue, Event currentEvent)
        {
            if (currentEvent.shift)
            {
                return 1f;
            }

            if (currentEvent.control || currentEvent.command)
            {
                return 0.01f;
            }

            var magnitude = Mathf.Abs(currentValue);
            if (magnitude >= 100f)
            {
                return 5f;
            }

            if (magnitude >= 10f)
            {
                return 1f;
            }

            if (magnitude >= 1f)
            {
                return 0.1f;
            }

            return 0.01f;
        }

        private static CompositeCurveSegment CreateNewSegment(CompositeCurveDefinition curve, bool custom)
        {
            var startX = GetCreationStartX(curve);
            var segment = new CompositeCurveSegment
            {
                StartX = startX,
                EndX = GetCreationEndX(startX)
            };

            if (custom)
            {
                segment.Mode = CompositeCurveSegmentMode.Custom;
                segment.DisplayName = "Custom Segment";
                segment.CustomExpression = "x";
                segment.SetVariables(Array.Empty<CompositeCurveVariable>());
            }
            else
            {
                segment.ResetVariablesToDefaults();
            }

            segment.EnsureIdentifier(CreateId);
            return segment;
        }

        private static float GetCreationStartX(CompositeCurveDefinition curve)
        {
            var segments = curve.Segments;
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                return segment.EndX;
            }

            return 0f;
        }

        private static float GetCreationEndX(float startX)
        {
            return startX + 1f;
        }

        private static void MarkCurveChanged(CompositeCurveDefinition curve, bool regenerateGeneratedCode)
        {
            curve.RebuildRuntimeCache();
            EditorUtility.SetDirty(curve);
        }

        private static string CreateId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
