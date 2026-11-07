using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace HoneyDrunk.Memory.Abstractions.Tests.ContractShape;

/// <summary>
/// Renders the public shape of a set of root types and of every public type from the same assembly
/// that appears in their signatures. Types are sorted by name; each type line is followed by its
/// member lines, sorted. Any change to a signature, a nullability annotation, a default value or an
/// enum number changes the text.
/// </summary>
internal static class ContractShapeRenderer
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>Renders the shape.</summary>
    /// <param name="roots">The root types; all from one assembly.</param>
    /// <returns>The sorted shape lines, newline-terminated.</returns>
    public static string Render(IReadOnlyList<Type> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            throw new ArgumentException("At least one root is required.", nameof(roots));
        }

        var assembly = roots[0].Assembly;
        var closure = new SortedDictionary<string, Type>(StringComparer.Ordinal);
        var pending = new Queue<Type>(roots);
        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (!IsContractType(type, assembly) || !closure.TryAdd(Name(type), type))
            {
                continue;
            }

            foreach (var referenced in Referenced(type))
            {
                pending.Enqueue(referenced);
            }
        }

        var context = new NullabilityInfoContext();
        var builder = new StringBuilder();
        foreach (var type in closure.Values)
        {
            builder.Append(TypeLine(type)).Append('\n');
            foreach (var line in MemberLines(type, context).Order(StringComparer.Ordinal))
            {
                builder.Append(line).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static bool IsContractType(Type type, Assembly assembly) =>
        type.Assembly == assembly && !type.IsGenericParameter && (type.IsPublic || type.IsNestedPublic);

    private static bool IsVisible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool IsVisible(FieldInfo field) => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool IsGenerated(MemberInfo member) => member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    private static IEnumerable<Type> Referenced(Type type)
    {
        var direct = new List<Type>();
        if (type.BaseType is not null)
        {
            direct.Add(type.BaseType);
        }

        direct.AddRange(type.GetInterfaces());
        foreach (var constructor in type.GetConstructors(Declared).Where(IsVisible))
        {
            direct.AddRange(constructor.GetParameters().Select(parameter => parameter.ParameterType));
        }

        foreach (var method in type.GetMethods(Declared).Where(IsVisible))
        {
            direct.Add(method.ReturnType);
            direct.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
        }

        foreach (var property in type.GetProperties(Declared))
        {
            direct.Add(property.PropertyType);
        }

        foreach (var field in type.GetFields(Declared).Where(IsVisible))
        {
            direct.Add(field.FieldType);
        }

        direct.AddRange(type.GetEvents(Declared).Select(eventInfo => eventInfo.EventHandlerType).OfType<Type>());

        return direct.SelectMany(Unwrap);
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.HasElementType)
        {
            foreach (var inner in Unwrap(type.GetElementType()!))
            {
                yield return inner;
            }

            yield break;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var inner in Unwrap(argument))
            {
                yield return inner;
            }
        }
    }

    private static string TypeLine(Type type)
    {
        string kind;
        if (type.IsInterface)
        {
            kind = "interface";
        }
        else if (type.IsEnum)
        {
            kind = "enum " + Name(Enum.GetUnderlyingType(type));
        }
        else if (typeof(Delegate).IsAssignableFrom(type))
        {
            kind = "delegate";
        }
        else if (type.IsValueType)
        {
            kind = type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false) ? "readonly struct" : "struct";
        }
        else
        {
            kind = (isAbstract: type.IsAbstract, isSealed: type.IsSealed) switch
            {
                (true, true) => "static class",
                (true, false) => "abstract class",
                (false, true) => "sealed class",
                _ => "class",
            };
        }

        var builder = new StringBuilder("type ").Append(Name(type)).Append(": ").Append(kind);
        if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType) && type.BaseType != typeof(Enum))
        {
            builder.Append(" : ").Append(TypeName(type.BaseType, null));
        }

        var interfaces = type.GetInterfaces().Select(contract => TypeName(contract, null)).Order(StringComparer.Ordinal).ToArray();
        if (interfaces.Length > 0)
        {
            builder.Append(" implements ").Append(string.Join(", ", interfaces));
        }

        return builder.ToString();
    }

    private static IEnumerable<string> MemberLines(Type type, NullabilityInfoContext context)
    {
        var prefix = "  " + Name(type) + " ";
        if (type.IsEnum)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                yield return prefix + "value " + field.Name + " = " + Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture);
            }

            yield break;
        }

        foreach (var constructor in type.GetConstructors(Declared).Where(IsVisible).Where(constructor => !IsGenerated(constructor)))
        {
            yield return prefix + Access(constructor) + "ctor(" + Parameters(constructor, context) + ")";
        }

        foreach (var method in type.GetMethods(Declared).Where(IsVisible).Where(method => !IsGenerated(method)))
        {
            if (method.IsSpecialName && !method.Name.StartsWith("op_", StringComparison.Ordinal))
            {
                continue;
            }

            var generic = method.IsGenericMethodDefinition
                ? "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">"
                : string.Empty;
            yield return prefix + Access(method) + Modifiers(method) + "method "
                + TypeName(method.ReturnType, context.Create(method.ReturnParameter)) + " "
                + method.Name + generic + "(" + Parameters(method, context) + ")";
        }

        foreach (var property in type.GetProperties(Declared).Where(property => !IsGenerated(property)))
        {
            var getter = property.GetMethod is { } get && IsVisible(get) ? get : null;
            var setter = property.SetMethod is { } set && IsVisible(set) ? set : null;
            if (getter is null && setter is null)
            {
                continue;
            }

            var accessors = new List<string>();
            if (getter is not null)
            {
                accessors.Add("get;");
            }

            if (setter is not null)
            {
                var isInit = setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
                accessors.Add(isInit ? "init;" : "set;");
            }

            var indexers = property.GetIndexParameters();
            var index = indexers.Length == 0
                ? string.Empty
                : "[" + string.Join(", ", indexers.Select(parameter => Parameter(parameter, context))) + "]";
            yield return prefix + Access((getter ?? setter)!) + Modifiers((getter ?? setter)!) + "property "
                + TypeName(property.PropertyType, context.Create(property)) + " " + property.Name + index
                + " { " + string.Join(" ", accessors) + " }";
        }

        foreach (var field in type.GetFields(Declared).Where(IsVisible).Where(field => !IsGenerated(field)))
        {
            var modifiers = field.IsLiteral ? "const " : (field.IsStatic ? "static " : string.Empty) + (field.IsInitOnly ? "readonly " : string.Empty);
            var value = field.IsLiteral ? " = " + Value(field.GetRawConstantValue(), field.FieldType) : string.Empty;
            yield return prefix + (field.IsPublic ? "public " : "protected ") + modifiers + "field "
                + TypeName(field.FieldType, context.Create(field)) + " " + field.Name + value;
        }

        foreach (var eventInfo in type.GetEvents(Declared).Where(eventInfo => !IsGenerated(eventInfo)))
        {
            yield return prefix + "event " + TypeName(eventInfo.EventHandlerType!, context.Create(eventInfo)) + " " + eventInfo.Name;
        }
    }

    private static string Access(MethodBase method) => method.IsPublic ? "public " : "protected ";

    private static string Modifiers(MethodInfo method)
    {
        if (method.IsStatic)
        {
            return "static ";
        }

        if (method.DeclaringType!.IsInterface)
        {
            return method.IsAbstract ? string.Empty : "default ";
        }

        if (method.IsAbstract)
        {
            return "abstract ";
        }

        if (method.IsVirtual && !method.IsFinal)
        {
            return method.GetBaseDefinition() == method ? "virtual " : "override ";
        }

        return method.IsVirtual && method.GetBaseDefinition() != method ? "sealed override " : string.Empty;
    }

    private static string Parameters(MethodBase method, NullabilityInfoContext context) =>
        string.Join(", ", method.GetParameters().Select(parameter => Parameter(parameter, context)));

    private static string Parameter(ParameterInfo parameter, NullabilityInfoContext context)
    {
        var type = parameter.ParameterType;
        var modifier = string.Empty;
        if (type.IsByRef)
        {
            modifier = parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
            type = type.GetElementType()!;
        }
        else if (parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false))
        {
            modifier = "params ";
        }

        var text = modifier + TypeName(type, context.Create(parameter)) + " " + parameter.Name;
        return parameter.HasDefaultValue ? text + " = " + Value(parameter.DefaultValue, type) : text;
    }

    private static string Value(object? value, Type type)
    {
        if (value is null)
        {
            return type.IsValueType && Nullable.GetUnderlyingType(type) is null ? "default" : "null";
        }

        return value switch
        {
            string text => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
            bool flag => flag ? "true" : "false",
            char character => "'" + character + "'",
            Enum member => Convert.ToString(Convert.ToInt64(member, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)!,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string TypeName(Type type, NullabilityInfo? nullability)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return TypeName(underlying, nullability?.GenericTypeArguments.FirstOrDefault()) + "?";
        }

        var nullable = !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? "?" : string.Empty;
        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!, nullability?.ElementType) + "[]" + nullable;
        }

        if (type.IsGenericParameter)
        {
            return type.Name + nullable;
        }

        if (!type.IsGenericType)
        {
            return Name(type) + nullable;
        }

        var arguments = type.GetGenericArguments();
        var argumentNames = arguments.Select((argument, index) =>
            TypeName(argument, nullability is not null && index < nullability.GenericTypeArguments.Length ? nullability.GenericTypeArguments[index] : null));
        return Name(type) + "<" + string.Join(", ", argumentNames) + ">" + nullable;
    }

    private static string Name(Type type)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        var name = definition.FullName ?? definition.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }
}
