namespace UnityEngine
{
    // NPCNativeLib logs through UnityEngine.Debug; this stands in for it outside Unity.
    internal static class Debug
    {
        public static void LogWarning(string message) => System.Console.Error.WriteLine("[warn] " + message);
    }
}
