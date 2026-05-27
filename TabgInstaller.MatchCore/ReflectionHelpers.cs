using System;
using System.Reflection;
using HarmonyLib;

namespace TabgInstaller.MatchCore
{
    internal static class ReflectionHelpers
    {
        public static FieldInfo Field(Type type, string name)
        {
            return AccessTools.Field(type, name);
        }

        public static MethodInfo Method(Type type, string name)
        {
            return AccessTools.Method(type, name);
        }

        public static T FieldValue<T>(object instance, Type type, string name) where T : class
        {
            return Field(type, name)?.GetValue(instance) as T;
        }
    }
}
