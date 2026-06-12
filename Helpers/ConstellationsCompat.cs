using System;
using System.Linq;
using System.Reflection;

namespace RouteRandomRedexed;

internal static class ConstellationsCompat
{
    private const string pluginAssemblyName = "LethalConstellations";
    private const string classMapperTypeName = "LethalConstellations.PluginCore.ClassMapper";

    internal static bool IsLevelInConstellation(SelectableLevel level) {
        Assembly pluginAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, pluginAssemblyName, StringComparison.InvariantCultureIgnoreCase));

        Type classMapperType = pluginAssembly?.GetType(classMapperTypeName, throwOnError: false);
        MethodInfo method = classMapperType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == nameof(IsLevelInConstellation)
                                 && m.ReturnType == typeof(bool)
                                 && m.GetParameters().Length == 1
                                 && m.GetParameters()[0].ParameterType == typeof(SelectableLevel));

        if (method == null) {
            return true;
        }

        return method.Invoke(null, new object[] { level }) is bool isInConstellation && isInConstellation;
    }
}
