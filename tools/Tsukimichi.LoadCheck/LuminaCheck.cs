using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text;

namespace Tsukimichi.LoadCheck;

/// <summary>
/// Patch-day safety: Tsukimichi.GameData compiles against the Lumina and Lumina.Excel NuGet packages, while the game
/// runs it against the copies Dalamud ships (built from a newer sheet schema). A sheet column Dalamud's build renamed
/// (Quest.Unknown12 becoming a named column, say) compiles and tests green here and throws MissingMethodException the
/// first time the catalog is built in the game. This reads every type and member the plugin's assemblies reference in
/// Lumina and Lumina.Excel from their metadata, and finds each one by name and signature in Dalamud's copies.
/// </summary>
internal static class LuminaCheck
{
    private static readonly string[] LuminaAssemblies = ["Lumina", "Lumina.Excel"];

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>
    /// Checks <paramref name="assemblyPaths"/> against Lumina and Lumina.Excel from <paramref name="dalamud"/>; failures
    /// are added to <paramref name="failures"/>, one per missing type or member. Returns how many member references were checked.
    /// </summary>
    public static int Run(string dalamud, IReadOnlyList<string> assemblyPaths, List<string> failures)
    {
        var targets = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var name in LuminaAssemblies)
        {
            var path = Path.Combine(dalamud, name + ".dll");
            if (!File.Exists(path))
            {
                failures.Add($"lumina: Dalamud has no {name}.dll at {path}");
                return 0;
            }

            var loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            if (!string.Equals(Path.GetFullPath(loaded.Location), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"lumina: {name} is already loaded from {loaded.Location}, not Dalamud's {path}; the check would compare against the wrong copy");
                return 0;
            }

            targets[name] = loaded;
        }

