using System.Threading.RateLimiting;
using Africa.Server.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Africa.Server
{
    public class Startup
    {
        private const string CorsPolicy = "ClientOrigins";
        public const string ApplicationsRateLimit = "Applications";

        public IConfiguration Configuration { get; }

        // Use the host's configuration so appsettings.{Environment}.json,
        // user secrets and environment variables are all picked up.
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            string conn = Configuration.GetConnectionString("ApplicationTable")
                ?? throw new InvalidOperationException(
                    "Connection string 'ApplicationTable' is not configured. " +
                    "Set it in appsettings or via the ConnectionStrings__ApplicationTable environment variable.");

            // A serverless Azure SQL database that is resuming from auto-pause can hold
            // the login open for up to about a minute. With the default 30 s connect
            // timeout the login times out (error -2), which EF does not retry, so the
            // request fails with a 500. Give the connection enough time to wait it out.
            var connBuilder = new SqlConnectionStringBuilder(conn);
            int minConnectTimeout = Configuration.GetValue("Database:MinConnectTimeoutSeconds", 90);
            if (connBuilder.ConnectTimeout < minConnectTimeout)
            {
                connBuilder.ConnectTimeout = minConnectTimeout;
            }
            conn = connBuilder.ConnectionString;

            // Retry transient SQL errors, e.g. 40613 while a serverless Azure SQL
            // database is resuming from auto-pause (can take about a minute).
            services.AddDbContext<ApplicationsContext>(opt => opt.UseSqlServer(conn, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

            // Only needed if the client is hosted on a different origin than the API.
            // In production the client is served by this app, so the list can stay empty.
            var allowedOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()));

            // Azure App Service sits behind a front end, so read the real visitor IP
            // from X-Forwarded-For. Only the last hop (added by Azure) is trusted,
            // so a client can't spoof its IP to dodge the rate limit.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Limit how often one visitor (client IP) can submit the application form,
            // so the database can't be flooded by a script.
            int permitLimit = Configuration.GetValue("RateLimiting:Applications:PermitLimit", 5);
            int windowMinutes = Configuration.GetValue("RateLimiting:Applications:WindowMinutes", 10);
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(ApplicationsRateLimit, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = TimeSpan.FromMinutes(windowMinutes),
                            QueueLimit = 0,
                        }));
            });

            services.AddHostedService<ApplicationsCleanupService>();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            if (Configuration.GetValue("Database:MigrateOnStartup", true))
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<ApplicationsContext>().Database.Migrate();
            }

            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Serve the built Vue client (copied into wwwroot on publish).
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCors(CorsPolicy);
            app.UseRateLimiter();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("/index.html");
            });
        }
    }
}
