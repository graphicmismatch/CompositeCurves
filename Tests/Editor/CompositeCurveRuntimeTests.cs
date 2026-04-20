using NUnit.Framework;
using UnityEngine;
using System;
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

        [Test]
        public void DefinitionVariablesHaveSharedSuffixAdded()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("myVar", 5f),
                new CompositeCurveVariable("other_shared", 10f)
            });

            Assert.AreEqual("myVar_shared", curve.Variables[0].Name);
            Assert.AreEqual("other_shared", curve.Variables[1].Name);
        }

        [Test]
        public void DefinitionVariablesAreMergedWithSegmentVariables()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("sharedVal", 100f)
            });

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Preset = CompositeCurvePreset.Constant
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("y", 50f)
            });

            curve.Segments.Add(segment);
            curve.RebuildRuntimeCache();

            var merged = curve.GetMergedVariables(segment.Variables);
            Assert.AreEqual(2, merged.Length);
            Assert.AreEqual("sharedVal_shared", merged[0].Name);
            Assert.AreEqual(100f, merged[0].Value);
            Assert.AreEqual("y", merged[1].Name);
            Assert.AreEqual(50f, merged[1].Value);
        }

        [Test]
        public void SegmentVariablesTakePrecedenceOverSharedVariables()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("sharedVal", 100f)
            });

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Preset = CompositeCurvePreset.Linear
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 1f),
                new CompositeCurveVariable("c", 0f),
                new CompositeCurveVariable("sharedVal", 999f)
            });

            curve.Segments.Add(segment);
            curve.RebuildRuntimeCache();

            var merged = curve.GetMergedVariables(segment.Variables);
            Assert.AreEqual(4, merged.Length);
            Assert.AreEqual("sharedVal_shared", merged[0].Name);
            Assert.AreEqual(100f, merged[0].Value);
            Assert.AreEqual("m", merged[1].Name);
            Assert.AreEqual("c", merged[2].Name);
            Assert.AreEqual("sharedVal", merged[3].Name);
            Assert.AreEqual(999f, merged[3].Value);
        }

        [Test]
        public void PreviewUsesFixedSeedForDeterministicRendering()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.OutsideRangeMode = CompositeCurveOutsideRangeMode.ReturnZero;

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(Array.Empty<CompositeCurveVariable>());

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var firstCall = curve.GetValueForPreview(5f);
            var secondCall = curve.GetValueForPreview(5f);
            Assert.AreEqual(firstCall, secondCall, 0.0001f);
        }

        [Test]
        public void PreviewUsesFallbackSeed1337WhenNoSeedVariableExists()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.OutsideRangeMode = CompositeCurveOutsideRangeMode.ReturnZero;

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(Array.Empty<CompositeCurveVariable>());

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var actual = curve.GetValueForPreview(5f);

            CompositeCurveRandom.ResetToFixedSeed(GetExpectedPreviewSeed(1337, 5f));
            var expected = CompositeCurveRandom.NextFloat();

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void RandomNextFloatReturnsValueBetweenZeroAndOne()
        {
            CompositeCurveRandom.ResetToFixedSeed(42);

            for (var i = 0; i < 100; i++)
            {
                var value = CompositeCurveRandom.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.LessOrEqual(value, 1f);
            }
        }

        [Test]
        public void FixedSeedProducesDeterministicRandomSequence()
        {
            CompositeCurveRandom.ResetToFixedSeed(12345);
            var first = CompositeCurveRandom.NextFloat();

            CompositeCurveRandom.ResetToFixedSeed(12345);
            var second = CompositeCurveRandom.NextFloat();

            Assert.AreEqual(first, second, 0.0001f);
        }

        [Test]
        public void RandomSeedChangesOnEachReset()
        {
            CompositeCurveRandom.ResetToRandomSeed();
            var first = CompositeCurveRandom.NextFloat();

            CompositeCurveRandom.ResetToRandomSeed();
            var second = CompositeCurveRandom.NextFloat();

            Assert.That(first, Is.Not.EqualTo(second).Within(0.0001f));
        }

        [Test]
        public void SegmentSeedTakesPrecedenceOverSharedSeed()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__shared", 11111)
            });

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__", 22222)
            });

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var actual = curve.GetValue(5f);

            CompositeCurveRandom.ResetToFixedSeed(22222);
            var expected = CompositeCurveRandom.NextFloat();

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void PreviewUsesSegmentSeedWhenProvided()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__shared", 42)
            });

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__", 22222f)
            });

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var actual = curve.GetValueForPreview(5f);

            CompositeCurveRandom.ResetToFixedSeed(GetExpectedPreviewSeed(22222, 5f));
            var expected = CompositeCurveRandom.NextFloat();

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void PreviewUsesSharedSeedWhenSegmentSeedIsMissing()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__shared", 42)
            });

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(Array.Empty<CompositeCurveVariable>());

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var actual = curve.GetValueForPreview(5f);

            CompositeCurveRandom.ResetToFixedSeed(GetExpectedPreviewSeed(42, 5f));
            var expected = CompositeCurveRandom.NextFloat();

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void SeedValueNegativeOneUsesSessionSeedAtRuntime()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__shared", 42)
            });

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(new[]
            {
                new CompositeCurveVariable("__seed__", -1f)
            });

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var firstCall = curve.GetValue(5f);
            var secondCall = curve.GetValue(5f);

            Assert.AreEqual(firstCall, secondCall, 0.0001f);
        }

        [Test]
        public void NoSeedVariableUsesSessionSeedAtRuntime()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();

            var segment = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "random()"
            };
            segment.SetVariables(Array.Empty<CompositeCurveVariable>());

            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();
            curve.RebuildRuntimeCache();

            CompositeCurveGeneratedRegistry.Register(
                delegate(string curveId, string segmentId, float x, CompositeCurveVariable[] vars, out float value)
                {
                    value = CompositeCurveRandom.NextFloat();
                    return true;
                });

            var firstCall = curve.GetValue(5f);
            var secondCall = curve.GetValue(5f);

            Assert.AreEqual(firstCall, secondCall, 0.0001f);
        }

        [Test]
        public void SharedDefinitionVariableAffectsAllSegments()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("globalMult", 10f)
            });

            var segment1 = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 10f,
                Preset = CompositeCurvePreset.Linear
            };
            segment1.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 1f),
                new CompositeCurveVariable("c", 0f)
            });

            var segment2 = new CompositeCurveSegment
            {
                StartX = 10f,
                EndX = 20f,
                Preset = CompositeCurvePreset.Linear
            };
            segment2.SetVariables(new[]
            {
                new CompositeCurveVariable("m", 1f),
                new CompositeCurveVariable("c", 0f)
            });

            curve.Segments.Add(segment1);
            curve.Segments.Add(segment2);
            curve.RebuildRuntimeCache();

            var merged1 = curve.GetMergedVariables(segment1.Variables);
            var merged2 = curve.GetMergedVariables(segment2.Variables);

            Assert.AreEqual(merged1[0].Name, "globalMult_shared");
            Assert.AreEqual(merged2[0].Name, "globalMult_shared");
            Assert.AreEqual(10f, merged1[0].Value);
            Assert.AreEqual(10f, merged2[0].Value);
        }

        private static int GetExpectedPreviewSeed(int baseSeed, float x)
        {
            unchecked
            {
                var hash = baseSeed;
                hash = (hash * 397) ^ BitConverter.SingleToInt32Bits(x);
                return hash;
            }
        }
    }
}
