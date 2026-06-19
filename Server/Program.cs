using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Auth;
using Server.Beacons;
using Server.Core;
using Server.Listeners;
using Server.Payloads;
using Server.Tasks;

namespace Server;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddDbContext<CrystalDb>(db =>
        {
            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = "data.db",
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            db.UseSqlite(csb.ConnectionString);
        });
        
        builder.Services.AddMediatR(m =>
        {
            // not sure how legit this is
            builder.Logging.AddFilter("LuckyPennySoftware.MediatR.License", LogLevel.None);
            
            m.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });
        
        builder.Services.AddScoped(typeof(IRepository<>), typeof(CrystalRepository<>));
        builder.Services.AddSingleton(typeof(IBroker<>),  typeof(CrystalBroker<>));
        
        builder.Services.AddSingleton<ListenerManager>();
        builder.Services.AddSingleton<TaskBroker>();
        builder.Services.AddScoped<C2Bridge>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "CrystalC2",
                    ValidAudience = "CrystalC2",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddGrpc();

        var app = builder.Build();

        await using var scope = app.Services.CreateAsyncScope();
        await using var db = scope.ServiceProvider.GetRequiredService<CrystalDb>();

        var created = await db.Database.EnsureCreatedAsync();

        if (!created)
        {
            // start saved listeners
            var listeners = await db.Listeners.ToListAsync();
            var manager = scope.ServiceProvider.GetRequiredService<ListenerManager>();
            var factory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            foreach (var listener in listeners)
            {
                try
                {
                    listener.Start(factory);
                    manager.Add(listener);
                }
                catch
                {
                    // pokemon
                }
            }
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGrpcService<AuthService>();
        app.MapGrpcService<ListenerService>();
        app.MapGrpcService<PayloadService>();
        app.MapGrpcService<BeaconService>();
        app.MapGrpcService<TaskService>();
        
        app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation("Server listening on {Url}", string.Join(", ", app.Urls)));
        app.Lifetime.ApplicationStopping.Register(() => app.Logger.LogInformation("Server shutting down..."));

        await app.RunAsync();
    }
}