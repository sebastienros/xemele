using System.Runtime.InteropServices;
using Xemele.IntegrationSamples;
Console.WriteLine(await ToolkitScenario.RunAsync(args.Single()));
Console.Error.WriteLine($"Framework={RuntimeInformation.FrameworkDescription}; Architecture={RuntimeInformation.ProcessArchitecture}");
