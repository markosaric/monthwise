using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Monthwise.Domain.Common;

namespace Monthwise.Api.Endpoints;

/// <summary>
/// Refuses dates outside <see cref="SupportedRange"/> in any request
/// (docs/feature-requirements/month-range.md FR-2): <see cref="DateOnly"/> route and query values
/// and every <see cref="DateOnly"/> property of a request body, nested records and lists included.
/// Errors are keyed by the field's own name (<c>expectedDate</c>, <c>endDate</c>), as the domain
/// errors are, so the forms show them on the field.
/// It is added once to the <c>/api</c> group, so new endpoints and request types are covered
/// without listing them. Endpoints without dates get no filter at all.
/// </summary>
internal static class SupportedRangeFilter
{
    private static readonly ConcurrentDictionary<Type, DateMember[]> MembersByType = new();

    public static TBuilder WithSupportedRange<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var checks = factoryContext.MethodInfo.GetParameters()
                .Select((parameter, index) => (parameter, index))
                .Where(p => p.parameter.Name is not null && HasDates(p.parameter.ParameterType))
                .Select(p => (p.index, name: JsonNamingPolicy.CamelCase.ConvertName(p.parameter.Name!)))
                .ToArray();
            if (checks.Length == 0)
            {
                return next;
            }
            return invocationContext =>
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var (index, name) in checks)
                {
                    Collect(invocationContext.Arguments[index], name, errors);
                }
                return errors.Count == 0
                    ? next(invocationContext)
                    : ValueTask.FromResult<object?>(TypedResults.ValidationProblem(errors));
            };
        });

    private static void Collect(object? value, string field, Dictionary<string, string[]> errors)
    {
        switch (value)
        {
            case null:
                return;
            case DateOnly date:
                if (!SupportedRange.Contains(date))
                {
                    errors[field] = [SupportedRange.DateMessage];
                }
                return;
            case string:
                return;
            case IEnumerable items:
                foreach (var item in items)
                {
                    Collect(item, field, errors);
                }
                return;
        }
        foreach (var member in Members(value.GetType()))
        {
            Collect(member.Property.GetValue(value), member.Name, errors);
        }
    }

    /// <summary>
    /// The properties of a request type that hold dates, directly or inside nested records or lists.
    /// Built once per type.
    /// </summary>
    private static DateMember[] Members(Type type) =>
        MembersByType.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && HasDates(p.PropertyType))
            .Select(p => new DateMember(p, JsonNamingPolicy.CamelCase.ConvertName(p.Name)))
            .ToArray());

    private static bool HasDates(Type type) => HasDates(type, []);

    private static bool HasDates(Type type, HashSet<Type> seen)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(DateOnly))
        {
            return true;
        }
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal)
            || !IsRequestType(type) || !seen.Add(type))
        {
            return ElementType(type) is { } element && HasDates(element, seen);
        }
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetIndexParameters().Length == 0 && HasDates(p.PropertyType, seen));
    }

    /// <summary>Only the app's own request types are walked, not framework types such as HttpContext.</summary>
    private static bool IsRequestType(Type type) =>
        type.Namespace?.StartsWith("Monthwise.", StringComparison.Ordinal) == true && !type.IsAbstract;

    private static Type? ElementType(Type type) =>
        type == typeof(string)
            ? null
            : type.IsArray
                ? type.GetElementType()
                : type.GetInterfaces().Append(type)
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    ?.GetGenericArguments()[0];

    private sealed record DateMember(PropertyInfo Property, string Name);
}
