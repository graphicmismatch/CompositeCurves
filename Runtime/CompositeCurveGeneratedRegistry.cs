namespace CompositeCurves
{
    public static class CompositeCurveGeneratedRegistry
    {
        public delegate bool GeneratedCurveEvaluator(
            string curveId,
            string segmentId,
            float x,
            CompositeCurveVariable[] variables,
            out float value);

        private static GeneratedCurveEvaluator s_evaluator = FallbackEvaluator;

        public static void Register(GeneratedCurveEvaluator evaluator)
        {
            s_evaluator = evaluator ?? FallbackEvaluator;
        }

        public static bool TryEvaluate(
            string curveId,
            string segmentId,
            float x,
            CompositeCurveVariable[] variables,
            out float value)
        {
            return s_evaluator(curveId, segmentId, x, variables, out value);
        }

        private static bool FallbackEvaluator(
            string curveId,
            string segmentId,
            float x,
            CompositeCurveVariable[] variables,
            out float value)
        {
            value = 0f;
            return false;
        }
    }
}
