using System.Data;

namespace WebApplication2.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
