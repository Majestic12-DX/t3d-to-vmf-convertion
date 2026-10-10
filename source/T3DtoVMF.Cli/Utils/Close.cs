namespace T3DtoVMF.Cli.Utils
{
    static class Close
    {
        // How "bad" is the error code depends on how high the value is
        public const int Success = 0;
        public const int FileMissing = 1;
        public const int BadT3D = 2;
        public const int UnexpectedError = 3;
        public const int BadArguments = 4;
        private static int ExitCode { get; set; } = Success;
        public static int OnPress(int exitCode = 0)
        {
            if (exitCode != 0) ExitCode = exitCode;

            // Launched by something that isn't a user
            if (Console.IsInputRedirected) return ExitCode;

            Console.Error.WriteLine("Press any button to exit...");
            Console.ReadKey();

            return ExitCode;
        }
        public static bool SetExitCodeIfWorse(int exitCode)
        {
            bool isWorse = exitCode > ExitCode;
            if (isWorse) ExitCode = exitCode;

            return isWorse;
        }
    }
}