        return assemblyPaths.Sum(path => CheckAssembly(path, targets, failures));
    }

    private static int CheckAssembly(string path, IReadOnlyDictionary<string, Assembly> targets, List<string> failures)
    {
        var file = Path.GetFileName(path);
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var names = new SignatureFormatter(md);

        foreach (var handle in md.AssemblyReferences)
        {
            var reference = md.GetAssemblyReference(handle);
            var name = md.GetString(reference.Name);
            if (targets.TryGetValue(name, out var target))
            {
                // The assembly versions stay at 7.0.0.0 across package releases; the file's product version tells them apart.
                var shipped = System.Diagnostics.FileVersionInfo.GetVersionInfo(target.Location).ProductVersion;
                Console.WriteLine($"  {file}: compiled against {name} {reference.Version}, Dalamud ships {target.GetName().Version} ({shipped})");
            }
        }

        var types = 0;
        foreach (var handle in md.TypeReferences)
        {
            if (TargetOf(md, handle, targets) is not { } target)
            {
                continue;
            }

            types++;
            var name = names.TypeReferenceName(handle);
            if (target.GetType(name, throwOnError: false) is null)
            {
                failures.Add($"lumina: {file} uses type {name}, which Dalamud's {target.GetName().Name}.dll does not have");
            }
        }

        var members = 0;
        foreach (var handle in md.MemberReferences)
        {
            var member = md.GetMemberReference(handle);
            if (DeclaringTypeReference(md, member.Parent) is not { } parent || TargetOf(md, parent, targets) is not { } target)
            {
                continue;
            }

            members++;
            var typeName = names.TypeReferenceName(parent);
            var name = md.GetString(member.Name);
            if (target.GetType(typeName, throwOnError: false) is not { } type)
            {
                // Already reported as a missing type.
                continue;
            }

            string wanted;
            bool found;
            if (member.GetKind() == MemberReferenceKind.Field)
            {
                var fieldType = member.DecodeFieldSignature(names, null);
                wanted = $"{typeName}.{name} : {fieldType}";
                found = type.GetFields(AllMembers).Any(f => f.Name == name && Format(f.FieldType) == fieldType);
            }
            else
            {
                var signature = member.DecodeMethodSignature(names, null);
                wanted = $"{typeName}.{name}{Describe(signature)}";
                found = FindMethod(type, name, signature);
            }

            if (!found)
            {
                failures.Add($"lumina: {file} uses {wanted}, which Dalamud's {target.GetName().Name}.dll does not have");
            }
        }

        Console.WriteLine($"  {file}: {types} type and {members} member references into Lumina checked");
        return members;
    }

    private static bool FindMethod(Type type, string name, MethodSignature<string> signature)
    {
        IEnumerable<MethodBase> candidates = name == ".ctor"
            ? type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            : type.GetMethods(AllMembers).Where(m => m.Name == name)
                .Concat(type.IsInterface ? type.GetInterfaces().SelectMany(i => i.GetMethods(AllMembers)).Where(m => m.Name == name) : []);

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var arity = candidate.IsGenericMethodDefinition ? candidate.GetGenericArguments().Length : 0;
            var returns = candidate is MethodInfo method ? Format(method.ReturnType) : "System.Void";
            if (candidate.IsStatic == signature.Header.IsInstance
                || arity != signature.GenericParameterCount
                || parameters.Length != signature.ParameterTypes.Length
                || returns != signature.ReturnType)
            {
                continue;
            }

            var same = true;
            for (var i = 0; i < parameters.Length && same; i++)
            {
                same = Format(parameters[i].ParameterType) == signature.ParameterTypes[i];
            }

            if (same)
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(MethodSignature<string> signature)
    {
        var generic = signature.GenericParameterCount > 0 ? $"``{signature.GenericParameterCount}" : string.Empty;
        return $"{generic}({string.Join(", ", signature.ParameterTypes)}) : {signature.ReturnType}";
    }

    /// <summary>The type reference a member reference's parent names: itself, or the generic type of an instantiation.</summary>
    private static TypeReferenceHandle? DeclaringTypeReference(MetadataReader md, EntityHandle parent)
    {
        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                return (TypeReferenceHandle)parent;
            case HandleKind.TypeSpecification:
                var blob = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
                if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
                {
                    // An array's Get/Set/Address: provided by the runtime; its element type is checked as a type reference.
                    return null;
                }

                blob.ReadCompressedInteger(); // CLASS or VALUETYPE
                var generic = blob.ReadTypeHandle();
                return generic.Kind == HandleKind.TypeReference ? (TypeReferenceHandle)generic : null;
            default:
                return null;
        }
    }

    /// <summary>The Lumina assembly a type reference resolves into (through its enclosing types), or null for any other.</summary>
    private static Assembly? TargetOf(MetadataReader md, TypeReferenceHandle handle, IReadOnlyDictionary<string, Assembly> targets)
    {
        var scope = md.GetTypeReference(handle).ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
        {
            scope = md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        }

        if (scope.Kind != HandleKind.AssemblyReference)
        {
            return null;
        }

        var name = md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
        return targets.TryGetValue(name, out var target) ? target : null;
    }

    /// <summary>
    /// A reflection type in the formatter's notation: namespace-qualified names with "+" for nesting, "!n" and "!!n"
    /// for type and method generic parameters, and generic arguments in angle brackets. Custom modifiers are ignored on
    /// both sides.
    /// </summary>
    internal static string Format(Type type)
    {
        if (type.IsGenericTypeParameter)
        {
            return "!" + type.GenericParameterPosition;
        }

        if (type.IsGenericMethodParameter)
        {
            return "!!" + type.GenericParameterPosition;
        }

        if (type.IsByRef)
        {
            return Format(type.GetElementType()!) + "&";
        }

        if (type.IsPointer)
        {
            return Format(type.GetElementType()!) + "*";
        }

        if (type.IsSZArray)
        {
            return Format(type.GetElementType()!) + "[]";
        }

        if (type.IsArray)
        {
            return Format(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        if (type.IsFunctionPointer)
        {
            return "method*";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            return DefinitionName(definition) + "<" + string.Join(",", type.GetGenericArguments().Select(Format)) + ">";
        }

        return DefinitionName(type);
    }

    private static string DefinitionName(Type type) =>
        type.IsNested ? DefinitionName(type.DeclaringType!) + "+" + type.Name : (string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name);

    /// <summary>Signatures decoded from metadata into the same notation as <see cref="Format"/>.</summary>
    private sealed class SignatureFormatter(MetadataReader md) : ISignatureTypeProvider<string, object?>
    {
        public string TypeReferenceName(TypeReferenceHandle handle)
        {
            var reference = md.GetTypeReference(handle);
            var name = md.GetString(reference.Name);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return TypeReferenceName((TypeReferenceHandle)reference.ResolutionScope) + "+" + name;
            }

            var ns = md.GetString(reference.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "System.Boolean",
            PrimitiveTypeCode.Byte => "System.Byte",
            PrimitiveTypeCode.SByte => "System.SByte",
            PrimitiveTypeCode.Char => "System.Char",
            PrimitiveTypeCode.Int16 => "System.Int16",
            PrimitiveTypeCode.UInt16 => "System.UInt16",
            PrimitiveTypeCode.Int32 => "System.Int32",
            PrimitiveTypeCode.UInt32 => "System.UInt32",
            PrimitiveTypeCode.Int64 => "System.Int64",
            PrimitiveTypeCode.UInt64 => "System.UInt64",
            PrimitiveTypeCode.Single => "System.Single",
            PrimitiveTypeCode.Double => "System.Double",
            PrimitiveTypeCode.IntPtr => "System.IntPtr",
            PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
            PrimitiveTypeCode.Object => "System.Object",
            PrimitiveTypeCode.String => "System.String",
            PrimitiveTypeCode.TypedReference => "System.TypedReference",
            PrimitiveTypeCode.Void => "System.Void",
            _ => typeCode.ToString(),
        };

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeReferenceName(handle);

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var definition = reader.GetTypeDefinition(handle);
            var name = reader.GetString(definition.Name);
            if (definition.GetDeclaringType() is { IsNil: false } declaring)
            {
                return GetTypeFromDefinition(reader, declaring, rawTypeKind) + "+" + name;
            }

            var ns = reader.GetString(definition.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";

        public string GetByReferenceType(string elementType) => elementType + "&";

        public string GetPointerType(string elementType) => elementType + "*";

        public string GetPinnedType(string elementType) => elementType;

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetFunctionPointerType(MethodSignature<string> signature) => "method*";

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            var text = new StringBuilder(genericType).Append('<');
            text.AppendJoin(',', typeArguments);
            return text.Append('>').ToString();
        }

        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;
    }
}
