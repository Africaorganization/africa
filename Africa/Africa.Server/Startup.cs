using Africa.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Africa.Server
{
    public class Startup
    {
        private const string CorsPolicy = "ClientOrigins";

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
            services.AddDbContext<ApplicationsContext>(opt => opt.UseSqlServer(conn));

            // Only needed if the client is hosted on a different origin than the API.
            // In production the client is served by this app, so the list can stay empty.
            var allowedOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()));

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("/index.html");
            });
        }
    }
}
