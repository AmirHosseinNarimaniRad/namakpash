using System.Diagnostics;
using System.Reflection;
using Xunit.Sdk;

namespace Splitt.Wasm.Spike;

/// <summary>
/// Runs the xUnit test methods compiled into this app, in the browser.
///
/// The desktop runner (Microsoft.NET.Test.Sdk + vstest) is a process host and does not exist
/// under WebAssembly, so discovery and invocation are done by reflection here. Only the parts
/// of xUnit that are plain managed code are used: the attributes and Assert.
/// </summary>
public static class TestRunner
{
    public record CaseResult(string Class, string Method, string? Arguments, bool Passed, string? Error, double Ms)
    {
        public string Display => Arguments is null ? Method : $"{Method}({Arguments})";
    }

    public static async Task<List<CaseResult>> RunAllAsync()
    {
        var results = new List<CaseResult>();

        var methods = typeof(TestRunner).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<FactAttribute>() is not null)   // TheoryAttribute derives from it
            .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name);

        foreach (var method in methods)
        {
            foreach (var args in ArgumentSets(method))
                results.Add(await RunOneAsync(method, args));
        }

        return results;
    }

    /// <summary>One entry for a [Fact]; one per [InlineData] for a [Theory].</summary>
    static IEnumerable<object?[]?> ArgumentSets(MethodInfo method)
    {
        var data = method.GetCustomAttributes<DataAttribute>().ToList();
        if (data.Count == 0)
        {
            yield return null;
            yield break;
        }

        foreach (var attribute in data)
            foreach (var row in attribute.GetData(method))
                yield return row;
    }

    static async Task<CaseResult> RunOneAsync(MethodInfo method, object?[]? args)
    {
        var type = method.DeclaringType!;
        var label = args is null ? null : string.Join(", ", args.Select(Format));
        var watch = Stopwatch.StartNew();

        try
        {
            var instance = Activator.CreateInstance(type);
            var returned = method.Invoke(instance, Coerce(method, args));
            if (returned is Task task) await task;

            watch.Stop();
            return new CaseResult(type.Name, method.Name, label, true, null, watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            watch.Stop();
            // Reflection wraps whatever the test threw; the inner exception is the real failure.
            var actual = ex is TargetInvocationException { InnerException: not null } t ? t.InnerException : ex;
            var error = $"{actual.GetType().Name}: {actual.Message}";
            return new CaseResult(type.Name, method.Name, label, false, error, watch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// [InlineData(100000, 4, 25000)] stores ints, but the test signature takes decimals — the
    /// real xUnit runner converts each argument to its parameter type before invoking, and
    /// reflection does not. Without this every [Theory] over decimal fails with ArgumentException.
    /// </summary>
    static object?[]? Coerce(MethodInfo method, object?[]? args)
    {
        if (args is null) return null;

        var parameters = method.GetParameters();
        var coerced = new object?[args.Length];

        for (var i = 0; i < args.Length; i++)
        {
            var value = args[i];
            if (i >= parameters.Length || value is null)
            {
                coerced[i] = value;
                continue;
            }

            var target = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
            coerced[i] = target.IsInstanceOfType(value)
                ? value
                : target.IsEnum
                    ? Enum.ToObject(target, value)
                    : Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
        }

        return coerced;
    }

    static string Format(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? value.ToString() ?? "?"
    };
}
