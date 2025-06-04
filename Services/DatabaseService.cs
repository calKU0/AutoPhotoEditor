using AutoPhotoEditor.Interfaces;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPhotoEditor.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;
        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }
        public async Task<bool> AttachImageToProduct(int productId, string extension, byte[] imageBytes)
        {
            bool success = false;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "dbo.DodajZdjecieDoTowaru";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@twrId", productId));
                    command.Parameters.Add(new SqlParameter("@zalacznikRozszerzenie", extension));
                    command.Parameters.Add("@zalacznikDane", SqlDbType.VarBinary, imageBytes.Length).Value = imageBytes;

                    // Add return value parameter
                    var returnParameter = new SqlParameter("@ReturnVal", SqlDbType.Int);
                    returnParameter.Direction = ParameterDirection.ReturnValue;
                    command.Parameters.Add(returnParameter);

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();

                    int result = (int)returnParameter.Value;

                    success = (result == 0);
                }
            }
            return success;

        }

        public async Task<int> FindProductByEANOrCode(string code)
        {
            int resultId = 0;
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (var command = connection.CreateCommand())
                {
                    string query = "select top 1 twr_gidnumer from cdn.twrkarty where twr_kod = @code or twr_ean = @code";

                    command.CommandType = CommandType.Text;
                    command.CommandText = query;

                    command.Parameters.Add(new SqlParameter("@code", code));

                    await connection.OpenAsync();

                    var result = await command.ExecuteScalarAsync();
                    if (result is not null)
                        resultId = (int)result;
                }
            }

            return resultId;
        }

        public async Task<string> FindProductById(int id)
        {
            string resultCode = string.Empty;
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (var command = connection.CreateCommand())
                {
                    string query = "select top 1 twr_kod from cdn.twrkarty where twr_gidnumer = @id";

                    command.CommandType = CommandType.Text;
                    command.CommandText = query;

                    command.Parameters.Add(new SqlParameter("@id", id));

                    await connection.OpenAsync();

                    var result = await command.ExecuteScalarAsync();
                    if (result is not null)
                        resultCode = (string)result;
                }
            }

            return resultCode;
        }
    }
}
