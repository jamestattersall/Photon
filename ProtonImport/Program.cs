
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.protonToSql;
using ProtonConsole2.Utilities;
using Serilog;

try
{

    if (args.Length == 0)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(ConfigurationManager.AppSettings.PathToLogs, "AppLog.txt"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
        if (ConfigurationManager.AppSettings.IsValid())
        {

            Log.Information($"ProtonConsole2 loaded: {0}", DateTime.Now.ToString());
            Log.Information($"Main task started: {0}", DateTime.Now.ToString());
            using ValuesLoader dsl = new();
            dsl.LoadValues(1000);
            if (dsl.EntitiesLoaded > 0 || dsl.EntitiesUpdated > 0)
            {
                EntityLoader.LoadIndexes(1000);
                EntityLoader.LoadEntities(1000);
                EntityLoader.UpdateEntityNames();
            }
            Log.Information($"Main task ended: {0}", DateTime.Now.ToString());
            Environment.Exit(0);
            return;
        }
        else
        {
            Log.Error("Configuration is not valid. Please edit settings.");
        }
    }
    else
    {
        Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .WriteTo.Console()
           .WriteTo.File(Path.Combine(ConfigurationManager.AppSettings.PathToLogs, "AppLog.txt"), rollingInterval: RollingInterval.Day)
           .CreateLogger();

        Questioner.EditSettings();
    }
    Environment.Exit(0);
    return;
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during startup.");
}





