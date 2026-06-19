using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CrystalC2.Auth;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;

namespace Server.Auth;

public sealed class AuthService(
    IConfiguration configuration,
    ILogger<AuthService> logger)
    : AuthProtoService.AuthProtoServiceBase
{
    public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var password = configuration["Auth:Password"];

        if (string.IsNullOrEmpty(request.Username) || request.Password != password)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid credentials"));
        }

        var token = GenerateToken(request.Username);
        
        logger.LogInformation("User {USER} authenticated", request.Username);
        
        return Task.FromResult(new LoginResponse { Token = token });
    }

    private string GenerateToken(string username)
    {
        var key = configuration["Jwt:Key"]!;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "CrystalC2",
            audience: "CrystalC2",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}