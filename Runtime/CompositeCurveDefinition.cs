using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompositeCurves
{
    [CreateAssetMenu(fileName = "CompositeCurve", menuName = "Composite Curves/Curve Definition")]
    public sealed class CompositeCurveDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private string curveId = string.Empty;
        [SerializeField] private CompositeCurveOutsideRangeMode outsideRangeMode = CompositeCurveOutsideRangeMode.ClampToEdge;
        [SerializeField] private List<CompositeCurveSegment> segments = new List<CompositeCurveSegment>();

        [NonSerialized] private CompositeCurveSegment[] sortedSegments = Array.Empty<CompositeCurveSegment>();

        public string CurveId => curveId;
        public CompositeCurveOutsideRangeMode OutsideRangeMode { get => outsideRangeMode; set => outsideRangeMode = value; }
        public List<CompositeCurveSegment> Segments => segments;

        private void OnEnable()
        {
            RebuildRuntimeCache();
        }

        private void OnValidate()
        {
            RebuildRuntimeCache();
        }

        public void OnBeforeSerialize()
        {
            EnsureIdentifiers();
        }

        public void OnAfterDeserialize()
        {
            RebuildRuntimeCache();
        }

        public void EnsureIdentifiers()
        {
            if (string.IsNullOrWhiteSpace(curveId))
            {
                curveId = Guid.NewGuid().ToString("N");
            }

            if (segments == null)
            {
                segments = new List<CompositeCurveSegment>();
                return;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i] == null)
                {
                    continue;
                }

                segments[i].EnsureIdentifier(() => Guid.NewGuid().ToString("N"));
                segments[i].PrepareRuntimeCache();
            }
        }

        public void RebuildRuntimeCache()
        {
            EnsureIdentifiers();

            if (segments == null || segments.Count == 0)
            {
                sortedSegments = Array.Empty<CompositeCurveSegment>();
                return;
            }

            var buffer = new List<CompositeCurveSegment>(segments.Count);
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null || !segment.Enabled)
                {
                    continue;
                }

                segment.PrepareRuntimeCache();
                buffer.Add(segment);
            }

            buffer.Sort(CompareByStartX);
            sortedSegments = buffer.ToArray();
        }

        public void SortSegmentsByDomain()
        {
            if (segments == null)
            {
                segments = new List<CompositeCurveSegment>();
            }

            segments.Sort(CompareByStartX);
            RebuildRuntimeCache();
        }

        public float GetValue(float x)
        {
            return Evaluate(x);
        }

        public float Evaluate(float x)
        {
            if (TryGetValue(x, out var value))
            {
                return value;
            }

            return 0f;
        }

        public bool TryGetValue(float x, out float value)
        {
            if (sortedSegments == null || sortedSegments.Length == 0)
            {
                value = 0f;
                return false;
            }

            var matchIndex = FindContainingSegmentIndex(x);
            if (matchIndex >= 0)
            {
                value = sortedSegments[matchIndex].Evaluate(curveId, x);
                return true;
            }

            value = ResolveOutOfRangeValue(x);
            return outsideRangeMode != CompositeCurveOutsideRangeMode.ReturnZero || !Mathf.Approximately(value, 0f);
        }

        private float ResolveOutOfRangeValue(float x)
        {
            if (outsideRangeMode == CompositeCurveOutsideRangeMode.ReturnZero || sortedSegments.Length == 0)
            {
                return 0f;
            }

            if (x <= sortedSegments[0].StartX)
            {
                return outsideRangeMode == CompositeCurveOutsideRangeMode.ClampToEdge
                    ? sortedSegments[0].EvaluateEdge(curveId, false)
                    : sortedSegments[0].Evaluate(curveId, x);
            }

            var lastSegment = sortedSegments[sortedSegments.Length - 1];
            if (x >= lastSegment.EndX)
            {
                return outsideRangeMode == CompositeCurveOutsideRangeMode.ClampToEdge
                    ? lastSegment.EvaluateEdge(curveId, true)
                    : lastSegment.Evaluate(curveId, x);
            }

            var leftIndex = FindPreviousSegmentIndex(x);
            var rightIndex = Mathf.Clamp(leftIndex + 1, 0, sortedSegments.Length - 1);
            var leftSegment = sortedSegments[Mathf.Clamp(leftIndex, 0, sortedSegments.Length - 1)];
            var rightSegment = sortedSegments[rightIndex];

            if (outsideRangeMode == CompositeCurveOutsideRangeMode.ExtrapolateNearestSegment)
            {
                var distanceToLeft = Mathf.Abs(x - leftSegment.EndX);
                var distanceToRight = Mathf.Abs(rightSegment.StartX - x);
                return distanceToLeft <= distanceToRight
                    ? leftSegment.Evaluate(curveId, x)
                    : rightSegment.Evaluate(curveId, x);
            }

            var leftEdgeDistance = Mathf.Abs(x - leftSegment.EndX);
            var rightEdgeDistance = Mathf.Abs(rightSegment.StartX - x);
            return leftEdgeDistance <= rightEdgeDistance
                ? leftSegment.EvaluateEdge(curveId, true)
                : rightSegment.EvaluateEdge(curveId, false);
        }

        private int FindContainingSegmentIndex(float x)
        {
            var low = 0;
            var high = sortedSegments.Length - 1;

            while (low <= high)
            {
                var mid = (low + high) >> 1;
                var segment = sortedSegments[mid];

                if (segment.Contains(x))
                {
                    return mid;
                }

                if (x < segment.StartX)
                {
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return -1;
        }

        private int FindPreviousSegmentIndex(float x)
        {
            var previous = -1;
            for (var i = 0; i < sortedSegments.Length; i++)
            {
                if (sortedSegments[i].StartX >= x)
                {
                    break;
                }

                previous = i;
            }

            return previous;
        }

        private static int CompareByStartX(CompositeCurveSegment left, CompositeCurveSegment right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftMinX = Mathf.Min(left.StartX, left.EndX);
            var rightMinX = Mathf.Min(right.StartX, right.EndX);
            var startComparison = leftMinX.CompareTo(rightMinX);
            if (startComparison != 0)
            {
                return startComparison;
            }

            var leftMaxX = Mathf.Max(left.StartX, left.EndX);
            var rightMaxX = Mathf.Max(right.StartX, right.EndX);
            var endComparison = leftMaxX.CompareTo(rightMaxX);
            if (endComparison != 0)
            {
                return endComparison;
            }

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
        }
    }
}
