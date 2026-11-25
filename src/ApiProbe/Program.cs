using System.Reflection;

class Program
{
    static void Main()
    {
        Dump(typeof(SoulsFormats.FLVER2));
        Dump(typeof(SoulsFormats.FLVER2.Material));
        Dump(typeof(SoulsFormats.FLVER2.Texture));
        Dump(typeof(SoulsFormats.FLVER2.Mesh));
        Dump(typeof(SoulsFormats.FLVER2.FaceSet));
        Dump(typeof(SoulsFormats.FLVER2.SkeletonSet));
        Dump(typeof(SoulsFormats.FLVER2.SkeletonSet.Bone));
        Dump(typeof(SoulsFormats.FLVER.Node));
        Dump(typeof(SoulsFormats.FLVER.Dummy));
        Dump(typeof(SoulsFormats.FLVER2.FLVERHeader));
    }

    static void Dump(Type t)
    {
        Console.WriteLine($"===== {t.FullName} =====");
        var bf = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(bf).OrderBy(p => p.Name))
            Console.WriteLine($"  PROP  {p.PropertyType.Name,-32} {p.Name}  {(p.CanWrite ? "rw" : "ro")}");

        foreach (var f in t.GetFields(bf).OrderBy(f => f.Name))
            Console.WriteLine($"  FIELD {f.FieldType.Name,-32} {f.Name}");

        foreach (var nt in t.GetNestedTypes(BindingFlags.Public).OrderBy(n => n.Name))
            Console.WriteLine($"  NESTED TYPE {nt.Name}");

        foreach (var m in t.GetMethods(bf).Where(m => !m.IsSpecialName).OrderBy(m => m.Name))
            Console.WriteLine($"  METHOD {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))})");

        Console.WriteLine();
    }
}