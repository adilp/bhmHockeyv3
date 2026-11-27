using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BHMHockey.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace BHMHockey.Api.Tests.Contract;

/// <summary>
/// The phones people already have installed are a FIXED client: the 1.0.5 store
/// build's embedded bundle and whatever over-the-air bundle has since reached
/// the production channel. Neither can be changed by a deploy, so no deploy may
/// change anything they call.
///
/// This test pins every HTTP endpoint the API exposes - verb, route template,
/// authorization attributes, parameter bindings, and the full JSON shape of the
/// request body and the declared response (property names as serialized,
/// camelCase, with types and nullability, recursively) - against
/// <c>installed_client_contract.txt</c>, a snapshot generated from the code as it
/// stood at 60977b7, before game results existed.
///
/// Rules it enforces:
///  1. Every line of the baseline is still produced, byte for byte. Removing,
///     renaming, retyping or re-nullabling anything an existing endpoint takes
///     or returns fails here.
///  2. Every endpoint that is NOT in the baseline is listed in
///     <c>additive_endpoints.txt</c>, so a new route is always a conscious,
///     reviewed addition (and can never silently shadow an old one).
///
/// Regenerate the additive list (never the baseline) with
/// <c>UPDATE_ADDITIVE_ENDPOINTS=1 dotnet test --filter InstalledClientContract</c>.
/// The baseline is regenerated only on a commit that ALSO ships a store build and
/// retires the old client - see docs/GAME_RESULTS_COMPATIBILITY.md.
/// </summary>
public class InstalledClientContractTests
{
    private const string BaselineFile = "installed_client_contract.txt";
    private const string AdditiveFile = "additive_endpoints.txt";

    private static string Dir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    [Fact]
    public void EveryEndpointOfTheInstalledClientKeepsItsExactShape()
    {
        var current = ContractDescriber.DescribeAll();
        var baselinePath = Path.Combine(Dir(), BaselineFile);

        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACT_BASELINE") == "1")
        {
            File.WriteAllLines(baselinePath, current);
        }

        File.Exists(baselinePath).Should().BeTrue("the baseline snapshot must be committed");
        var baseline = File.ReadAllLines(baselinePath).Where(l => l.Length > 0).ToList();
        baseline.Should().NotBeEmpty();

        var missing = baseline.Except(current).ToList();
        missing.Should().BeEmpty(
            "every endpoint the installed app may call must keep its route, auth, request and response shape exactly;" +
            " changed or removed:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryEndpointAddedSinceTheBaselineIsListedAsAdditive()
    {
        var current = ContractDescriber.DescribeAll();
        var baseline = File.ReadAllLines(Path.Combine(Dir(), BaselineFile)).Where(l => l.Length > 0).ToHashSet();
        var added = current.Where(l => !baseline.Contains(l)).ToList();

        var additivePath = Path.Combine(Dir(), AdditiveFile);
        if (Environment.GetEnvironmentVariable("UPDATE_ADDITIVE_ENDPOINTS") == "1")
        {
            File.WriteAllLines(additivePath, added);
        }

        var listed = File.Exists(additivePath)
            ? File.ReadAllLines(additivePath).Where(l => l.Length > 0).ToList()
            : new List<string>();

        added.Should().BeEquivalentTo(listed,
            "a new endpoint must be reviewed as an addition (additive_endpoints.txt) before it ships");
    }

    [Fact]
    public void NoAddedEndpointSharesARouteWithABaselineEndpoint()
    {
        // A new action on an old verb+route would make ASP.NET throw
        // AmbiguousMatchException at request time - a crash of the old client.
        var baselineRoutes = File.ReadAllLines(Path.Combine(Dir(), BaselineFile))
            .Where(l => l.Length > 0).Select(RouteOf).ToHashSet();
        var current = ContractDescriber.DescribeAll();
        var addedRoutes = current.Select(RouteOf).Where(r => !baselineRoutes.Contains(r));
        var all = current.Select(RouteOf).ToList();

        all.Should().OnlyHaveUniqueItems("two actions on one verb+route are ambiguous");
        addedRoutes.Should().NotIntersectWith(baselineRoutes);
    }

    // Routing is case-insensitive and a parameter matches whatever its name or
    // constraint, so compare "VERB /api/x/{}/y" lowercased.
    private static string RouteOf(string line) =>
        System.Text.RegularExpressions.Regex.Replace(line.Split(" | ")[0], @"\{[^}]*\}", "{}").ToLowerInvariant();
}

/// <summary>Reflection over every controller action, rendered one line per endpoint.</summary>
internal static class ContractDescriber
{
    private static readonly NullabilityInfoContext Nullability = new();

    public static List<string> DescribeAll()
    {
        var asm = typeof(EventsController).Assembly;
        var lines = new List<string>();
        foreach (var controller in asm.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var classRoute = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            classRoute = classRoute.Replace("[controller]",
                controller.Name.EndsWith("Controller") ? controller.Name[..^"Controller".Length] : controller.Name);
            var classAuth = AuthOf(controller);

            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(m => m.Name, StringComparer.Ordinal)
                         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
            {
                foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>())
                {
                    foreach (var verb in http.HttpMethods)
                    {
                        var template = http.Template ?? "";
                        var route = template.StartsWith("/") || template.StartsWith("~/")
                            ? template.TrimStart('~')
                            : "/" + string.Join("/", new[] { classRoute, template }.Where(s => s.Length > 0));
                        var auth = string.Join(",", new[] { classAuth, AuthOf(method) }.Where(s => s.Length > 0));
                        var pars = string.Join(", ", method.GetParameters().Select(DescribeParameter));
                        var ret = DescribeReturn(method);
                        lines.Add($"{verb} {route} | auth[{auth}] | params({pars}) | returns {ret}");
                    }
                }
            }
        }
        return lines;
    }

    private static string AuthOf(MemberInfo member)
    {
        var parts = new List<string>();
        foreach (var a in member.GetCustomAttributes<AuthorizeAttribute>(inherit: false))
        {
            parts.Add("Authorize" + (a.Roles != null ? $"(Roles={a.Roles})" : "") + (a.Policy != null ? $"(Policy={a.Policy})" : ""));
        }
        if (member.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) != null) parts.Add("AllowAnonymous");
        return string.Join(",", parts);
    }

