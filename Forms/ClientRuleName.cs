using System;

namespace FormsSandbox.Forms
{
    internal static class ClientRuleName
    {
        // Named public static methods link by name. Anonymous and instance
        // callbacks require an explicit browser registry key; their generated
        // CLR method names are not stable names for Transpose output.
        public static string From(Delegate method, string ruleId)
        {
            if (method == null)
                throw new InvalidOperationException("A custom form function is required.");
            if (ruleId != null)
            {
                if (string.IsNullOrWhiteSpace(ruleId))
                    throw new InvalidOperationException("An explicit RuleId cannot be blank.");
                return ruleId;
            }
            if (!method.Method.IsStatic || !method.Method.IsPublic ||
                method.Method.DeclaringType == null || !method.Method.DeclaringType.IsPublic ||
                Attribute.IsDefined(method.Method.DeclaringType,
                    typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)) ||
                Attribute.IsDefined(method.Method,
                    typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
                throw new InvalidOperationException(
                    "An anonymous or non-public custom function requires RuleId.");

            return method.Method.Name;
        }
    }
}