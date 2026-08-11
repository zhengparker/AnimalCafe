using System;
using System.Linq;
using System.Reflection;

namespace AnimalCafe.Tests.PlayMode
{
    internal static class EditorPrebuildScopeBridge
    {
        public static void Setup(string typeName)
        {
            Invoke(typeName, "Setup");
        }

        public static void Cleanup(string typeName)
        {
            Invoke(typeName, "Cleanup");
        }

        private static void Invoke(string typeName, string methodName)
        {
#if UNITY_EDITOR
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null)
            {
                throw new InvalidOperationException(
                    $"Editor prebuild scope '{typeName}' was not found.");
            }

            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException(
                    $"Editor prebuild scope '{typeName}' has no public static {methodName}().");
            }

            method.Invoke(null, null);
#endif
        }
    }
}
