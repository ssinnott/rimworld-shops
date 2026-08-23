using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RefDump
{
    /// <summary>
    /// Answers "does this RimWorld type or member actually exist?" against the 1.6 reference
    /// assemblies, without launching the game.
    ///
    /// tools/validate_defs.py has always had a --refdump hook and there has never been a tool
    /// behind it, so every vanilla type named in XML has gone unverified and every vanilla API
    /// used from C# has been checked by memory. The reference assemblies are already on disk
    /// as a NuGet package the build restores; this reads their metadata.
    ///
    /// Metadata only: reference assemblies carry no IL and refuse to load for execution, so
    /// MetadataLoadContext is the only way in. That is enough to answer existence questions,
    /// which is the class of mistake worth catching statically.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Usage();
                return 2;
            }

            string refDir = Environment.GetEnvironmentVariable("RIMWORLD_REF_DIR") ?? FindRefDir();
            if (refDir == null)
            {
                Console.Error.WriteLine(
                    "refdump: could not find the RimWorld reference assemblies.\n" +
                    "Restore the project once (dotnet build Source/OldWestTown/OldWestTown.csproj),\n" +
                    "or set RIMWORLD_REF_DIR to the folder holding Assembly-CSharp.dll.");
                return 3;
            }

            string[] assemblies = Directory.GetFiles(refDir, "*.dll");
            var resolver = new PathAssemblyResolver(assemblies);
            using var mlc = new MetadataLoadContext(resolver, "mscorlib");

            var loaded = new List<Assembly>();
            foreach (string path in assemblies)
            {
                try { loaded.Add(mlc.LoadFromAssemblyPath(path)); }
                catch (BadImageFormatException) { /* native or resource-only, skip */ }
            }

            int worst = 0;
            foreach (string query in args)
            {
                worst = Math.Max(worst, Answer(query, loaded));
            }
            return worst;
        }

        /// <summary>
        /// A query is one of:
        ///   =Name          does a type called Name exist (short or fully-qualified)?
        ///   Type.Member    does that type have that field/property/method?
        ///   ~substring     list type names containing substring
        /// Exit code is 0 when every query resolved, 1 when any did not, so this drops
        /// straight into a shell conditional or a CI step.
        /// </summary>
        private static int Answer(string query, List<Assembly> loaded)
        {
            if (query.StartsWith("~"))
            {
                string needle = query.Substring(1);
                var hits = AllTypes(loaded)
                    .Where(t => t.FullName != null &&
                                t.FullName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(t => t.FullName)
                    .OrderBy(n => n)
                    .Take(60)
                    .ToList();
                foreach (string h in hits) Console.WriteLine(h);
                if (hits.Count == 0) Console.WriteLine("(no type name contains '" + needle + "')");
                return hits.Count == 0 ? 1 : 0;
            }

            if (query.StartsWith("="))
            {
                string name = query.Substring(1);
                Type t = Resolve(name, loaded);
                Console.WriteLine(t != null
                    ? "OK   type " + t.FullName
                    : "MISS type " + name);
                return t == null ? 1 : 0;
            }

            int dot = query.LastIndexOf('.');
            if (dot <= 0)
            {
                Type bare = Resolve(query, loaded);
                Console.WriteLine(bare != null ? "OK   type " + bare.FullName : "MISS type " + query);
                return bare == null ? 1 : 0;
            }

            string typeName = query.Substring(0, dot);
            string member = query.Substring(dot + 1);
            Type owner = Resolve(typeName, loaded);
            if (owner == null)
            {
                Console.WriteLine("MISS type " + typeName + " (so cannot check ." + member + ")");
                return 1;
            }

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Instance | BindingFlags.Static
                                     | BindingFlags.FlattenHierarchy;

            var found = new List<string>();
            for (Type t = owner; t != null; t = BaseOf(t))
            {
                foreach (var f in t.GetFields(Flags).Where(f => f.Name == member))
                    found.Add("field    " + Pretty(f.FieldType) + " " + t.Name + "." + f.Name);
                foreach (var p in t.GetProperties(Flags).Where(p => p.Name == member))
                    found.Add("property " + Pretty(p.PropertyType) + " " + t.Name + "." + p.Name);
                foreach (var m in t.GetMethods(Flags).Where(m => m.Name == member))
                    found.Add("method   " + Pretty(m.ReturnType) + " " + t.Name + "." + m.Name
                              + "(" + string.Join(", ", m.GetParameters().Select(Describe)) + ")");
            }

            if (found.Count == 0)
            {
                Console.WriteLine("MISS " + owner.FullName + "." + member);
                return 1;
            }
            foreach (string line in found.Distinct()) Console.WriteLine("OK   " + line);
            return 0;
        }

        private static string Describe(ParameterInfo p)
        {
            string s = Pretty(p.ParameterType) + " " + p.Name;
            return p.IsOptional ? s + " = ..." : s;
        }

        /// <summary>Base types resolve across the load context; a broken link ends the walk
        /// rather than throwing, since a partial answer still beats no answer.</summary>
        private static Type BaseOf(Type t)
        {
            try { return t.BaseType; }
            catch { return null; }
        }

        private static string Pretty(Type t)
        {
            if (t == null) return "?";
            if (!t.IsGenericType) return t.Name;
            string bare = t.Name.Contains('`') ? t.Name.Substring(0, t.Name.IndexOf('`')) : t.Name;
            return bare + "<" + string.Join(", ", t.GetGenericArguments().Select(Pretty)) + ">";
        }

        private static IEnumerable<Type> AllTypes(List<Assembly> loaded)
        {
            foreach (Assembly a in loaded)
            {
                Type[] types;
                try { types = a.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (Type t in types) yield return t;
            }
        }

        /// <summary>Accepts a fully-qualified name or a bare one. RimWorld's own types live in
        /// a handful of namespaces and XML names them without one, which is why a short-name
        /// lookup has to work at all.</summary>
        private static Type Resolve(string name, List<Assembly> loaded)
        {
            Type exact = AllTypes(loaded).FirstOrDefault(t => t.FullName == name);
            if (exact != null) return exact;
            var byShort = AllTypes(loaded).Where(t => t.Name == name).ToList();
            return byShort.Count > 0 ? byShort[0] : null;
        }

        /// <summary>The reference assemblies arrive as a restored NuGet package, so the build
        /// having run once is the only setup this needs.</summary>
        private static string FindRefDir()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string root = Path.Combine(home, ".nuget", "packages", "krafs.rimworld.ref");
            if (!Directory.Exists(root)) return null;

            return Directory.GetDirectories(root)
                .OrderByDescending(d => d, StringComparer.Ordinal)
                .Select(d => Path.Combine(d, "ref", "net472"))
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "Assembly-CSharp.dll")));
        }

        private static void Usage()
        {
            Console.Error.WriteLine(
                "usage: refdump QUERY...\n" +
                "  =Name           does a type exist (short or fully-qualified name)\n" +
                "  Type.Member     does that type have that field/property/method\n" +
                "  ~substring      list type names containing substring\n" +
                "Exit code 0 if every query resolved, 1 if any did not.");
        }
    }
}
