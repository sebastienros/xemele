using Xemele;

static class AllocationProbe
{
    internal static void Run()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var first = new XmlDocument(); var builder = first.BeginDomConstruction();
        long cold = GC.GetAllocatedBytesForCurrentThread() - before;
        var root = first.CreateElement("e"); builder.AppendChild(first.DocumentNode, root);
        for (int i = 0; i < 100; i++) builder.AppendChild(root, first.CreateText("x"));
        long helper = 0, persistence = 0;
        for (int i = 0; i < 4096; i++)
        {
            before = GC.GetAllocatedBytesForCurrentThread(); var text = first.CreateText("x"); persistence += GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread(); builder.AppendChild(root, text); helper += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        string[] names = new string[512]; before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < names.Length; i++) names[i] = "a" + i;
        long fixture = GC.GetAllocatedBytesForCurrentThread() - before;
        static XmlDocument Build(string[] names)
        {
            var d = new XmlDocument(new() { InternedNames = null }); var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
            foreach (var name in names) { b.AppendAttribute(e, name, "value"); b.AppendNamespaceDeclaration(e, name, "urn:shared"); }
            return d;
        }
        Build(names);
        before = GC.GetAllocatedBytesForCurrentThread(); var owned = Build(names); long ownedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        var retained = new XmlDocument[32]; long retainedBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < retained.Length; i++) retained[i] = Build(names);
        long retainedBytes = (GC.GetTotalMemory(true) - retainedBefore) / retained.Length;
        Console.WriteLine($"cold document+builder setup={cold} B; fixture name setup={fixture} B; warm 4096 append helpers={helper} B; node arena growth={persistence} B; owned 512 attributes+512 declarations construction={ownedBytes} B; retained document delta={retainedBytes} B/doc (32-document GC estimate, shared fixture strings excluded)");
        if (helper != 0 || owned.RootElement.Attributes.Count != 512 || retained[^1].RootElement.NamespaceDeclarations.Count != 512) throw new Exception("Allocation/ownership probe mismatch.");
        GC.KeepAlive(first); GC.KeepAlive(owned); GC.KeepAlive(retained); GC.KeepAlive(names);
    }
}
