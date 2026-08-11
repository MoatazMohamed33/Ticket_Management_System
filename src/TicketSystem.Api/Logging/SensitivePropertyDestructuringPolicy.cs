using Serilog.Core;
using Serilog.Events;

namespace TicketSystem.Api.Logging;

/// <summary>
/// Serilog destructuring policy that redacts common credential-bearing property names.
/// Prevents accidental password/token leakage into structured logs (NFR-S10).
/// </summary>
public sealed class SensitivePropertyDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordHash", "token", "accessToken", "refreshToken", "authorization"
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        if (value is null)
        {
            result = new ScalarValue(null);
            return false;
        }

        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
        {
            result = new ScalarValue(value);
            return false;
        }

        var properties = type.GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p =>
            {
                var v = Sensitive.Contains(p.Name)
                    ? new ScalarValue("***REDACTED***")
                    : propertyValueFactory.CreatePropertyValue(SafeGet(p, value), destructureObjects: true);
                return new LogEventProperty(p.Name, v);
            })
            .ToArray();

        result = new StructureValue(properties);
        return true;
    }

    private static object? SafeGet(System.Reflection.PropertyInfo p, object target)
    {
        try { return p.GetValue(target); }
        catch { return "<unreadable>"; }
    }
}
