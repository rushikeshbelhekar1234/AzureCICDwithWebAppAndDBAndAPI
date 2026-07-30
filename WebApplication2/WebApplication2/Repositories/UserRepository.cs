using Dapper;
using System.Data;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Repositories
{
    public class UserRepository
    {
        private readonly IDbConnectionFactory _factory;

        public UserRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "SELECT Id, Username, PasswordHash FROM Users WHERE Username = @Username LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
        }

        public async Task<int> CreateAsync(User user)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "INSERT INTO Users (Username, PasswordHash) VALUES (@Username, @PasswordHash); SELECT LAST_INSERT_ID();";
            var id = await conn.ExecuteScalarAsync<int>(sql, new { Username = user.Username, PasswordHash = user.PasswordHash });
            return id;
        }
    }
}
