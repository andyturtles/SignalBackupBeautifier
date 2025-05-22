namespace SignalBackupBeautifier;

/// <summary>
/// This is the main program for the SignalBackupBeautifier.
///
/// For the one special person, for which the entire universe conspired so that we found each other.
/// </summary>
internal class Program {

    private static void Main(string[] args) {
        if ( args.Length < 1 ) {
            Console.WriteLine("Use: dotnet run <BackupDatei> [removeNumbers] [minify]");
            Console.WriteLine("eg: dotnet run test.txt");
            Console.WriteLine("eg 2: dotnet run test.txt false false");
            Console.ReadLine();
            return;
        }

        string backupFile    = args[0];
        bool   removeNumbers = args.Length > 1 ? Boolean.Parse(args[1]) : true;
        bool   minify        = args.Length > 2 ? Boolean.Parse(args[2]) : true;

        try {
            if ( !File.Exists(backupFile) ) Log.Info($"File not found: {backupFile}");

            BackupBeautifier.Convert(backupFile, removeNumbers, minify);
            Console.WriteLine("Done.");
        }
        catch ( Exception ex ) {
            Console.WriteLine("Error: " + ex.Message);
            Console.ReadLine();
        }
    }

}
