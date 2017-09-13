namespace SeeJit.Compilation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;

    public class CompilationException : ApplicationException
    {
        private static string ToErrorMessage(IEnumerable<Diagnostic> diagnostics)
        {
            var failures = diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

            var sb = new StringBuilder();

            foreach (var f in failures)
            {
                sb.AppendLine($"{f.Severity} {f.Id}: {f.GetMessage()}");
            }

            return sb.ToString();
        }

        public CompilationException(IEnumerable<Diagnostic> diagnostics)
            : base(ToErrorMessage(diagnostics)) { }
    }
}
