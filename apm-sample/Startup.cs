using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.NetCoreAll;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Transactions;

namespace apm_sample
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAllElasticApm(Configuration); // Add this line to use Elastic APM

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });

                endpoints.MapGet("/maruto", HelloAkito);
                endpoints.MapGet("/hello", Hello);
                endpoints.MapGet("/select", SelectSomething);
                //endpoints.MapGet("/list", ListSystemMember);
                endpoints.MapGet("/list", Dummy);


                endpoints.MapGet("/insert", async context =>
                {
                    string firstName = context.Request.Query["firstName"];
                    string lastName = context.Request.Query["lastName"];
                    int age = int.Parse(context.Request.Query["age"]);
                    string email = context.Request.Query["email"];

                    await InsertSomething(context, firstName, lastName, age, email);
                });

            });



        }

        async Task Hello(HttpContext context)
        {


            try
            {
                //application code that is captured as a transaction
                await context.Response.WriteAsync("Hello Akito!");
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
                await context.Response.WriteAsync("Hello Akito!");
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
            var asyncResult = await transaction.CaptureSpan("Select FROM MySampleTable", ApiConstants.TypeDb, async (s) =>
            {


                string connectionString = "Server=localhost;Database=;User Id=sa;Password=Nzn6M_3M-X1s;";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string sqlQuery = "SELECT * FROM MySampleTable";

                    using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                string firstName = reader.GetString(1);
                                string lastName = reader.GetString(2);
                                int age = reader.GetInt32(3);
                                string email = reader.GetString(4);

                                Console.WriteLine($"ID: {id}, FirstName: {firstName}, LastName: {lastName}, Age: {age}, Email: {email}");
                            }
                        }
                    }

                }

                await Task.Delay(500); //sample async code

               

                return 42;
            });


        }

        private async Task InsertSomething(HttpContext context, string firstName, string lastName, int age, string email)
        {
            ITransaction transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            var asyncResult = await transaction.CaptureSpan("INSERT INTO MySampleTable", ApiConstants.TypeDb, async (s) =>
            {
                string connectionString = "Server=localhost;Database=;User Id=sa;Password=Nzn6M_3M-X1s;";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string sqlQuery = "INSERT INTO MySampleTable (FirstName, LastName, Age, Email) VALUES (@FirstName, @LastName, @Age, @Email)";

                    using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                    {
                        command.Parameters.AddWithValue("@FirstName", firstName);
                        command.Parameters.AddWithValue("@LastName", lastName);
                        command.Parameters.AddWithValue("@Age", age);
                        command.Parameters.AddWithValue("@Email", email);

                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine("Insert successful.");
                        }
                        else
                        {
                            Console.WriteLine("Insert failed.");

                        }
                    }
                }

                await Task.Delay(100);  // Sample async code

                return 42;
            });
        }


        public static List<SystemMemberDto> ListSystemMember()
        {
            List<SystemMemberDto> result = new List<SystemMemberDto>();

            string connectionString = "Server=localhost;Database=;User Id=sa;Password=Nzn6M_3M-X1s;";

            string query = "SELECT RoleName, EmpNo, UserId, UpdateTime FROM SystemMembers";

            //var transaction = Agent.Tracer.StartTransaction("TASK", ApiConstants.TypeRequest);


            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                try
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SystemMemberDto member = new SystemMemberDto
                        {
                            RoleName = reader["RoleName"] as string,
                            EmpNo = reader["EmpNo"] as string,
                            UserId = reader["UserId"] as string,


                        };

                        if (DateTime.TryParse(reader["UpdateTime"].ToString(), out DateTime updateTime))
                        {
                            member.UpdateTime = updateTime;
                        }
                        result.Add(member);
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    // Handle exception
                    Console.WriteLine(ex.Message);
                    //transaction.CaptureException(ex);
                }

                //transaction.End();
            }
            return result;
        }


        public static void Dummy()
        {

            var transaction = Agent.Tracer.StartTransaction("TASK - dummy", ApiConstants.TypeRequest);
            try
            {
                ListSystemMember();
            }
            catch (Exception ex)
            {
                transaction.CaptureException(ex);
            }
            finally
            {
                transaction.End();
            }


        }
    }
}



