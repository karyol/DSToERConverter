using SoulsFormats;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        { Console.WriteLine("usage: FlverDump <file.flver>"); return 1; }
        var f = FLVER2.Read(args[0]);
        var h = f.Header;

        Console.WriteLine($"FILE     : {args[0]}");
        Console.WriteLine($"VERSION  : 0x{h.Version:X}");
        Console.WriteLine($"UNICODE  : {h.Unicode}   BIGENDIAN: {h.BigEndian}");
        Console.WriteLine($"BBOX     : {h.BoundingBoxMin} .. {h.BoundingBoxMax}");
        Console.WriteLine($"UNK68    : {h.Unk68}   SPECMOD: {h.SpecialModifier}   SPEEDTREE: {f.IsSpeedtree()}");
        Console.WriteLine();

        Console.WriteLine($"== NODES ({f.Nodes.Count}) ==");
        for (int i = 0; i < f.Nodes.Count; i++)
        {
            var n = f.Nodes[i];
            Console.WriteLine($"[{i,3}] flags={(int)n.Flags,-4} ({n.Flags}) parent={n.ParentIndex} child={n.FirstChildIndex} next={n.NextSiblingIndex} prev={n.PreviousSiblingIndex}  {n.Name}");
        }
        Console.WriteLine();

        Console.WriteLine($"== DUMMIES ({f.Dummies.Count}) ==");
        for (int i = 0; i < f.Dummies.Count; i++)
        {
            var d = f.Dummies[i];
            Console.WriteLine($"[{i,3}] ref={d.ReferenceID} parent={d.ParentBoneIndex} attach={d.AttachBoneIndex} usesAttach={d.UsesAttachBoneIndex} up={d.UseUpwardVector} hash=0x{d.NameHash:X8}");
        }
        Console.WriteLine();

        Console.WriteLine($"== MATERIALS ({f.Materials.Count})  GXLists={f.GXLists.Count} ==");
        for (int i = 0; i < f.Materials.Count; i++)
        {
            var m = f.Materials[i];
            Console.WriteLine($"[{i,3}] name={m.Name}");
            Console.WriteLine($"       MTD/MATBIN={m.MTD}   matbinIdx={m.Index}   gxIdx={m.GXIndex}");
            foreach (var t in m.Textures)
                Console.WriteLine($"       tex {t.ParamName} = {t.Path}  (U={t.TilingTypeU} V={t.TilingTypeV} scale={t.TilingScale})");
        }
        Console.WriteLine();

        Console.WriteLine($"== MESHES ({f.Meshes.Count}) ==");
        for (int i = 0; i < f.Meshes.Count; i++)
        {
            var m = f.Meshes[i];
            Console.WriteLine($"[{i,3}] node={m.NodeIndex} mat={m.MaterialIndex} dyn={m.Dynamic} boneW={m.UseBoneWeights} verts={m.Vertices.Count} facesets={m.FaceSets.Count} vbufs={m.VertexBuffers.Count}");
            foreach (var fs in m.FaceSets)
                Console.WriteLine($"       faceset flags={fs.Flags} ({(int)fs.Flags}) idx={fs.Indices.Count} cull={fs.CullBackfaces} strip={fs.TriangleStrip} unk06={fs.Unk06}");
        }
        Console.WriteLine();

        Console.WriteLine($"== BUFFER LAYOUTS ({f.BufferLayouts.Count}) ==");
        for (int i = 0; i < f.BufferLayouts.Count; i++)
            Console.WriteLine($"[{i,3}] " + string.Join(", ", f.BufferLayouts[i].Select(m => $"{m.Semantic}:{m.Type}:{m.Index}")));
        Console.WriteLine();

        Console.WriteLine($"== SKELETONS ==");
        if (f.Skeletons == null)
            Console.WriteLine("<null>");
        else
        {
            Console.WriteLine($"BaseSkeleton={f.Skeletons.BaseSkeleton.Count} AllSkeletons={f.Skeletons.AllSkeletons.Count}");
            foreach (var b in f.Skeletons.BaseSkeleton)
                Console.WriteLine($"  node={b.NodeIndex} parent={b.ParentIndex} child={b.FirstChildIndex} next={b.NextSiblingIndex} prev={b.PreviousSiblingIndex}");
        }
        Console.WriteLine();

        var meshNodes = f.Meshes.Select(m => m.NodeIndex).ToHashSet();
        var dummyParents = f.Dummies.SelectMany(d => new[] { (int)d.ParentBoneIndex, (int)d.AttachBoneIndex })
                                    .Where(i => i >= 0).ToHashSet();
        Console.WriteLine("== FLAG AUDIT (oczekiwane) ==");
        for (int i = 0; i < f.Nodes.Count; i++)
        {
            FLVER.Node.NodeFlags want = FLVER.Node.NodeFlags.Bone;
            if (meshNodes.Contains(i))
                want |= FLVER.Node.NodeFlags.Mesh;
            if (dummyParents.Contains(i))
                want |= FLVER.Node.NodeFlags.DummyOwner;
            var have = f.Nodes[i].Flags;
            Console.WriteLine($"[{i,3}] have={have} want={want} {(have == want ? "OK" : "MISMATCH")}  {f.Nodes[i].Name}");
        }
        return 0;
    }
}