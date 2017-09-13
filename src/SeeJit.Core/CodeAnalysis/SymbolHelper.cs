namespace SeeJit.CodeAnalysis
{
    using System;
    using System.Reflection;
    using Microsoft.CodeAnalysis;

    internal static class SymbolHelper
    {
        public static bool IsSame(this IMethodSymbol methodSymbol, MethodBase method)
        {
            if (methodSymbol.Name != method.Name)
                return false;

            if (method.IsStatic != methodSymbol.IsStatic)
                return false;

            if (method.IsGenericMethod != methodSymbol.IsGenericMethod)
                return false;

            var parameters = method.GetParameters();
            var parameterSymbols = methodSymbol.Parameters;

            if (parameters.Length != parameterSymbols.Length)
                return false;

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var s = parameterSymbols[i];

                if (p.Name != s.Name)
                    return false;

                if (!s.Type.IsSame(p.ParameterType))
                    return false;
            }

            return true;
        }

        public static bool IsSame(this ITypeSymbol symbol, Type type)
        {
            if (type.Name != symbol.MetadataName)
                return false;

            // This is obviously flawed. Missing namespace and generic parameters comparsion.

            return true;
        }
    }
}
