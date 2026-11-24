using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using ProtonImport.DataContext;
using Serilog;

namespace ProtonImport.ProtonToSql
{
    /// <summary>
    /// Applies Sql/Schema.sql (embedded in the assembly): schema fixes and the database
    /// functions/procedures Photon uses. The script is idempotent, so it runs on every load.
    /// </summary>
    internal static partial class DatabaseSchema
    {
        private const string ScriptResource = "ProtonImport.Sql.Schema.sql";

        [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
        private static partial Regex GoSeparator();

        public static void Apply()
        {
            // creating a context creates the database and tables if they don't exist yet
            using (Proton2Context ctx = new()) { }

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ScriptResource)
                ?? throw new InvalidOperationException($"Embedded resource {ScriptResource} not found");
            using StreamReader reader = new(stream);
            string script = reader.ReadToEnd();

            Log.Information("Applying database schema updates..");

            using SqlConnection cnn = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            cnn.Open();

            foreach (string batch in GoSeparator().Split(script))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                try
                {
                    // no timeout: the first run may rebuild large tables with compression
                    using SqlCommand cmd = new(batch, cnn) { CommandTimeout = 0 };
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    // one failing batch (e.g. compression on an old edition) shouldn't stop the import
                    Log.Warning(ex, "Schema update batch failed:{NewLine}{Batch}", Environment.NewLine, batch.Trim());
                }
            }
        }
    }
}
