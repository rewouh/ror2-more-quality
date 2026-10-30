// The NuGet game references are publicized: private game members look public, so the compiler can
// silently bind to one (e.g. CharacterBody's private "transform" field instead of Component.transform),
// and the game then throws FieldAccessException / MethodAccessException at runtime.
//
// This checks the built DLL against the REAL assemblies (game folder + r2modman profile) and lists
// every member it uses that isn't actually accessible.
//
// Usage: dotnet run --project tools/AccessCheck -- <MoreQuality.dll> <assembly folder>...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: AccessCheck <assembly> <search folder>...");
            return 2;
        }

        var resolver = new DefaultAssemblyResolver();
        foreach (var folder in args.Skip(1).Where(Directory.Exists))
        {
            resolver.AddSearchDirectory(folder);
            foreach (var sub in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories)) resolver.AddSearchDirectory(sub);
        }

        var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });
        var problems = new SortedSet<string>();
        foreach (var reference in module.GetMemberReferences())
        {
            if (reference.DeclaringType == null || reference.DeclaringType.Scope == module) continue;
            IMemberDefinition target;
            try
            {
                target = reference is FieldReference field ? field.Resolve() : (IMemberDefinition)((MethodReference)reference).Resolve();
            }
            catch (AssemblyResolutionException)
            {
                continue;   // e.g. framework facades: not game code
            }
            if (target == null)
            {
                problems.Add("missing:    " + reference.FullName);
                continue;
            }
            if (!Accessible(target)) problems.Add("not public: " + reference.FullName);
        }
        foreach (var type in module.GetTypeReferences())
        {
            TypeDefinition target;
            try { target = type.Resolve(); }
            catch (AssemblyResolutionException) { continue; }
            if (target != null && !TypeAccessible(target)) problems.Add("not public: " + type.FullName);
        }

        foreach (var problem in problems) Console.WriteLine(problem);
        Console.WriteLine(problems.Count == 0 ? "AccessCheck: OK" : "AccessCheck: " + problems.Count + " problem(s)");
        return problems.Count == 0 ? 0 : 1;
    }

    // Protected members count as accessible: our Harmony patches and subclasses may use them legitimately
    // only through reflection, but a protected access from a subclass is still valid IL.
    private static bool Accessible(IMemberDefinition member)
    {
        if (!TypeAccessible(member.DeclaringType)) return false;
        switch (member)
        {
            case FieldDefinition f: return f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly;
            case MethodDefinition m: return m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;
            default: return true;
        }
    }

    private static bool TypeAccessible(TypeDefinition type)
    {
        for (var t = type; t != null; t = t.DeclaringType)
        {
            if (t.IsNested ? !(t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly) : !t.IsPublic) return false;
        }
        return true;
    }
}
