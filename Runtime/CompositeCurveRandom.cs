using System;
using UnityEngine;

namespace CompositeCurves
{
    public static class CompositeCurveRandom
    {
        private static System.Random s_random = new System.Random();
        private static bool s_useRandomSeed = true;
        private static int s_seed = 0;
        private static int s_sessionSeed = CreateSessionSeed();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RefreshSessionSeed()
        {
            s_sessionSeed = CreateSessionSeed();
            ResetToSessionSeed();
        }

        private static int CreateSessionSeed()
        {
            return Guid.NewGuid().GetHashCode();
        }

        public static void SetSeed(int seed)
        {
            s_seed = seed;
            s_useRandomSeed = false;
            s_random = new System.Random(seed);
        }

        public static void ResetToRandomSeed()
        {
            s_useRandomSeed = true;
            s_seed = CreateSessionSeed();
            s_random = new System.Random(s_seed);
        }

        public static void ResetToFixedSeed(int seed)
        {
            SetSeed(seed);
        }

        public static void ResetToSessionSeed()
        {
            s_useRandomSeed = true;
            s_seed = s_sessionSeed;
            s_random = new System.Random(s_seed);
        }

        public static float NextFloat()
        {
            return (float)s_random.NextDouble();
        }

        public static float NextFloat(float min, float max)
        {
            return min + ((float)s_random.NextDouble() * (max - min));
        }

        internal static int Seed => s_seed;
        internal static int SessionSeed => s_sessionSeed;
        internal static bool IsUsingRandomSeed => s_useRandomSeed;
    }
}
