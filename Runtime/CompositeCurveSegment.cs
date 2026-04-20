using System;
using UnityEngine;

namespace CompositeCurves
{
    [Serializable]
    public sealed class CompositeCurveSegment
    {
        [SerializeField] private string segmentId = string.Empty;
        [SerializeField] private string displayName = "Segment";
        [SerializeField] private bool enabled = true;
        [SerializeField] private float startX = 0f;
        [SerializeField] private float endX = 1f;
        [SerializeField] private CompositeCurveBoundaryInclusion startInclusion = CompositeCurveBoundaryInclusion.Inclusive;
        [SerializeField] private CompositeCurveBoundaryInclusion endInclusion = CompositeCurveBoundaryInclusion.Exclusive;
        [SerializeField] private CompositeCurveSegmentMode mode = CompositeCurveSegmentMode.Preset;
        [SerializeField] private CompositeCurvePreset preset = CompositeCurvePreset.Linear;
        [SerializeField] private string customExpression = "x";
        [SerializeField] private CompositeCurveVariable[] variables = Array.Empty<CompositeCurveVariable>();

        [NonSerialized] private float cached0;
        [NonSerialized] private float cached1;
        [NonSerialized] private float cached2;
        [NonSerialized] private float cached3;

        public string SegmentId => segmentId;
        public string DisplayName { get => displayName; set => displayName = value; }
        public bool Enabled { get => enabled; set => enabled = value; }
        public float StartX { get => startX; set => startX = value; }
        public float EndX { get => endX; set => endX = value; }
        public CompositeCurveBoundaryInclusion StartInclusion { get => startInclusion; set => startInclusion = value; }
        public CompositeCurveBoundaryInclusion EndInclusion { get => endInclusion; set => endInclusion = value; }
        public CompositeCurveSegmentMode Mode { get => mode; set => mode = value; }
        public CompositeCurvePreset Preset { get => preset; set => preset = value; }
        public string CustomExpression { get => customExpression; set => customExpression = value; }
        public CompositeCurveVariable[] Variables => variables;

        public void EnsureIdentifier(Func<string> idFactory)
        {
            if (!string.IsNullOrWhiteSpace(segmentId))
            {
                return;
            }

            segmentId = idFactory != null ? idFactory() : Guid.NewGuid().ToString("N");
        }

        public void SetVariables(CompositeCurveVariable[] newVariables)
        {
            variables = newVariables ?? Array.Empty<CompositeCurveVariable>();
            PrepareRuntimeCache();
        }

        public void ResetVariablesToDefaults()
        {
            variables = CreateDefaultVariables(mode, preset);
            PrepareRuntimeCache();
        }

        public void PrepareRuntimeCache()
        {
            if (variables == null)
            {
                variables = Array.Empty<CompositeCurveVariable>();
            }

            cached0 = 0f;
            cached1 = 0f;
            cached2 = 0f;
            cached3 = 0f;

            if (mode != CompositeCurveSegmentMode.Preset)
            {
                return;
            }

            switch (preset)
            {
                case CompositeCurvePreset.Constant:
                    cached0 = GetVariableValue("y", 0f);
                    break;
                case CompositeCurvePreset.Linear:
                    cached0 = GetVariableValue("m", 1f);
                    cached1 = GetVariableValue("c", 0f);
                    break;
                case CompositeCurvePreset.Quadratic:
                    cached0 = GetVariableValue("a", 1f);
                    cached1 = GetVariableValue("b", 0f);
                    cached2 = GetVariableValue("c", 0f);
                    break;
                case CompositeCurvePreset.Cubic:
                    cached0 = GetVariableValue("a", 1f);
                    cached1 = GetVariableValue("b", 0f);
                    cached2 = GetVariableValue("c", 0f);
                    cached3 = GetVariableValue("d", 0f);
                    break;
                case CompositeCurvePreset.Sine:
                case CompositeCurvePreset.Cosine:
                case CompositeCurvePreset.Tangent:
                    cached0 = GetVariableValue("amplitude", 1f);
                    cached1 = GetVariableValue("frequency", 1f);
                    cached2 = GetVariableValue("phase", 0f);
                    cached3 = GetVariableValue("offset", 0f);
                    break;
                case CompositeCurvePreset.QuadraticBezier:
                    cached0 = GetVariableValue("y0", 0f);
                    cached1 = GetVariableValue("y1", 0.5f);
                    cached2 = GetVariableValue("y2", 1f);
                    break;
                case CompositeCurvePreset.CubicBezier:
                    cached0 = GetVariableValue("y0", 0f);
                    cached1 = GetVariableValue("y1", 0.33f);
                    cached2 = GetVariableValue("y2", 0.66f);
                    cached3 = GetVariableValue("y3", 1f);
                    break;
            }
        }

        public bool Contains(float x)
        {
            if (!enabled)
            {
                return false;
            }

            if (x < startX || (x <= startX && startInclusion == CompositeCurveBoundaryInclusion.Exclusive))
            {
                return false;
            }

            if (x > endX)
            {
                return false;
            }

            if (x >= endX && endInclusion == CompositeCurveBoundaryInclusion.Exclusive)
            {
                return false;
            }

            return true;
        }

