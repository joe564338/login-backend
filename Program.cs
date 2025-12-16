using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Specialized;
using BCrypt.Net;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using LightUserAuth.DB;
namespace HttpListenerExample
{
    class HttpServer
    {
        public static string connectionString = "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=true;";//"Data Source=DESKTOP-P5HTAK5\\SQLEXPRESS;Integrated Security=True;Trust Server Certificate=True";
        public static HttpListener listener;
        public static string url = "http://127.0.0.1:8000/";
        public static int pageViews = 0;
        public static int requestCount = 0;
        public static string pageData =
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            "    <title>HttpListener Example</title>" +
            "  </head>" +
            "  <body>" +
            "    <p>Page Views: {0}</p>" +
            "    <form method=\"post\" action=\"shutdown\">" +
            "      <input type=\"submit\" value=\"Shutdown\" {1}>" +
            "    </form>" +
            "  </body>" +
            "</html>";


        public static async Task HandleIncomingConnections()
        {
            
            bool runServer = true;
            UsersContext usersContext = new UsersContext();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            string sql = "Select * From Users";
            SqlCommand cmd = new SqlCommand(sql, connection);
            using SqlDataReader sqlReader = cmd.ExecuteReader();
            while (sqlReader.Read())
            {
                usersContext.Users.Add(new User() { UserName = sqlReader.GetString(0), Password = sqlReader.GetString(1), Salt = sqlReader.GetString(2), Email = sqlReader.GetString(3) });
            }
            cmd.Dispose();
            sqlReader.Dispose();
            
            
            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                byte[] data = Encoding.UTF8.GetBytes("{\"Error\":\"Unknown Error\"}");
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                // Print out some info about the request
                Console.WriteLine("Request #: {0}", ++requestCount);
                Console.WriteLine(req.Url.ToString());
                Console.WriteLine(req.HttpMethod);
                Console.WriteLine(req.UserHostName);
                Console.WriteLine(req.UserAgent);
                Console.WriteLine();

                // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
                if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/shutdown"))
                {
                    Console.WriteLine("Shutdown requested");
                    data = Encoding.UTF8.GetBytes("Attempting shutdown");
                    runServer = false;
                }
                else if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/login"))
                {
                    bool userAuthed = false;
                    string userInfo = "";
                    string username = "";
                    string password = "";
                    using (StreamReader reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    {
                        userInfo = reader.ReadToEnd();
                        var infoArr = userInfo.Split('|');
                        username = infoArr[0];
                        password = infoArr[1];
                    }
                    var result = from u in usersContext.Users where u.UserName == username select u;
                    if (result.Count() > 0)
                    {
                        string hashedPass = BCrypt.Net.BCrypt.HashPassword(password, result.First().Salt);

                        if (result.Count() == 1 && result.First().Password == hashedPass)
                        {
                            data = Encoding.UTF8.GetBytes("{\"Token\":" + "\"" + BCrypt.Net.BCrypt.HashPassword(username + password + DateTime.Now.ToString(), BCrypt.Net.BCrypt.GenerateSalt()) + "\"}");
                        }
                        else
                        {
                            data = Encoding.UTF8.GetBytes("{\"Error\":\"Incorrect username or password\"}");
                        }
                        //todo: retrieve salt from db for user, hash pass with salt, compare with hashed and salted pass in db, return a user token if match
                        Console.WriteLine(userInfo);
                    }
                    else
                    {
                        data = Encoding.UTF8.GetBytes("{\"Error\":\"Incorrect username or password\"}");
                    }


                }
                else if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/register"))
                {
                    string salt = BCrypt.Net.BCrypt.GenerateSalt(13);
                    string username = "";
                    string password = "";
                    string email = "";
                    using (StreamReader reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    {
                        string info = reader.ReadToEnd();
                        var infoArr = info.Split('|');
                        username = infoArr[0];
                        password = infoArr[1];
                        email = infoArr[2];
                    }
                    var result = from u in usersContext.Users where u.UserName == username select u;
                    if (result.Count() == 0)
                    {
                        string hash = BCrypt.Net.BCrypt.HashPassword(password, salt);
                        //todo: store hashed salted pass and salt in db
                        SqlTransaction transaction = connection.BeginTransaction();
                        try
                        {
                            string command = $"Insert Into Users(Username, Password, Salt, Email) Values(\'{username}\', \'{hash}\', \'{salt}\', \'{email}\')";
                            new SqlCommand(command, connection, transaction).ExecuteNonQuery();
                            transaction.Commit();
                            data = Encoding.UTF8.GetBytes("{\"Success\":\"User created\"}");
                            usersContext.Add(new User() { Email = email, UserName = username, Password = hash, Salt = salt });
                        }
                        catch (SqlException e)
                        {
                            transaction.Rollback();
                            data = Encoding.UTF8.GetBytes("{\"Error\":\"Error adding user\"}");
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                    {
                        data = Encoding.UTF8.GetBytes("{\"Error\":\"User already exists\"}");
                    }
                }
                else
                {
                    data = Encoding.UTF8.GetBytes("{\"Error\":\"Unknown path\"}");
                }

                    // Make sure we don't increment the page views counter if `favicon.ico` is requested
                    if (req.Url.AbsolutePath != "/favicon.ico")
                    pageViews += 1;

                // Write the response info
                string disableSubmit = !runServer ? "disabled" : "";
                
                resp.ContentType = "text";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.Headers.Add("Access-Control-Allow-Origin:*");
                resp.Headers.Add("Access-Control-Allow-Methods:*");
                resp.Headers.Add("Access-Control-Allow-Headers:*");
                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
            connection.Close();
        }


        public static void Main(string[] args)
        {
            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            listener.Close();
        }
    }
}