    private static string DescribeParameter(ParameterInfo p)
    {
        string source =
            p.GetCustomAttribute<FromBodyAttribute>() != null ? "body" :
            p.GetCustomAttribute<FromQueryAttribute>() != null ? "query" :
            p.GetCustomAttribute<FromRouteAttribute>() != null ? "route" :
            p.GetCustomAttribute<FromHeaderAttribute>() != null ? "header" :
            p.ParameterType == typeof(CancellationToken) ? "cancel" : "auto";
        var def = p.HasDefaultValue ? $"={JsonSerializer.Serialize(p.DefaultValue)}" : "";
        return $"{source}:{p.Name}:{Shape(p.ParameterType, NullableOf(p), new HashSet<Type>())}{def}";
    }

    private static string DescribeReturn(MethodInfo m)
    {
        var t = m.ReturnType;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>)) t = t.GetGenericArguments()[0];
        else if (t == typeof(Task)) return "void";
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return Shape(t.GetGenericArguments()[0], false, new HashSet<Type>());
        }
        return t.Name; // IActionResult / ActionResult: untyped, body decided in the method
    }

    private static bool NullableOf(ParameterInfo p)
    {
        if (Nullable.GetUnderlyingType(p.ParameterType) != null) return true;
        if (p.ParameterType.IsValueType) return false;
        return Nullability.Create(p).ReadState == NullabilityState.Nullable;
    }

    private static bool NullableOf(PropertyInfo p)
    {
        if (Nullable.GetUnderlyingType(p.PropertyType) != null) return true;
        if (p.PropertyType.IsValueType) return false;
        return Nullability.Create(p).ReadState == NullabilityState.Nullable;
    }

    private static string Shape(Type t, bool nullable, HashSet<Type> seen)
    {
        var q = nullable ? "?" : "";
        var under = Nullable.GetUnderlyingType(t);
        if (under != null) { t = under; q = "?"; }

        if (t.IsEnum) return $"enum<{t.Name}:number>{q}";
        if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(Guid)
            || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) || t == typeof(object))
            return t.Name + q;
        if (t == typeof(JsonElement)) return "json" + q;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)
            || t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            || t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
        {
            var a = t.GetGenericArguments();
            return $"{{{Shape(a[0], false, seen)}:{Shape(a[1], false, seen)}}}{q}";
        }
        if (t.IsArray) return $"[{Shape(t.GetElementType()!, false, seen)}]{q}";
        if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
            return $"[{Shape(t.GetGenericArguments()[0], false, seen)}]{q}";

        if (seen.Contains(t)) return $"<{t.Name}>{q}";
        var next = new HashSet<Type>(seen) { t };
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod != null)
            .OrderBy(p => p.MetadataToken);
        var sb = new StringBuilder();
        sb.Append(t.Name).Append('{');
        sb.Append(string.Join(",", props.Select(p =>
            $"{JsonNamingPolicy.CamelCase.ConvertName(p.Name)}:{Shape(p.PropertyType, NullableOf(p), next)}")));
        sb.Append('}').Append(q);
        return sb.ToString();
    }
}
