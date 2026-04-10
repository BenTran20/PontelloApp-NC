using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PontelloApp.Ultilities
{
    /// <summary>
    /// sqLite Database Utility
    /// D. Stovell: 2025-09-16
    /// </summary>
    public static class SqLiteDBUtility
    {
        /// <summary>
        /// Releases file locks and deletes the SQLite database file.
        /// </summary>
        /// <param name="context">The DbContext object</param>
        /// <returns>True if the database file was deleted or didn't exist; false if deletion failed.</returns>
        public static bool ReallyEnsureDeleted(DbContext context)
        {
            string connectionString = context.Database.GetDbConnection().ConnectionString;
            var builder = new SqliteConnectionStringBuilder(connectionString);
            string filePath = builder.DataSource;

            if (!File.Exists(filePath))
            {
                return true; // Already gone
            }

            // Clear connection pool to release file locks
            SqliteConnection.ClearAllPools();

            // Attempt deletion
            context.Database.EnsureDeleted();

            // Confirm deletion
            return !File.Exists(filePath);
        }
    }
}
