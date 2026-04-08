using System;
using UnityEngine;

namespace CompositeCurves
{
    public enum CompositeCurveSegmentMode
    {
        Preset = 0,
        Custom = 1
    }

    public enum CompositeCurvePreset
    {
        Constant = 0,
        Linear = 1,
        Quadratic = 2,
        Cubic = 3,
        Sine = 4,
        Cosine = 5,
        Tangent = 6,
        QuadraticBezier = 7,
        CubicBezier = 8
    }

    public enum CompositeCurveOutsideRangeMode
    {
        ReturnZero = 0,
        ClampToEdge = 1,
        ExtrapolateNearestSegment = 2
    }

    public enum CompositeCurveBoundaryInclusion
    {
        Exclusive = 0,
        Inclusive = 1
    }

    [Serializable]
    public struct CompositeCurveVariable
    {
        public string Name;
        public float Value;

        public CompositeCurveVariable(string name, float value)
        {
            Name = name;
            Value = value;
        }
    }
}