        public float Evaluate(string curveId, float x)
        {
            return EvaluateWithVariables(curveId, x, variables, false, 0);
        }

        public float EvaluateWithVariables(string curveId, float x, CompositeCurveVariable[] mergedVariables)
        {
            return EvaluateWithVariables(curveId, x, mergedVariables, false, 0);
        }

        public float EvaluateWithVariables(string curveId, float x, CompositeCurveVariable[] mergedVariables, bool useFixedSeed, int fixedSeed)
        {
            if (!enabled)
            {
                return 0f;
            }

            if (mode == CompositeCurveSegmentMode.Custom)
            {
                if (CompositeCurveGeneratedRegistry.TryEvaluate(curveId, segmentId, x, mergedVariables, useFixedSeed, fixedSeed, out var generatedValue))
                {
                    return generatedValue;
                }

                return 0f;
            }

            return EvaluatePresetWithVariables(x, mergedVariables);
        }

        public float EvaluateEdge(string curveId, bool useUpperEdge)
        {
            return Evaluate(curveId, useUpperEdge ? endX : startX);
        }

        public float EvaluateEdgeWithVariables(string curveId, bool useUpperEdge, CompositeCurveVariable[] mergedVariables)
        {
            return EvaluateWithVariables(curveId, useUpperEdge ? endX : startX, mergedVariables);
        }

        public float EvaluateEdgeWithVariables(string curveId, bool useUpperEdge, CompositeCurveVariable[] mergedVariables, bool useFixedSeed, int fixedSeed)
        {
            return EvaluateWithVariables(curveId, useUpperEdge ? endX : startX, mergedVariables, useFixedSeed, fixedSeed);
        }

        public CompositeCurveSegment Clone()
        {
            var clone = new CompositeCurveSegment
            {
                segmentId = string.Empty,
                displayName = displayName,
                enabled = enabled,
                startX = startX,
                endX = endX,
                startInclusion = startInclusion,
                endInclusion = endInclusion,
                mode = mode,
                preset = preset,
                customExpression = customExpression,
                variables = CopyVariables(variables)
            };
            clone.PrepareRuntimeCache();
            return clone;
        }

        public static CompositeCurveVariable[] CreateDefaultVariables(
            CompositeCurveSegmentMode segmentMode,
            CompositeCurvePreset segmentPreset)
        {
            if (segmentMode == CompositeCurveSegmentMode.Custom)
            {
                return Array.Empty<CompositeCurveVariable>();
            }

            switch (segmentPreset)
            {
                case CompositeCurvePreset.Constant:
                    return new[] { new CompositeCurveVariable("y", 0f) };
                case CompositeCurvePreset.Linear:
                    return new[]
                    {
                        new CompositeCurveVariable("m", 1f),
                        new CompositeCurveVariable("c", 0f)
                    };
                case CompositeCurvePreset.Quadratic:
                    return new[]
                    {
                        new CompositeCurveVariable("a", 1f),
                        new CompositeCurveVariable("b", 0f),
                        new CompositeCurveVariable("c", 0f)
                    };
                case CompositeCurvePreset.Cubic:
                    return new[]
                    {
                        new CompositeCurveVariable("a", 1f),
                        new CompositeCurveVariable("b", 0f),
                        new CompositeCurveVariable("c", 0f),
                        new CompositeCurveVariable("d", 0f)
                    };
                case CompositeCurvePreset.Sine:
                case CompositeCurvePreset.Cosine:
                case CompositeCurvePreset.Tangent:
                    return new[]
                    {
                        new CompositeCurveVariable("amplitude", 1f),
                        new CompositeCurveVariable("frequency", 1f),
                        new CompositeCurveVariable("phase", 0f),
                        new CompositeCurveVariable("offset", 0f)
                    };
                case CompositeCurvePreset.QuadraticBezier:
                    return new[]
                    {
                        new CompositeCurveVariable("y0", 0f),
                        new CompositeCurveVariable("y1", 0.5f),
                        new CompositeCurveVariable("y2", 1f)
                    };
                case CompositeCurvePreset.CubicBezier:
                    return new[]
                    {
                        new CompositeCurveVariable("y0", 0f),
                        new CompositeCurveVariable("y1", 0.33f),
                        new CompositeCurveVariable("y2", 0.66f),
                        new CompositeCurveVariable("y3", 1f)
                    };
                default:
                    return Array.Empty<CompositeCurveVariable>();
            }
        }

