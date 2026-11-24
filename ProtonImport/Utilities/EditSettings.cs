using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.protonToSql;
using ProtonConsole2.ProtonToSql;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.Utilities
{
    internal class Questioner
    {
        public static bool GetBoolResponse(string caption, bool defaultValue) 
        {
            string? retn = GetStringResponse(caption + " Y/N?", defaultValue ? "Y":"N");
            if (!string.IsNullOrEmpty(retn))
            {
                if  (retn!.ToUpper().StartsWith("Y", StringComparison.CurrentCultureIgnoreCase)) return true;
                if (retn.ToUpper().StartsWith("N", StringComparison.CurrentCultureIgnoreCase)) return false;
            }
            return false;
        }
        public static int? GetIntResponse(string caption, int defaultValue = 0)
        {
            string? retn = GetStringResponse(caption, defaultValue.ToString());
            if (!string.IsNullOrEmpty(retn))
            {
                if (int.TryParse(retn, out int op))
                {
                    return op;
                }

            }
            return null;
        }

        public static string? GetStringResponse(string caption, string defaultValue = "")
        {
            Console.WriteLine(caption);
            Console.WriteLine("Current value: " + defaultValue);
            return  Console.ReadLine();
        }

       
        public static void EditSettings() 
        {
            var apsettings = ConfigurationManager.AppSettings;
            string capt;
            //Console.Clear();
            Console.WriteLine("[1] SQL Db Server: " + apsettings.Server);
            Console.WriteLine("[2] DB name: " + apsettings.DBname);
            Console.WriteLine("[3] DB integrated security: " + apsettings.DBIsIntegrated.ToString());
            Console.WriteLine("[4] DB Password: ");
            Console.WriteLine("[5] Proton.dbs files directory: " + apsettings.PathToProtonFolder);
            Console.WriteLine("[6] Log files directory: " + apsettings.PathToLogs);
            Console.WriteLine("[7] Items to exclude (leave blank to include all): " + string.Join(',',apsettings.ExcludeItems.ConvertAll(o => o.ToString()).ToArray()));
            Console.WriteLine("[8] Only these entities (leave blank for all): " + string.Join(',', apsettings.OnlyTheseEntities.ConvertAll(o => o.ToString()).ToArray()));
            
            if (apsettings.TestPathToProton())
            {
                Console.WriteLine("[9] Scan for errors, no import: " + apsettings.NoLoad.ToString());

                if (apsettings.IsValid())
                {
                    using Proton2Context ctx = new();
                    Console.WriteLine("[10] Load/update metadata...");
                    Console.WriteLine("[11] Load/update entity data...");
                    capt = "Enter number 0-11;";
                }
                else
                {
                    capt = "Enter number 0-9;";
                }
            }
            else capt = "Enter number 0-8;";
            Console.WriteLine("[0] Exit");
            
            int? opt = null;
            while (opt != 0)
            {
                opt = GetIntResponse(capt);
                SwitchProcess(opt);
            }
        }

        public static void SwitchProcess(int? opt)
        {
            var appsettings = ConfigurationManager.AppSettings;
            switch (opt)
            {
                case 1:
                    ConfigurationManager.SetDbServer();
                    break;
                case 2:
                    ConfigurationManager.SetDBname();
                    break;
                case 3:
                    ConfigurationManager.SetDbIntegretedSecurity();
                    break;
                case 4:
                    ConfigurationManager.SetDbPassword();
                    break;

                case 5:
                    ConfigurationManager.SetPathToProtonDbs();
                    break;

                case 6:
                    ConfigurationManager.SetPathToLog();
                    break;

                case 7:
                    ConfigurationManager.SetExcludeItems();
                    break;

                case 8:
                    ConfigurationManager.SetOnlyTheseEntities();
                    break;


                case 9:
                    ConfigurationManager.SetNoLoad();
                    break;

                case 10:

                    if (appsettings.IsValid())
                    {
                        Log.Information("Connecting to SQL Server {Server} and database {DBname} using {cnn}", appsettings.Server, appsettings.DBname, appsettings.SQLConnectionString());

                        MetadataLoader.LoadMetadata();
                        EntityLoader.LoadLookups(1000);
                    }
                    break;


                case 11:
                    if (appsettings.IsValid())
                    {
                        Log.Information("Connecting to SQL Server {Server} and database {DBname} using {cnn}", appsettings.Server, appsettings.DBname, appsettings.SQLConnectionString());

                        using ValuesLoader dsl = new();
                        dsl.LoadValues(1000);
                        if (dsl.EntitiesLoaded > 0 || dsl.EntitiesUpdated > 0)
                        {
                            EntityLoader.LoadIndexes(1000);
                            EntityLoader.LoadEntities(1000);
                            EntityLoader.UpdateEntityNames();
                        }
                    }
                    break;

                case 12:
                    if (appsettings.IsValid())
                    {
                        EntityLoader.LoadIndexes(1000);
                        EntityLoader.LoadEntities(1000);
                        EntityLoader.UpdateEntityNames();
                    }
                    break;
                case 14:
                    Console.WriteLine(ConfigurationManager.AppSettings.SQLConnectionString());
                    break;
                case 0:
                    Console.WriteLine("Exiting...");
                    break;
                default:
                    break;
            }
        }
    }
}
