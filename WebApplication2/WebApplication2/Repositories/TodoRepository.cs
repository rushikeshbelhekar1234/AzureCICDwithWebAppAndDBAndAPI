using Dapper;
using System.Data;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Repositories
{
    public class TodoRepository
    {
        private readonly IDbConnectionFactory _factory;

        public TodoRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<IEnumerable<TodoItem>> GetPageAsync(int page, int pageSize)
        {
            var offset = (page - 1) * pageSize;
            using var conn = _factory.CreateConnection();
            const string sql = "SELECT Id, Title, IsCompleted FROM Todos ORDER BY Id LIMIT @PageSize OFFSET @Offset";
            return await conn.QueryAsync<TodoItem>(sql, new { PageSize = pageSize, Offset = offset });
        }

        public async Task<TodoItem?> GetByIdAsync(int id)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "SELECT Id, Title, IsCompleted FROM Todos WHERE Id = @Id";
            return await conn.QueryFirstOrDefaultAsync<TodoItem>(sql, new { Id = id });
        }

        public async Task<int> CreateAsync(TodoItem item)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "INSERT INTO Todos (Title, IsCompleted) VALUES (@Title, @IsCompleted); SELECT LAST_INSERT_ID();";
            var id = await conn.ExecuteScalarAsync<int>(sql, new { Title = item.Title, IsCompleted = item.IsCompleted ? 1 : 0 });
            return id;
        }

        public async Task<bool> UpdateAsync(TodoItem item)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "UPDATE Todos SET Title = @Title, IsCompleted = @IsCompleted WHERE Id = @Id";
            var affected = await conn.ExecuteAsync(sql, new { Title = item.Title, IsCompleted = item.IsCompleted ? 1 : 0, Id = item.Id });
            return affected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "DELETE FROM Todos WHERE Id = @Id";
            var affected = await conn.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }
    }
}
