using NUnit.Framework;
using UnityEngine;

namespace CompositeCurves.Editor
{
    public sealed class CompositeCurveRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            CompositeCurveGeneratedRegistry.Register(null);
        }

        [Test]
        public void LinearPresetEvaluatesConfiguredVariables()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Preset,
                Preset = CompositeCurvePreset.Linear
            };

            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 2f),
                new CompositeCurveVariable("c", 3f)
            });

            curve.Segments.Add(segment);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(11f, curve.GetValue(4f), 0.0001f);
        }

        [Test]
        public void InclusiveAndExclusiveBoundsAreRespected()
        {
            var inclusive = new CompositeCurveSegment
            {
                StartX = 1f,
                EndX = 2f,
                StartInclusion = CompositeCurveBoundaryInclusion.Inclusive,
                EndInclusion = CompositeCurveBoundaryInclusion.Inclusive
            };

            var exclusive = new CompositeCurveSegment
            {
                StartX = 1f,
                EndX = 2f,
                StartInclusion = CompositeCurveBoundaryInclusion.Exclusive,
                EndInclusion = CompositeCurveBoundaryInclusion.Exclusive
            };

            Assert.IsTrue(inclusive.Contains(1f));
            Assert.IsTrue(inclusive.Contains(2f));
            Assert.IsFalse(exclusive.Contains(1f));
            Assert.IsFalse(exclusive.Contains(2f));
            Assert.IsTrue(exclusive.Contains(1.5f));
        }

        [Test]
        public void SortSegmentsByDomainOrdersByActualRangeStart()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();

            var late = new CompositeCurveSegment
            {
                DisplayName = "Late",
                StartX = 10f,
                EndX = 12f
            };

            var earlyReversed = new CompositeCurveSegment
            {
                DisplayName = "Early",
                StartX = 5f,
                EndX = 2f
            };

            curve.Segments.Add(late);
            curve.Segments.Add(earlyReversed);

            curve.SortSegmentsByDomain();

            Assert.AreSame(earlyReversed, curve.Segments[0]);
            Assert.AreSame(late, curve.Segments[1]);
        }

        [Test]
        public void DisabledSegmentsAreIgnoredDuringEvaluation()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.OutsideRangeMode = CompositeCurveOutsideRangeMode.ReturnZero;

            var disabled = new CompositeCurveSegment
            {
                Enabled = false,
                StartX = 0f,
                EndX = 5f,
                Preset = CompositeCurvePreset.Constant
            };
            disabled.SetVariables(new[]
            {
                new CompositeCurveVariable("y", 99f)
            });

            curve.Segments.Add(disabled);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(0f, curve.GetValue(2f), 0.0001f);
            float unusedValue;
            Assert.IsFalse(curve.TryGetValue(2f, out unusedValue));
        }

        [Test]
        public void ClampToEdgeUsesNearestSegmentEdgeOutsideRange()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.OutsideRangeMode = CompositeCurveOutsideRangeMode.ClampToEdge;

            var segment = new CompositeCurveSegment
            {
                StartX = 1f,
                EndX = 3f,
                Preset = CompositeCurvePreset.Linear
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 2f),
                new CompositeCurveVariable("c", 1f)
            });

            curve.Segments.Add(segment);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(3f, curve.GetValue(1f), 0.0001f);
            Assert.AreEqual(7f, curve.GetValue(100f), 0.0001f);
        }

        [Test]
        public void CustomSegmentUsesGeneratedRegistry()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "x + bias"
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("bias", 4f)
            });

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] variables, out float value)
                {
                    value = x + variables[0].Value;
                    return true;
                });

            Assert.AreEqual(6f, curve.GetValue(2f), 0.0001f);
        }

        [Test]
        public void QuadraticPresetEvaluatesExpectedPolynomial()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            var segment = new CompositeCurveSegment
            {
                StartX = -10f,
                EndX = 10f,
                Preset = CompositeCurvePreset.Quadratic
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("a", 1f),
                new CompositeCurveVariable("b", 2f),
                new CompositeCurveVariable("c", 3f)
            });

            curve.Segments.Add(segment);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(11f, curve.GetValue(2f), 0.0001f);
        }

        [Test]
        public void CubicBezierPresetNormalizesInputAcrossSegmentDomain()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            var segment = new CompositeCurveSegment
            {
                StartX = 10f,
                EndX = 20f,
                Preset = CompositeCurvePreset.CubicBezier
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("y0", 0f),
                new CompositeCurveVariable("y1", 0f),
                new CompositeCurveVariable("y2", 10f),
                new CompositeCurveVariable("y3", 10f)
            });

            curve.Segments.Add(segment);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(0f, curve.GetValue(10f), 0.0001f);
            Assert.AreEqual(10f, curve.GetValue(20f), 0.0001f);
            Assert.Greater(curve.GetValue(15f), 0f);
        }

        [Test]
        public void ExtrapolateNearestSegmentUsesNearestSegmentAcrossGap()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.OutsideRangeMode = CompositeCurveOutsideRangeMode.ExtrapolateNearestSegment;

            var left = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 2f,
                Preset = CompositeCurvePreset.Linear
            };
            left.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 1f),
                new CompositeCurveVariable("c", 0f)
            });

            var right = new CompositeCurveSegment
            {
                StartX = 5f,
                EndX = 7f,
                Preset = CompositeCurvePreset.Linear
            };
            right.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 10f),
                new CompositeCurveVariable("c", 0f)
            });

            curve.Segments.Add(left);
            curve.Segments.Add(right);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(3f, curve.GetValue(3f), 0.0001f);
            Assert.AreEqual(40f, curve.GetValue(4f), 0.0001f);
        }

        [Test]
        public void ClampToEdgeUsesNearestBoundaryInsideGap()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.OutsideRangeMode = CompositeCurveOutsideRangeMode.ClampToEdge;

            var left = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 2f,
                Preset = CompositeCurvePreset.Constant
            };
            left.SetVariables(new[]
            {
                new CompositeCurveVariable("y", 5f)
            });

            var right = new CompositeCurveSegment
            {
                StartX = 6f,
                EndX = 8f,
                Preset = CompositeCurvePreset.Constant
            };
            right.SetVariables(new[]
            {
                new CompositeCurveVariable("y", 9f)
            });

            curve.Segments.Add(left);
            curve.Segments.Add(right);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(5f, curve.GetValue(3f), 0.0001f);
            Assert.AreEqual(9f, curve.GetValue(5f), 0.0001f);
        }
    }
}
