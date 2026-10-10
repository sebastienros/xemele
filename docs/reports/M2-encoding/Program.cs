using BenchmarkDotNet.Running;
using Xemele;
using Xemele.Parsing.Input;
if(args.Length==3 && args[0]=="--contract")
{
    var model=System.Reflection.Assembly.LoadFile(Path.GetFullPath(args[1]));
    static string[] Surface(System.Reflection.Assembly assembly)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.DeclaredOnly;
        var rows=new List<string>();
        foreach(var type in assembly.GetTypes().Where(t=>t.Namespace=="Xemele.Parsing.Input" && !t.IsNested))
        {
            rows.Add($"{type.FullName}: public={type.IsPublic}; sealed={type.IsSealed}; interface={type.IsInterface}; enum={type.IsEnum}; value={type.IsValueType}; readonly={type.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute),false)}");
            foreach(var member in type.GetMembers(flags))
            {
                if(member is System.Reflection.MethodBase method)
                {
                    if(method.IsSpecialName && method.Name.StartsWith("set_",StringComparison.Ordinal))continue;
                    rows.Add($"{type.FullName}: {method}; parameters={string.Join(";",method.GetParameters().Select(p=>$"{p.Name}:{p.ParameterType}:{p.IsOptional}:{p.DefaultValue}"))}");
                }
                else if(member is System.Reflection.PropertyInfo property)rows.Add($"{type.FullName}: property {property}");
                else if(member is System.Reflection.FieldInfo field)rows.Add($"{type.FullName}: field {field}; constant={(field.IsLiteral?field.GetRawConstantValue():null)}");
            }
        }
        return rows.Order(StringComparer.Ordinal).ToArray();
    }
    var actual=Surface(typeof(XmlInputDecoder).Assembly);var expected=Surface(model);
    if(actual.Length==0 || !actual.SequenceEqual(expected))
    {
        Console.WriteLine("Only actual: "+string.Join("\n",actual.Except(expected)));
        Console.WriteLine("Only model: "+string.Join("\n",expected.Except(actual)));throw new Exception("Internal contract mismatch");
    }
    File.WriteAllLines(args[2],actual);
    Console.WriteLine($"Exact internal compiled surface: {actual.Length} rows match approved M2 Encoding model.");
}
else if(args.Contains("--allocation"))
{
    long first=GC.GetAllocatedBytesForCurrentThread();var cold=new XmlInputDecoder(new());
    Console.WriteLine($"Cold setup bytes: {GC.GetAllocatedBytesForCurrentThread()-first}");cold.Dispose();
    for(int i=0;i<100;i++)new XmlInputDecoder(new()).Dispose();
    long before=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<10000;i++)new XmlInputDecoder(new()).Dispose();
    Console.WriteLine($"Warm setup bytes/op: {(GC.GetAllocatedBytesForCurrentThread()-before)/10000.0}");
    var retained=new XmlInputDecoder[10000];GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    long heap=GC.GetGCMemoryInfo().HeapSizeBytes-GC.GetGCMemoryInfo().FragmentedBytes;
    for(int i=0;i<retained.Length;i++)retained[i]=new XmlInputDecoder(new());
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    long after=GC.GetGCMemoryInfo().HeapSizeBytes-GC.GetGCMemoryInfo().FragmentedBytes;
    Console.WriteLine($"Informational retained heap-minus-fragmentation bytes/op (reference array excluded): {(after-heap)/10000.0}");GC.KeepAlive(retained);
}
else if(args.Contains("--oracle"))
{
    foreach(string label in new[]{"US-ASCII","ISO-8859-1","Windows-1252"})
    foreach(int length in new[]{8,4096})new EncodingBenchmarks{Label=label,Length=length}.Setup();
    new EncodingDomBenchmarks().Setup();
    Console.WriteLine("All six baseline/decoder character+absolute-mapping oracles and legacy DOM oracle passed.");
}
else BenchmarkSwitcher.FromAssembly(typeof(EncodingBenchmarks).Assembly).Run(args);
