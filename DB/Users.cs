using HttpListenerExample;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace LightUserAuth.DB
{
    public class UsersContext: DbContext
    {
        string connectionString = "Server=(local)\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=true;";
        public DbSet<User> Users { get; set; }
        SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder();
        public UsersContext() 
        {
            sqlConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
           
            

        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlServer(sqlConnectionStringBuilder.ConnectionString);


    }
    public class User
    {
        [Key]
        required public string UserName { get; set; }
        required public string Password { get; set; }
        required public string Salt { get; set; }
        required public string Email { get; set; }
    }
}
