using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CompositeCurves.Editor
{
    public sealed class CompositeCurveRegressionTests
    {
        [SetUp]
        public void SetUp()
        {
            CompositeCurveGeneratedRegistry.Register(null);
        }

        [Test]
        public void SortingPreservesSegmentModeAndVariables()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();

            var customSegment = new CompositeCurveSegment
            {
                DisplayName = "Custom",
                StartX = 10f,
                EndX = 20f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "x + gain"
            };
            customSegment.SetVariables(new[]
            {
                new CompositeCurveVariable("gain", 5f)
            });

            var presetSegment = new CompositeCurveSegment
            {
                DisplayName = "Preset",
                StartX = 0f,
                EndX = 5f,
                Mode = CompositeCurveSegmentMode.Preset,
                Preset = CompositeCurvePreset.Quadratic
            };
            presetSegment.SetVariables(new[]
            {
                new CompositeCurveVariable("a", 2f),
                new CompositeCurveVariable("b", 3f),
                new CompositeCurveVariable("c", 4f)
            });

            curve.Segments.Add(customSegment);
            curve.Segments.Add(presetSegment);

            curve.SortSegmentsByDomain();

            Assert.AreSame(presetSegment, curve.Segments[0]);
            Assert.AreSame(customSegment, curve.Segments[1]);
            Assert.AreEqual(CompositeCurveSegmentMode.Preset, curve.Segments[0].Mode);
            Assert.AreEqual(CompositeCurvePreset.Quadratic, curve.Segments[0].Preset);
            Assert.AreEqual("gain", curve.Segments[1].Variables[0].Name);
            Assert.AreEqual(5f, curve.Segments[1].Variables[0].Value, 0.0001f);
        }

        [Test]
        public void SharedInclusiveBoundaryUsesEarlierSegment()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();

            var first = new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 1f,
                StartInclusion = CompositeCurveBoundaryInclusion.Inclusive,
                EndInclusion = CompositeCurveBoundaryInclusion.Inclusive,
                Preset = CompositeCurvePreset.Constant
            };
            first.SetVariables(new[]
            {
                new CompositeCurveVariable("y", 10f)
            });

            var second = new CompositeCurveSegment
            {
                StartX = 1f,
                EndX = 2f,
                StartInclusion = CompositeCurveBoundaryInclusion.Inclusive,
                EndInclusion = CompositeCurveBoundaryInclusion.Exclusive,
                Preset = CompositeCurvePreset.Constant
            };
            second.SetVariables(new[]
            {
                new CompositeCurveVariable("y", 20f)
            });

            curve.Segments.Add(first);
            curve.Segments.Add(second);
            curve.RebuildRuntimeCache();

            Assert.AreEqual(10f, curve.GetValue(1f), 0.0001f);
        }

        [Test]
        public void EnsureIdentifiersKeepsExistingIdentifiersStable()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            var segment = new CompositeCurveSegment();
            segment.EnsureIdentifier(delegate { return "segment-original"; });
            curve.Segments.Add(segment);
            curve.EnsureIdentifiers();

            var firstCurveId = curve.CurveId;
            var firstSegmentId = segment.SegmentId;

            curve.EnsureIdentifiers();

            Assert.AreEqual(firstCurveId, curve.CurveId);
            Assert.AreEqual(firstSegmentId, segment.SegmentId);
        }

        [Test]
        public void ClonePreservesBoundsModeAndVariablesButGetsNewIdentifierWhenEnsured()
        {
            var original = new CompositeCurveSegment
            {
                StartX = 2f,
                EndX = 6f,
                StartInclusion = CompositeCurveBoundaryInclusion.Exclusive,
                EndInclusion = CompositeCurveBoundaryInclusion.Inclusive,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "x + bonus"
            };
            original.EnsureIdentifier(delegate { return "original-id"; });
            original.SetVariables(new[]
            {
                new CompositeCurveVariable("bonus", 7f)
            });

            var clone = original.Clone();
            clone.EnsureIdentifier(delegate { return "clone-id"; });

            Assert.AreEqual(2f, clone.StartX, 0.0001f);
            Assert.AreEqual(6f, clone.EndX, 0.0001f);
            Assert.AreEqual(CompositeCurveBoundaryInclusion.Exclusive, clone.StartInclusion);
            Assert.AreEqual(CompositeCurveBoundaryInclusion.Inclusive, clone.EndInclusion);
            Assert.AreEqual(CompositeCurveSegmentMode.Custom, clone.Mode);
            Assert.AreEqual("x + bonus", clone.CustomExpression);
            Assert.AreEqual("bonus", clone.Variables[0].Name);
            Assert.AreEqual(7f, clone.Variables[0].Value, 0.0001f);
            Assert.AreNotEqual(original.SegmentId, clone.SegmentId);
        }

        [Test]
        public void CreateNewSegmentUsesLastSegmentEndOnlyAtCreation()
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            curve.Segments.Add(new CompositeCurveSegment
            {
                StartX = 0f,
                EndX = 4.5f
            });

            var createMethod = typeof(CompositeCurveDefinitionEditor).GetMethod(
                "CreateNewSegment",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(createMethod);

            var createdSegment = (CompositeCurveSegment)createMethod.Invoke(null, new object[] { curve, false });
            Assert.AreEqual(4.5f, createdSegment.StartX, 0.0001f);
            Assert.AreEqual(5.5f, createdSegment.EndX, 0.0001f);

            curve.Segments[0].EndX = 9f;

            Assert.AreEqual(4.5f, createdSegment.StartX, 0.0001f);
            Assert.AreEqual(5.5f, createdSegment.EndX, 0.0001f);
        }
    }
}
