namespace SeeJit.Reflection
{
    using System.Reflection;
    using System.Runtime.CompilerServices;

    internal static class MemberHelper
    {
        public static bool IsCompilerGenerated(this MemberInfo member)
        {
            return member.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
        }
    }
}
