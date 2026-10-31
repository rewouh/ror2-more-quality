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
        // Walk every instruction, so protected members can be checked against the type using them.
        foreach (var type in AllTypes(module))
        {
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (!(instruction.Operand is MemberReference reference) || reference is TypeReference) continue;
                    if (reference.DeclaringType == null || reference.DeclaringType.Scope == module) continue;
                    IMemberDefinition target;
                    try
                    {
                        if (reference is FieldReference field) target = field.Resolve();
                        else if (reference is MethodReference called) target = called.Resolve();
                        else continue;
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
                    if (!Accessible(target, type)) problems.Add("not public: " + reference.FullName + "  (from " + type.FullName + ")");
                }
            }
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

    private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
    {
        var stack = new Stack<TypeDefinition>(module.Types);
        while (stack.Count > 0)
        {
            var type = stack.Pop();
            yield return type;
            foreach (var nested in type.NestedTypes) stack.Push(nested);
        }
    }

    // Protected members only count when the caller (or a type enclosing it) derives from their declaring type:
    // our Harmony patches are not subclasses, so a protected getter used there throws MethodAccessException.
    private static bool Accessible(IMemberDefinition member, TypeDefinition caller)
    {
        if (!TypeAccessible(member.DeclaringType)) return false;
        bool isPublic, isFamily;
        switch (member)
        {
            case FieldDefinition f: isPublic = f.IsPublic; isFamily = f.IsFamily || f.IsFamilyOrAssembly; break;
            case MethodDefinition m: isPublic = m.IsPublic; isFamily = m.IsFamily || m.IsFamilyOrAssembly; break;
            default: return true;
        }
        return isPublic || (isFamily && DerivesFrom(caller, member.DeclaringType));
    }

    private static bool DerivesFrom(TypeDefinition caller, TypeDefinition baseType)
    {
        for (var outer = caller; outer != null; outer = outer.DeclaringType)
        {
            for (var t = outer; t != null; )
            {
                if (t.FullName == baseType.FullName) return true;
                try { t = t.BaseType?.Resolve(); }
                catch (AssemblyResolutionException) { break; }
            }
        }
        return false;
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
