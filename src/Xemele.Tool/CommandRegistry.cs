namespace Xemele.Tool;

// Feature tasks implement their registration hook in a separate partial file.
internal sealed partial class CommandRegistry
{
    private readonly SortedDictionary<string, (string Description, Func<string[], int> Execute)> _commands = new(StringComparer.Ordinal);

    public CommandRegistry()
    {
        RegisterXmlCommands();
        RegisterHtmlCommands();
        RegisterValidationCommands();
    }

    internal void Register(string name, string description, Func<string[], int> execute)
    {
        _commands.Add(name, (description, execute));
    }

    internal int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            Console.WriteLine("Xemele XML/HTML toolkit");
            Console.WriteLine("Usage: xemele <command> [options]");
            Console.WriteLine("  --help  Show help");
            foreach (var (name, command) in _commands)
            {
                Console.WriteLine($"  {name}  {command.Description}");
            }
            return 0;
        }

        if (_commands.TryGetValue(args[0], out var selected))
        {
            return selected.Execute(args[1..]);
        }

        Console.Error.WriteLine($"Unknown command: {args[0]}. Use --help for available commands.");
        return 2;
    }

    partial void RegisterXmlCommands();
    partial void RegisterHtmlCommands();
    partial void RegisterValidationCommands();
}
