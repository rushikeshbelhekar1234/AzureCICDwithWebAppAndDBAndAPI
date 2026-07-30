using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WebApplication2.Repositories;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;

namespace WebApplication2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly UserRepository _users;

        public AuthController(IConfiguration cfg, UserRepository users)
        {
            _cfg = cfg;
            _users = users;
        }

        [HttpPost("token")]
        public async Task<IActionResult> Token([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Invalid credentials");

            var user = await _users.GetByUsernameAsync(req.Username!);
            if (user == null)
                return Unauthorized();

            // Verify password using BCrypt. Catch hash/format errors and return Unauthorized rather than 500.
            try
            {
                if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                    return Unauthorized();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Password verification failed for user '{req.Username}': {ex.Message}");
                return Unauthorized();
            }

            var jwt = _cfg.GetSection("Jwt");
            var key = jwt.GetValue<string>("Key")!;
            var issuer = jwt.GetValue<string>("Issuer");
            var audience = jwt.GetValue<string>("Audience");
            var expiresMinutes = jwt.GetValue<int>("ExpiresMinutes");

            var claims = new[] { new Claim(ClaimTypes.Name, user.Username ?? string.Empty), new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddMinutes(expiresMinutes), signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { access_token = tokenString, token_type = "Bearer", expires_in = expiresMinutes * 60 });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Username and password are required.");

            var existing = await _users.GetByUsernameAsync(req.Username!);
            if (existing != null) return Conflict("Username already exists.");

            // Hash password and create user
            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            var user = new WebApplication2.Models.User { Username = req.Username, PasswordHash = hash };
            var id = await _users.CreateAsync(user);
            return CreatedAtAction(null, new { id }, new { id, username = req.Username });
        }

        public class LoginRequest
        {
            public string? Username { get; set; }
            public string? Password { get; set; }
        }
    }
}
