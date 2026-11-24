using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.Proton;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.Utilities
{
    public static class ConfigurationManager
    {
        static ConfigurationManager()
        {
            path = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));
            AppSettings = new();
            LoadSettings();
        }

        private static readonly string path;

        public static AppSettings AppSettings { get; set; }

        public static void SaveSettings(bool withFeedback = true)
        {
            string json = System.Text.Json.JsonSerializer.Serialize<AppSettings>(AppSettings);
            System.IO.File.WriteAllText(path , json);
            if (withFeedback)
            {
                Console.WriteLine("settings saved successfully");
                Console.WriteLine("Press enter to continue");

            }
        }

        public static void LoadSettings()
        {
            if (System.IO.File.Exists(path))
            {
                string json = System.IO.File.ReadAllText(path );
                //string json = Protection.Decrypt(enc);
                AppSettings? asst  = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                if (asst != null ) {AppSettings = asst;}
                if (!string.IsNullOrEmpty(AppSettings.PathToProtonFolder))
                {
                    if (File.Exists(Path.Combine(AppSettings.PathToProtonFolder, "BASE.dbs")))
                    {

                        ProtonBase.SetProtonBase(AppSettings.PathToProtonFolder);
                    }
                }

            } else
            {
                SaveSettings(false);
            }
        }

        public static void SetUserPassword()
        {
            string? pwd = Questioner.GetStringResponse("Enter password");
            if (!string.IsNullOrEmpty(pwd))
            {
                AppSettings.UserPassword = pwd!;
                SaveSettings();
            }
        }

        public static void SetDBname()
        {
            int trycount = 0; ;
            while (trycount < 4)
            {

                string? dbn = Questioner.GetStringResponse("Enter DB name", AppSettings.DBname);
                if (!string.IsNullOrEmpty(dbn))
                {
                    AppSettings.DBname = dbn!;
                    SaveSettings();
                    trycount = 10;
                } else
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if (toExit)
                    {
                        trycount = 10;
                    }
                }
                trycount++;
            }
            Questioner.EditSettings();
        }

        public static void SetPathToProtonDbs()
        {
            int trycount = 0; ;
            while (trycount<4) { 
                string? path = Questioner.GetStringResponse("Enter Proton dbs files directory path:", AppSettings.PathToProtonFolder);
                if (string.IsNullOrEmpty(path))
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if (toExit)
                    {
                        trycount = 10;
                    }
                } else
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        var filename = "BASE.DBS";
                        if (System.IO.File.Exists(Path.Combine(path , filename)))
                        {
                            AppSettings.PathToProtonFolder = path!;
                            SaveSettings();
                            trycount = 10; ;
                            ProtonBase.SetProtonBase(AppSettings.PathToProtonFolder);

                        } else Log.Error("Directory does not contain base.dbs file.");
                    }
                    else
                    {
                        Log.Error("Directory not found");
                    }
                }
                trycount++;
            }
            Questioner.EditSettings();
        }

        public static void SetExcludeItems()
        {
            string? res = Questioner.GetStringResponse("Enter items to ignore: ", string.Join(',', AppSettings.ExcludeItems.ConvertAll(o => o.ToString()).ToArray()));

            if (res != null) {
                AppSettings.ExcludeItems.Clear();

                var arry=res.Split(',').ToList();
                foreach (string item in arry)
                {
                    if(int.TryParse(item, out int i))
                    {
                        AppSettings.ExcludeItems.Add(i);
                    }
                }
                SaveSettings();
            }
            Questioner.EditSettings();
        }

        public static void SetOnlyTheseEntities()
        {
            string? res = Questioner.GetStringResponse("Enter ientities to load: ", string.Join(',', AppSettings.OnlyTheseEntities.ConvertAll(o => o.ToString()).ToArray()));

            if (res != null)
            {
                AppSettings.OnlyTheseEntities.Clear();

                var arry = res.Split(',').ToList();
                foreach (string item in arry)
                {
                    if (int.TryParse(item, out int i))
                    {
                        AppSettings.OnlyTheseEntities.Add(i);
                    }
                }
                SaveSettings();
            }
            Questioner.EditSettings();
        }
        public static void SetPathToLog()
        {
            int trycount = 0; ;
            while (trycount < 4)
            {
                string? path = Questioner.GetStringResponse("Enter directory to store log files: ", AppSettings.PathToLogs);
                if (string.IsNullOrEmpty(path))
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if (toExit)
                    {
                        trycount = 10;
                    }
                }
                else
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        AppSettings.PathToLogs = path!;
                        SaveSettings();
                        trycount = 10;
                        Log.Logger = new LoggerConfiguration()
                             .MinimumLevel.Debug()
                             .WriteTo.Console()
                             .WriteTo.File(Path.Combine(ConfigurationManager.AppSettings.PathToLogs ,"AppsLog.txt"), rollingInterval: RollingInterval.Day)
                             .CreateLogger();

                    }
                    else
                    {
                        Console.WriteLine("Directory not found");
                    }
                }
                trycount++;
            }
            Questioner.EditSettings();
        }

        public static void SetDbServer()
        {

            int trycount = 0; ;
            while (trycount < 4) { 
                var sa = Questioner.GetStringResponse("Enter SQL server address", AppSettings.Server);
                if (!string.IsNullOrEmpty(sa))
                {
                    AppSettings.Server = sa!;
                    SaveSettings();
                    trycount = 10;
                }
                else
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if (toExit)
                    {
                        trycount = 10;
                    }
                }
                trycount++;
            }

            Questioner.EditSettings();

        }

        public static void SetDbPassword()
        {
            int trycount = 0;
            while (trycount < 4)
            {
                string? pwd = Questioner.GetStringResponse("Enter DB password", AppSettings.DBPassword);
                if (!string.IsNullOrEmpty(pwd))
                {
                    SaveSettings();
                    trycount = 10;
                }
                else
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if ( toExit)
                    {
                        trycount = 10;
                    }
                }
                trycount++;
            }

            Questioner.EditSettings();
        }


        public static void SetDbIntegretedSecurity()
        {
            int trycount = 0;
            while (trycount < 4) { 
                bool? igp = Questioner.GetBoolResponse("Db Integrated security", AppSettings.DBIsIntegrated);
                if (igp != null)
                {
                    AppSettings.DBIsIntegrated = (bool)igp!;
                    SaveSettings();
                    trycount = 10;
                }
                else
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if (toExit)
                    {
                        trycount = 10;
                    }
                }
                trycount++;
            }

            Questioner.EditSettings();
        }

        public static void SetNoLoad()
        {
            int trycount = 0;
            while (trycount < 4)
            {
                bool? igp = Questioner.GetBoolResponse("Scan only, no load", AppSettings.NoLoad);
                if (igp != null)
                {
                    AppSettings.NoLoad = (bool)igp!;
                    SaveSettings();
                    trycount = 10;
                }
                else
                {
                    var toExit = Questioner.GetBoolResponse("Continue without saving?", false);
                    if (toExit)
                    {
                        trycount = 10;
                    }
                }
                trycount++;
            }

            Questioner.EditSettings();
        }
    }

    public class AppSettings
    {

        public  string UserPassword { get; set; } = string.Empty;
        public  string Server { get; set; } = string.Empty;
        public  string DBname { get; set; } = string.Empty;
        public  string DBPassword { get; set; } = string.Empty;
        public  bool DBIsIntegrated { get; set; } = false;
        public string PathToProtonFolder { get; set; } = string.Empty;
        public string PathToLogs { get; set; } = string.Empty;
        public List<int> ExcludeItems { get; set; } = [];
        public List<int> OnlyTheseEntities { get; set; } = [];
        public bool NoLoad { get; set; } = false;
        public  DateTime LastUpdate { get; set; } = DateTime.MinValue;


        public string SQLConnectionString(bool isDefault = false)
        {
            var sb = new SqlConnectionStringBuilder()
            {
                DataSource = Server,
                InitialCatalog = isDefault? "master": DBname,
                IntegratedSecurity = DBIsIntegrated,
                Password = DBIsIntegrated? "" : DBPassword,
                CommandTimeout = 100000,
                TrustServerCertificate = true,
            };
             
            return sb.ToString();
        }

        public bool TestConnection(bool defaultDb=false)
        {
            using SqlConnection connection = new(SQLConnectionString(defaultDb));
            
            try
            {
                connection.Open();
                connection.Close();
                return true;
            }
            catch (SqlException ex)
            {
                Log.Error($"unable to connect to server using: {SQLConnectionString(defaultDb)}. Exception: {ex.Message}");
                //Console.WriteLine("unable to connect to server using:" + ConfigurationManager.AppSettings.SQLConnectionString(true) + " " + ex.Message);
                return false;
            }
            
        }
        public bool TestPathToProton()
        {
            if (File.Exists(Path.Combine(PathToProtonFolder , "BASE.dbs")))
            {
                return true;
            }
            else
            {
                Log.Error($"Proton dbs files not found in specified directory: ({PathToProtonFolder}).");
                return false;
            }
        }
        public bool TestPathToLogs()
        {
            if (Path.Exists(PathToLogs))
            {
                return true;
            }
            else { 
                Log.Error($"Log files directory not found: ({PathToLogs}).");
                return false;
            }
        }

        public bool IsValid()
        {
            return TestPathToProton() && TestPathToLogs() && TestConnection(true);
        }
    }
}