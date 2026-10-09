if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    Console.WriteLine("Xemele corpus manager");
    Console.WriteLine("Bootstrap shell: --help is the available command.");
    return 0;
}

Console.Error.WriteLine("Unknown command. Use --help for available commands.");
return 2;
