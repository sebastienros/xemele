using Xemele.DomPersistenceMeasurements;
foreach (int length in new[] { 16, 16384 })
    foreach (string shape in new[] { "values", "names" })
        new DomPersistenceBenchmarks { Length = length, Shape = shape }.Setup();
Console.WriteLine("Complete checked/trusted node/member equivalence: 4 setup pairs / 8 cases passed, no timing.");
