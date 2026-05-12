using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CompositeCurves.Editor
{
    public sealed class CompositeCurveIntegrationTests
    {
        private string originalGeneratedSource;

        [SetUp]
        public void SetUp()
        {
            CompositeCurveGeneratedRegistry.Register(null);
            originalGeneratedSource = CompositeCurveTestUtility.BackupGeneratedSource();
            CompositeCurveTestUtility.CleanupTempAssets();
        }

        [TearDown]
        public void TearDown()
        {
            CompositeCurveTestUtility.CleanupTempAssets();
            CompositeCurveTestUtility.RestoreGeneratedSource(originalGeneratedSource);
        }

        [Test]
        public void RegenerateAllWritesExpectedCustomCurveCases()
        {
            var curve = CompositeCurveTestUtility.CreateCurveAsset("GeneratorCases");
            var customSegment = new CompositeCurveSegment
            {
                DisplayName = "GeneratedSegment",
                StartX = 0f,
                EndX = 5f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "sin(x) + bias"
            };
            customSegment.SetVariables(new[]
            {
                new CompositeCurveVariable("bias", 3f)
            });

            curve.Segments.Add(customSegment);
            EditorUtility.SetDirty(curve);
            AssetDatabase.SaveAssets();

            CompositeCurveCodeGenerator.RegenerateAll();

            Assert.IsFalse(string.IsNullOrEmpty(curve.CurveId));
            Assert.IsFalse(string.IsNullOrEmpty(customSegment.SegmentId));

            var generatedSource = CompositeCurveTestUtility.ReadGeneratedSource();

            StringAssert.Contains("Mathf.Sin", generatedSource);
            StringAssert.Contains(curve.CurveId, generatedSource);
            StringAssert.Contains(customSegment.SegmentId, generatedSource);
            StringAssert.Contains("var bias = variables != null && variables.Length > 0 ? variables[0].Value : 3f;", generatedSource);
            StringAssert.Contains("value = Mathf.Sin(x) + bias;", generatedSource);
        }

        [Test]
        public void RegenerateAllSanitizesInvalidVariableNames()
        {
            var curve = CompositeCurveTestUtility.CreateCurveAsset("GeneratorSanitize");
            var customSegment = new CompositeCurveSegment
            {
                DisplayName = "SanitizeSegment",
                StartX = 0f,
                EndX = 5f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "1-bad + x"
            };
            customSegment.SetVariables(new[]
            {
                new CompositeCurveVariable("1-bad", 2f)
            });

            curve.Segments.Add(customSegment);
            EditorUtility.SetDirty(curve);
            AssetDatabase.SaveAssets();

            CompositeCurveCodeGenerator.RegenerateAll();

            var generatedSource = CompositeCurveTestUtility.ReadGeneratedSource();

            StringAssert.Contains("var _1_bad = variables != null && variables.Length > 0 ? variables[0].Value : 2f;", generatedSource);
            StringAssert.Contains("value = _1_bad + x;", generatedSource);
        }

        [Test]
        public void RegenerateAllEmitsSingleCurveSwitchCaseForMultipleCustomSegments()
        {
            var curve = CompositeCurveTestUtility.CreateCurveAsset("GeneratorSingleSwitch");

            var first = new CompositeCurveSegment
            {
                DisplayName = "A",
                StartX = 0f,
                EndX = 1f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "x"
            };

            var second = new CompositeCurveSegment
            {
                DisplayName = "B",
                StartX = 1f,
                EndX = 2f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "x * x"
            };

            curve.Segments.Add(first);
            curve.Segments.Add(second);
            EditorUtility.SetDirty(curve);
            AssetDatabase.SaveAssets();

            CompositeCurveCodeGenerator.RegenerateAll();

            Assert.IsFalse(string.IsNullOrEmpty(curve.CurveId));
            Assert.IsFalse(string.IsNullOrEmpty(first.SegmentId));
            Assert.IsFalse(string.IsNullOrEmpty(second.SegmentId));

            var generatedSource = CompositeCurveTestUtility.ReadGeneratedSource();
            var caseLabel = "case \"" + curve.CurveId + "\":";

            Assert.AreEqual(1, CountOccurrences(generatedSource, caseLabel));
            StringAssert.Contains(first.SegmentId, generatedSource);
            StringAssert.Contains(second.SegmentId, generatedSource);
        }


        [Test]
        public void RegenerateAllScopesSharedAndSegmentVariablesSeparately()
        {
            var curve = CompositeCurveTestUtility.CreateCurveAsset("GeneratorScopedVariables");
            curve.SetVariables(new[]
            {
                new CompositeCurveVariable("generatorScopedMax", 2.7f),
                new CompositeCurveVariable("generatorScopedMin", 0.3f)
            });

            var first = new CompositeCurveSegment
            {
                DisplayName = "A",
                StartX = 0f,
                EndX = 1f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "generatorScopedMax_shared - generatorScopedGain"
            };
            first.SetVariables(new[]
            {
                new CompositeCurveVariable("generatorScopedGain", 0.1f)
            });

            var second = new CompositeCurveSegment
            {
                DisplayName = "B",
                StartX = 1f,
                EndX = 2f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "generatorScopedMin_shared + generatorScopedGain"
            };
            second.SetVariables(new[]
            {
                new CompositeCurveVariable("generatorScopedGain", 0.5f)
            });

            curve.Segments.Add(first);
            curve.Segments.Add(second);
            EditorUtility.SetDirty(curve);
            AssetDatabase.SaveAssets();

            CompositeCurveCodeGenerator.RegenerateAll();

            var generatedSource = CompositeCurveTestUtility.ReadGeneratedSource();

            Assert.AreEqual(1, CountOccurrences(generatedSource, "var generatorScopedMax_shared = variables != null && variables.Length > 0 ? variables[0].Value : 2.7f;"));
            Assert.AreEqual(1, CountOccurrences(generatedSource, "var generatorScopedMin_shared = variables != null && variables.Length > 1 ? variables[1].Value : 0.3f;"));
            Assert.AreEqual(2, CountOccurrences(generatedSource, "var generatorScopedGain = variables != null && variables.Length > 2 ? variables[2].Value"));
            StringAssert.Contains("value = generatorScopedMax_shared - generatorScopedGain;", generatedSource);
            StringAssert.Contains("value = generatorScopedMin_shared + generatorScopedGain;", generatedSource);
        }

        [Test]
        public void RegenerateAllAssignsIdsToAssetBackedCurvesAndSegments()
        {
            var curve = CompositeCurveTestUtility.CreateCurveAsset("GeneratorIds");
            var customSegment = new CompositeCurveSegment
            {
                DisplayName = "NeedsIds",
                StartX = 0f,
                EndX = 1f,
                Mode = CompositeCurveSegmentMode.Custom,
                CustomExpression = "x"
            };

            curve.Segments.Add(customSegment);
            EditorUtility.SetDirty(curve);
            AssetDatabase.SaveAssets();

            CompositeCurveCodeGenerator.RegenerateAll();
            AssetDatabase.Refresh();

            Assert.IsFalse(string.IsNullOrEmpty(curve.CurveId));
            Assert.IsFalse(string.IsNullOrEmpty(customSegment.SegmentId));
        }

        private static int CountOccurrences(string source, string pattern)
        {
            var count = 0;
            var index = 0;

            while (true)
            {
                index = source.IndexOf(pattern, index, System.StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                count++;
                index += pattern.Length;
            }

            return count;
        }
    }
}
