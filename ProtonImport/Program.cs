using ProtonImport.ProtonToSql;
using ProtonImport.Utilities;
using Serilog;

// No arguments: unattended import (e.g. scheduled task). Any argument: interactive settings menu.
bool interactive = args.Length > 0;

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(Path.Combine(ConfigurationManager.AppSettings.PathToLogs, "AppLog.txt"), rollingInterval: RollingInterval.Day);
if (interactive) logConfig.WriteTo.Console();
Log.Logger = logConfig.CreateLogger();

int exitCode = 0;
try
{
    if (interactive)
    {
        Questioner.EditSettings();
    }
    else if (ConfigurationManager.AppSettings.IsValid())
    {
        Log.Information("Import started");
        ValuesLoader.ImportAll(1000);
        Log.Information("Import ended");
    }
    else
    {
        Log.Error("Configuration is not valid. Please edit settings.");
        exitCode = 1;
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred.");
    exitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

return exitCode;
