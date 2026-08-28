using Dapper;
using Npgsql;
using PharmaLink.API.DTOs.Medicines;
using PharmaLink.API.Entities;
using PharmaLink.API.Interfaces.RepositoryInterface;
using System.Data;
using System.Text;

namespace PharmaLink.API.Repositories
{
    public class MedicineRepository(IConfiguration configuration) : IMedicineRepository
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection string not found");

        public async Task<Medicine?> GetByIdAsync(int id)
        {
            using var connection = new NpgsqlConnection (_connectionString);
            string sql = "SELECT * FROM Medicines WHERE Id = @Id";
            return await connection.QuerySingleOrDefaultAsync<Medicine>(sql, new { Id = id });
        }

        public async Task<Medicine?> GetByNameAsync(string name)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            string sql = @"
                SELECT * FROM Medicines 
                WHERE LOWER(LTRIM(RTRIM(Name))) = LOWER(LTRIM(RTRIM(@Name)))";

            return await connection.QuerySingleOrDefaultAsync<Medicine>(sql, new { Name = name });
        }

        public async Task<(IEnumerable<Medicine>, int)> GetAllAsync(MedicineParams parameters)
        {
            using var connection = new NpgsqlConnection(_connectionString);

            var sqlBuilder = new System.Text.StringBuilder(@"SELECT * FROM ""Medicines"" WHERE 1=1 ");
            var dbParams = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                sqlBuilder.Append(@" AND ""Name"" ILIKE @SearchTerm");
                dbParams.Add("SearchTerm", $"%{parameters.SearchTerm}%");
            }

            if (parameters.CategoryId.HasValue)
            {
                sqlBuilder.Append(@" AND ""CategoryId"" = @CategoryId");
                dbParams.Add("CategoryId", parameters.CategoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameters.Filter))
            {
                if (parameters.Filter.Equals("low", StringComparison.OrdinalIgnoreCase))
                {
                    sqlBuilder.Append(@" AND ""StockQuantity"" <= 10");
                }
                else if (parameters.Filter.Equals("expiring", StringComparison.OrdinalIgnoreCase))
                {
                    sqlBuilder.Append(@" AND ""ExpiryDate"" <= (CURRENT_DATE + INTERVAL '90 days')");
                }
            }

            string sortQuery = parameters.SortBy?.ToLower() switch
            {
                "price" => @"ORDER BY ""Price"" ASC",
                "price_desc" => @"ORDER BY ""Price"" DESC",
                "expiry" => @"ORDER BY ""ExpiryDate"" ASC",
                "name_desc" => @"ORDER BY ""Name"" DESC",
                _ => @"ORDER BY ""Name"" ASC"
            };

            string whereClause = sqlBuilder.ToString().Substring(sqlBuilder.ToString().IndexOf("WHERE"));
            string countSql = $@"SELECT COUNT(*) FROM ""Medicines"" {whereClause}";

            sqlBuilder.Append($" {sortQuery}");
            sqlBuilder.Append(@" LIMIT @PageSize OFFSET @Offset");

            dbParams.Add("Offset", (parameters.PageNumber - 1) * parameters.PageSize);
            dbParams.Add("PageSize", parameters.PageSize);

            int totalCount = await connection.ExecuteScalarAsync<int>(countSql, dbParams);
            var items = await connection.QueryAsync<Medicine>(sqlBuilder.ToString(), dbParams);

            return (items, totalCount);
        }

        public async Task<int> CreateAsync(Medicine medicine)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            string sql = @"
                INSERT INTO Medicines (CategoryId, Name, Description, StockQuantity, Price, ExpiryDate)
                VALUES (@CategoryId, @Name, @Description, @StockQuantity, @Price, @ExpiryDate);
                SELECT LASTVAL();";
            return await connection.QuerySingleAsync<int>(sql, medicine);
        }

        // Align signature with nullable IDbTransaction? contract
        public async Task<bool> UpdateStockAsync(int id, int quantityDeducted, IDbTransaction? transaction = null)
        {
            string sql = "UPDATE Medicines SET StockQuantity = StockQuantity - @Quantity WHERE Id = @Id";
            var parameters = new { Quantity = quantityDeducted, Id = id };

            if (transaction != null)
            {
                int rows = await transaction.Connection!.ExecuteAsync(sql, parameters, transaction);
                return rows > 0;
            }
            else
            {
                using var connection = new NpgsqlConnection(_connectionString);
                int rows = await connection.ExecuteAsync(sql, parameters);
                return rows > 0;
            }
        }

        public async Task<bool> UpdateAsync(Medicine medicine)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            string sql = @"
                UPDATE Medicines 
                SET Name = @Name, 
                    Description = @Description,
                    CategoryId = @CategoryId, 
                    StockQuantity = @StockQuantity, 
                    Price = @Price, 
                    ExpiryDate = @ExpiryDate
                    WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, medicine);
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            string sql = "DELETE FROM Medicines WHERE Id = @Id";

            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}