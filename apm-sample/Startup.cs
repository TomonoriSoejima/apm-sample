using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.NetCoreAll;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using System.Net.Http;
using System.Threading.Tasks;
using System.Transactions;
using apm_sample;
using Microsoft.AspNetCore.Http;

namespace apm_sample
{
    public class Startup
    {
        private readonly string _connectionString = "Server=localhost;Database=MySampleDatabase;User Id=sa;Password=tomonori_987##123;"; // Define global variable
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddRazorPages(); // Add this line to use Razor Pages
            services.AddHttpClient(); // Add this line to register HttpClient
            services.AddLogging(config =>
            {
                config.AddConsole();
                config.AddDebug();
            }); // Add this line to configure logging
            // Remove any references to DatabaseSettings
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAllElasticApm(Configuration); // Add this line to use Elastic APM

            app.UseStaticFiles(); // Add this line to serve static files

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages(); // Add this line to map Razor Pages
                endpoints.MapGet("/", async context =>
                {
                    context.Response.Redirect("/index.html");
                });

                endpoints.MapGet("/akito", HelloAkito);
                endpoints.MapGet("/hello", Hello);
                endpoints.MapGet("/select", SelectSomething);
                endpoints.MapGet("/insert", async context =>
                {
                    string firstName = context.Request.Query["firstName"];
                    string lastName = context.Request.Query["lastName"];
                    string ageString = context.Request.Query["age"];
                    string email = context.Request.Query["email"];

                    if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(ageString) || string.IsNullOrEmpty(email))
                    {
                        // Insert dummy data if any parameter is missing
                        firstName = "John";
                        lastName = "Doe";
                        ageString = "30";
                        email = "john.doe@example.com";
                    }

                    if (!int.TryParse(ageString, out int age))
                    {
                        await context.Response.WriteAsync("Invalid age parameter.");
                        return;
                    }

                    bool insertSuccess = await InsertSomething(context, firstName, lastName, age, email);
                    if (insertSuccess)
                    {
                        await context.Response.WriteAsync("Insert successful.");
                    }
                    else
                    {
                        await context.Response.WriteAsync("Insert failed.");
                    }
                });

                endpoints.MapGet("/call_to_another_service", async context =>
                {
                    var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
                    await CallAnotherService(context, httpClientFactory);
                });
            });
        }

        async Task Hello(HttpContext context)
        {
            try
            {
                //application code that is captured as a transaction
                await context.Response.WriteAsync("Hello Hello!");
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
            }
        }

        async Task HelloAkito(HttpContext context)
        {
            var transaction = Elastic.Apm.Agent
                       .Tracer.StartTransaction("MyTransaction", ApiConstants.TypeRequest);

            try
            {
                //application code that is captured as a transaction
                await context.Response.WriteAsync("Hello Akito Soejima!");
                var just_wait = true;
            }
            catch (Exception e)
            {
                transaction.CaptureException(e);
                throw;
            }
            finally
            {
                transaction.End();
            }
        }

        private async Task SelectSomething(HttpContext context)
        {
            ITransaction transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            var asyncResult = await transaction.CaptureSpan("Select FROM users", ApiConstants.TypeDb, async (s) =>
            {
                context.RequestServices.GetRequiredService<ILogger<Startup>>().LogInformation("Using connection string: {ConnectionString}", _connectionString);

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    string sqlQuery = "SELECT * FROM users";
                    context.RequestServices.GetRequiredService<ILogger<Startup>>().LogInformation("Executing query: {SqlQuery}", sqlQuery);

                    using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            var result = new System.Text.StringBuilder();
                            result.Append("<html><body><table border='1'><tr><th>ID</th><th>FirstName</th><th>LastName</th><th>Age</th><th>Email</th></tr>");

                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                string firstName = reader.GetString(1);
                                string lastName = reader.GetString(2);
                                int age = reader.GetInt32(3);
                                string email = reader.GetString(4);

                                result.Append($"<tr><td>{id}</td><td>{firstName}</td><td>{lastName}</td><td>{age}</td><td>{email}</td></tr>");
                            }

                            result.Append("</table></body></html>");
                            await context.Response.WriteAsync(result.ToString());
                        }
                    }
                }

                await Task.Delay(500); //sample async code

                return 42;
            });
        }

        private async Task<bool> InsertSomething(HttpContext context, string firstName, string lastName, int age, string email)
        {
            ITransaction transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            var asyncResult = await transaction.CaptureSpan("INSERT INTO users", ApiConstants.TypeDb, async (s) =>
            {
                context.RequestServices.GetRequiredService<ILogger<Startup>>().LogInformation("Using connection string: {ConnectionString}", _connectionString);

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    string sqlQuery = "INSERT INTO usersj (FirstName, LastName, Age, Email) VALUES (@FirstName, @LastName, @Age, @Email)";
                    context.RequestServices.GetRequiredService<ILogger<Startup>>().LogInformation("Executing query: {SqlQuery}", sqlQuery);

                    using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                    {
                        command.Parameters.AddWithValue("@FirstName", firstName);
                        command.Parameters.AddWithValue("@LastName", lastName);
                        command.Parameters.AddWithValue("@Age", age);
                        command.Parameters.AddWithValue("@Email", email);

                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        return rowsAffected > 0;
                    }
                }
            });

            return asyncResult;
        }

        private async Task CallAnotherService(HttpContext context, IHttpClientFactory httpClientFactory)
        {
            var transaction = Elastic.Apm.Agent.Tracer.StartTransaction("MyTransaction", ApiConstants.TypeRequest);

            try
            {
                var client = httpClientFactory.CreateClient();
                var response = await client.GetAsync("http://localhost:7000/from_another_service");

                if (!response.IsSuccessStatusCode)
                {
                    await context.Response.WriteAsync($"Error: {response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                await context.Response.WriteAsync(content);
            }
            catch (HttpRequestException e)
            {
                transaction.CaptureException(e);
                await context.Response.WriteAsync($"Request error: {e.Message}");
            }
            catch (Exception e)
            {
                transaction.CaptureException(e);
                throw;
            }
            finally
            {
                transaction.End();
            }
        }
    }
}

     
    