        private static CompositeCurveVariable[] CopyVariables(CompositeCurveVariable[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<CompositeCurveVariable>();
            }

            var copy = new CompositeCurveVariable[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private float EvaluatePreset(float x)
        {
            switch (preset)
            {
                case CompositeCurvePreset.Constant:
                    return cached0;
                case CompositeCurvePreset.Linear:
                    return (cached0 * x) + cached1;
                case CompositeCurvePreset.Quadratic:
                    return (((cached0 * x) + cached1) * x) + cached2;
                case CompositeCurvePreset.Cubic:
                    return ((((cached0 * x) + cached1) * x) + cached2) * x + cached3;
                case CompositeCurvePreset.Sine:
                    return cached0 * Mathf.Sin((cached1 * x) + cached2) + cached3;
                case CompositeCurvePreset.Cosine:
                    return cached0 * Mathf.Cos((cached1 * x) + cached2) + cached3;
                case CompositeCurvePreset.Tangent:
                    return cached0 * Mathf.Tan((cached1 * x) + cached2) + cached3;
                case CompositeCurvePreset.QuadraticBezier:
                {
                    var t = NormalizeInput(x);
                    var oneMinusT = 1f - t;
                    return (oneMinusT * oneMinusT * cached0)
                        + (2f * oneMinusT * t * cached1)
                        + (t * t * cached2);
                }
                case CompositeCurvePreset.CubicBezier:
                {
                    var t = NormalizeInput(x);
                    var oneMinusT = 1f - t;
                    return (oneMinusT * oneMinusT * oneMinusT * cached0)
                        + (3f * oneMinusT * oneMinusT * t * cached1)
                        + (3f * oneMinusT * t * t * cached2)
                        + (t * t * t * cached3);
                }
                default:
                    return 0f;
            }
        }

        private float NormalizeInput(float x)
        {
            var width = endX - startX;
            if (Mathf.Approximately(width, 0f))
            {
                return 0f;
            }

            return Mathf.Clamp01((x - startX) / width);
        }

        private float GetVariableValue(string name, float fallback)
        {
            return GetVariableValue(name, fallback, variables);
        }

        private float GetVariableValue(string name, float fallback, CompositeCurveVariable[] variableSource)
        {
            if (variableSource == null)
            {
                return fallback;
            }

            for (var i = 0; i < variableSource.Length; i++)
            {
                if (string.Equals(variableSource[i].Name, name, StringComparison.Ordinal))
                {
                    return variableSource[i].Value;
                }
            }

            return fallback;
        }

        private float EvaluatePresetWithVariables(float x, CompositeCurveVariable[] variableSource)
        {
            switch (preset)
            {
                case CompositeCurvePreset.Constant:
                    return GetVariableValue("y", 0f, variableSource);
                case CompositeCurvePreset.Linear:
                    return (GetVariableValue("m", 1f, variableSource) * x) + GetVariableValue("c", 0f, variableSource);
                case CompositeCurvePreset.Quadratic:
                    return (((GetVariableValue("a", 1f, variableSource) * x) + GetVariableValue("b", 0f, variableSource)) * x) + GetVariableValue("c", 0f, variableSource);
                case CompositeCurvePreset.Cubic:
                    return ((((GetVariableValue("a", 1f, variableSource) * x) + GetVariableValue("b", 0f, variableSource)) * x) + GetVariableValue("c", 0f, variableSource)) * x + GetVariableValue("d", 0f, variableSource);
                case CompositeCurvePreset.Sine:
                    return GetVariableValue("amplitude", 1f, variableSource) * Mathf.Sin((GetVariableValue("frequency", 1f, variableSource) * x) + GetVariableValue("phase", 0f, variableSource)) + GetVariableValue("offset", 0f, variableSource);
                case CompositeCurvePreset.Cosine:
                    return GetVariableValue("amplitude", 1f, variableSource) * Mathf.Cos((GetVariableValue("frequency", 1f, variableSource) * x) + GetVariableValue("phase", 0f, variableSource)) + GetVariableValue("offset", 0f, variableSource);
                case CompositeCurvePreset.Tangent:
                    return GetVariableValue("amplitude", 1f, variableSource) * Mathf.Tan((GetVariableValue("frequency", 1f, variableSource) * x) + GetVariableValue("phase", 0f, variableSource)) + GetVariableValue("offset", 0f, variableSource);
                case CompositeCurvePreset.QuadraticBezier:
                {
                    var t = NormalizeInput(x);
                    var oneMinusT = 1f - t;
                    return (oneMinusT * oneMinusT * GetVariableValue("y0", 0f, variableSource))
                        + (2f * oneMinusT * t * GetVariableValue("y1", 0.5f, variableSource))
                        + (t * t * GetVariableValue("y2", 1f, variableSource));
                }
                case CompositeCurvePreset.CubicBezier:
                {
                    var t = NormalizeInput(x);
                    var oneMinusT = 1f - t;
                    return (oneMinusT * oneMinusT * oneMinusT * GetVariableValue("y0", 0f, variableSource))
                        + (3f * oneMinusT * oneMinusT * t * GetVariableValue("y1", 0.33f, variableSource))
                        + (3f * oneMinusT * t * t * GetVariableValue("y2", 0.66f, variableSource))
                        + (t * t * t * GetVariableValue("y3", 1f, variableSource));
                }
                default:
                    return 0f;
            }
        }
    }
}
