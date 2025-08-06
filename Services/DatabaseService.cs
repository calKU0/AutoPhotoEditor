using AutoPhotoEditor.Interfaces;
using AutoPhotoEditor.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AutoPhotoEditor.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int?> AttachImageToProductAsync(int productId, string extension, byte[] imageBytes)
        {
            if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
            if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("Extension required", nameof(extension));
            if (imageBytes is null) throw new ArgumentNullException(nameof(imageBytes));

            const string procName = "dbo.DodajZdjecieDoTowaru";

            using var connection = new SqlConnection(_connectionString);
            using var command = connection.CreateCommand();
            command.CommandText = procName;
            command.CommandType = CommandType.StoredProcedure;

            // Input params
            command.Parameters.Add(new SqlParameter("@twrId", SqlDbType.Int) { Value = productId });
            command.Parameters.Add(new SqlParameter("@zalacznikRozszerzenie", SqlDbType.VarChar, 29) { Value = extension });

            var dataParam = new SqlParameter("@zalacznikDane", SqlDbType.VarBinary, -1)
            {
                Value = imageBytes
            };
            command.Parameters.Add(dataParam);

            var outDabId = new SqlParameter("@DabId", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outDabId);

            // Return value
            var retParam = new SqlParameter("@RETURN_VALUE", SqlDbType.Int)
            {
                Direction = ParameterDirection.ReturnValue
            };
            command.Parameters.Add(retParam);

            await connection.OpenAsync().ConfigureAwait(false);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            int returnCode = retParam.Value == DBNull.Value ? -999 : (int)retParam.Value;
            if (returnCode != 0)
            {
                return null;
            }

            if (outDabId.Value == DBNull.Value) return null;
            return (int?)outDabId.Value;
        }

        public async Task<bool> DetachImageFromProductAsync(int dabId)
        {
            if (dabId <= 0) throw new ArgumentOutOfRangeException(nameof(dabId));

            const string procName = "dbo.UsunZdjecieTowaru";

            using var connection = new SqlConnection(_connectionString);
            using var command = connection.CreateCommand();
            command.CommandText = procName;
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@DabId", SqlDbType.Int) { Value = dabId });

            var retParam = new SqlParameter("@RETURN_VALUE", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            command.Parameters.Add(retParam);

            await connection.OpenAsync().ConfigureAwait(false);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);

            int returnCode = retParam.Value == DBNull.Value ? -999 : (int)retParam.Value;
            return returnCode == 0;
        }

        public async Task<Product?> FindProductByEANOrCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            const string query = @"
            SELECT TOP 1
                twr_gidnumer AS Id,
                twr_kod      AS Code,
                twr_nazwa    AS Name,
                twr_ean      AS EAN
            FROM cdn.twrkarty
            WHERE twr_kod = @code OR twr_ean = @code;";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                // adjust size/type if you know column type/length
                var p = new SqlParameter("@code", SqlDbType.NVarChar, 128) { Value = code.Trim() };
                command.Parameters.Add(p);

                await connection.OpenAsync().ConfigureAwait(false);

                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync().ConfigureAwait(false))
                        return null;

                    int id = reader["Id"] is DBNull ? 0 : Convert.ToInt32(reader["Id"]);
                    string codeVal = reader["Code"] is DBNull ? string.Empty : reader["Code"].ToString()!;
                    string nameVal = reader["Name"] is DBNull ? string.Empty : reader["Name"].ToString()!;
                    string eanVal = reader["EAN"] is DBNull ? string.Empty : reader["EAN"].ToString()!;

                    return new Product
                    {
                        Id = id,
                        Code = codeVal,
                        Name = nameVal,
                        EAN = eanVal
                    };
                }
            }
        }

        public async Task<Product?> FindProductByIdAsync(int id)
        {
            if (id <= 0) return null;

            const string query = @"
                SELECT TOP 1
                    twr_gidnumer AS Id,
                    twr_kod      AS Code,
                    twr_nazwa    AS Name,
                    twr_ean      AS EAN
                FROM cdn.twrkarty
                WHERE twr_gidnumer = @id;";

            using var connection = new SqlConnection(_connectionString);
            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

            await connection.OpenAsync().ConfigureAwait(false);

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
                return null;

            int idVal = reader["Id"] is DBNull ? 0 : Convert.ToInt32(reader["Id"]);
            string code = reader["Code"] is DBNull ? string.Empty : reader["Code"].ToString()!;
            string name = reader["Name"] is DBNull ? string.Empty : reader["Name"].ToString()!;
            string ean = reader["EAN"] is DBNull ? string.Empty : reader["EAN"].ToString()!;

            return new Product
            {
                Id = idVal,
                Code = code,
                Name = name,
                EAN = ean
            };
        }
    }
}