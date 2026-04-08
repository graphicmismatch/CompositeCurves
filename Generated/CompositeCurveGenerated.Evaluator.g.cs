using UnityEngine;

namespace CompositeCurves
{
    public static class CompositeCurveGeneratedBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntime()
        {
            CompositeCurveGeneratedRegistry.Register(Evaluate);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditor()
        {
            CompositeCurveGeneratedRegistry.Register(Evaluate);
        }
#endif

        private static bool Evaluate(string curveId, string segmentId, float x, CompositeCurveVariable[] variables, out float value)
        {
            value = 0f;
            return false;
        }
    }
}
