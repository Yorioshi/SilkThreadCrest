using System;

namespace SilkThreadCrest
{
    internal static class SilkThreadEclipsePose
    {
        internal static int Select(string clipName, int frame, int frameCount)
        {
            string name = (clipName ?? string.Empty).ToLowerInvariant();
            if (name.Length == 0)
                return -1;
            // Never replace an unsupported action with an unrelated idle or slash pose.
            if (Has(name, "hurt", "death", "dead", "respawn", "bench", "sit", "wall", "mantle", "swim",
                "dash", "sprint", "roll", "spin", "double jump", "doublejump", "downspike", "down spike",
                "downslash", "down slash", "upslash", "up slash", "needle throw", "throw", "cutscene", "recoil",
                "charged", "stun", "wake", "door", "harpoon", "fireball", "umbrella", "shuttlecock", "super jump"))
                return -1;
            if (Has(name, "bind", "focus", "heal"))
                return 7;
            if (Has(name, "fall"))
                return 5;
            if (Has(name, "jump"))
                return 4;
            if (Has(name, "attack", "slash"))
            {
                float progress = frameCount > 1 ? (float)Math.Max(0, Math.Min(frame, frameCount - 1)) / (frameCount - 1) : 0.5f;
                return progress >= 0.20f && progress <= 0.70f ? 6 : -1;
            }
            if (Has(name, "walk", "run"))
                return ProgressIndex(frame, frameCount, 2, 3);
            if (Has(name, "idle", "stand", "rest"))
                return ProgressIndex(frame, frameCount, 0, 1);
            return -1;
        }

        private static int ProgressIndex(int frame, int frameCount, int first, int second)
        {
            if (frameCount <= 1)
                return first;
            int bounded = Math.Max(0, Math.Min(frame, frameCount - 1));
            return bounded < (frameCount + 1) / 2 ? first : second;
        }

        private static bool Has(string value, params string[] terms)
        {
            foreach (string term in terms)
                if (value.IndexOf(term, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }
    }
}
