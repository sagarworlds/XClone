using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using XCloneAPI.Data;
using XCloneAPI.DTOs;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly PasswordHasher<User> _passwordHasher = new();

        // Verified against when the account doesn't exist, so a missing user takes as long as a wrong password.
        private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString("N"));

        public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                    throw new ArgumentException("Email already registered");

                if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                    throw new ArgumentException("Username already taken");

                // Create new user
                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName,
                    Bio = request.Bio ?? string.Empty
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User registered: {user.Email}");

                return new AuthResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl,
                    Token = GenerateJwtToken(user),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryMinutes"]))
                };
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during registration: {ex.Message}");
                throw new Exception("Registration failed", ex);
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email == request.UsernameOrEmail || u.Username == request.UsernameOrEmail);
                if (user == null)
                {
                    _passwordHasher.VerifyHashedPassword(new User(), DummyHash, request.Password);
                    throw new UnauthorizedAccessException("Invalid username/email or password");
                }

                if (!await VerifyPasswordAsync(user, request.Password))
                    throw new UnauthorizedAccessException("Invalid username/email or password");

                _logger.LogInformation($"User logged in: {user.Email}");

                return new AuthResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl,
                    Token = GenerateJwtToken(user),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryMinutes"]))
                };
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during login: {ex.Message}");
                throw new Exception("Login failed", ex);
            }
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email),
                new System.Security.Claims.Claim("username", user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Accounts created before the switch to PBKDF2 hold an unsalted SHA-256 hash (base64 of 32 bytes = 44 chars).
        // Those are verified once and upgraded to the current hash format on a successful login.
        private async Task<bool> VerifyPasswordAsync(User user, string password)
        {
            if (IsLegacyHash(user.PasswordHash))
            {
                var expected = Convert.FromBase64String(user.PasswordHash);
                var actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                    return false;

                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Upgraded password hash for user {user.Id}");
                return true;
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
                return false;

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                await _context.SaveChangesAsync();
            }

            return true;
        }

        private static bool IsLegacyHash(string hash)
        {
            var buffer = new byte[32];
            return hash.Length == 44 && Convert.TryFromBase64String(hash, buffer, out var written) && written == 32;
        }
    }
}
