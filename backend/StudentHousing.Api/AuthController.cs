using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace StudentHousing.Api;
public record RegisterDto(string FullName,string Email,string Password,string Role);
public record LoginDto(string Email,string Password);
public record ProfileDto(string FullName,string? PhoneNumber);
[ApiController,Route("api/auth"),EnableRateLimiting("auth")]
public class AuthController(HousingDb db,IConfiguration config):ControllerBase {
 [HttpPost("register")] public async Task<IActionResult> Register(RegisterDto d){if(string.IsNullOrWhiteSpace(d.FullName)||!System.Net.Mail.MailAddress.TryCreate(d.Email,out _)||d.Password.Length<8||!Enum.TryParse<Role>(d.Role,true,out var role)||role==Role.Admin)return BadRequest(new{error="Thông tin đăng ký không hợp lệ"});var email=d.Email.Trim().ToLowerInvariant();if(await db.Users.AnyAsync(x=>x.Email==email))return Conflict(new{error="Email đã tồn tại"});var u=new User{FullName=d.FullName.Trim(),Email=email,PasswordHash=BCrypt.Net.BCrypt.HashPassword(d.Password),Role=role};db.Users.Add(u);await db.SaveChangesAsync();return Created("/api/auth/me",new{u.Id,u.FullName,u.Email,u.Role});}
 [HttpPost("login")] public async Task<IActionResult> Login(LoginDto d){var u=await db.Users.SingleOrDefaultAsync(x=>x.Email==d.Email.Trim().ToLower());if(u==null||!u.IsActive||!BCrypt.Net.BCrypt.Verify(d.Password,u.PasswordHash))return Unauthorized(new{error="Email hoặc mật khẩu không đúng"});var claims=new[]{new Claim(ClaimTypes.NameIdentifier,u.Id.ToString()),new Claim(ClaimTypes.Role,u.Role.ToString()),new Claim(ClaimTypes.Name,u.FullName)};var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));var token=new JwtSecurityToken(config["Jwt:Issuer"]??"ctustay",config["Jwt:Audience"]??"ctustay-client",claims,expires:DateTime.UtcNow.AddHours(8),signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256));return Ok(new{token=new JwtSecurityTokenHandler().WriteToken(token),user=new{u.Id,u.FullName,u.Email,u.Role}});}
 [Authorize,HttpPut("me")] public async Task<IActionResult> UpdateProfile(ProfileDto d){if(string.IsNullOrWhiteSpace(d.FullName)||d.FullName.Length>120||d.PhoneNumber?.Length>20)return BadRequest(new{error="Hồ sơ không hợp lệ"});var id=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);var u=await db.Users.FindAsync(id);if(u==null||!u.IsActive)return Unauthorized();u.FullName=d.FullName.Trim();u.PhoneNumber=d.PhoneNumber?.Trim();await db.SaveChangesAsync();return Ok(new{u.Id,u.FullName,u.Email,u.PhoneNumber,u.Role});}
 [Authorize,HttpGet("me")] public async Task<IActionResult> Me(){var id=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);var u=await db.Users.Where(x=>x.Id==id).Select(x=>new{x.Id,x.FullName,x.Email,x.PhoneNumber,x.Role,x.IsActive}).FirstOrDefaultAsync();return u==null?Unauthorized():Ok(u);}
}
