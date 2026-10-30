using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudentHousing.Api;

var builder=WebApplication.CreateBuilder(args);
var connection=builder.Configuration.GetConnectionString("Housing") ?? throw new InvalidOperationException("ConnectionStrings__Housing is required");
var jwtKey=builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt__Key is required");
if(Encoding.UTF8.GetByteCount(jwtKey)<32) throw new InvalidOperationException("Jwt__Key must have at least 32 bytes");
builder.Services.AddDbContext<HousingDb>(o=>o.UseSqlServer(connection));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{o.TokenValidationParameters=new(){ValidateIssuer=true,ValidateAudience=true,ValidateIssuerSigningKey=true,ValidateLifetime=true,ValidIssuer=builder.Configuration["Jwt:Issuer"]??"ctustay",ValidAudience=builder.Configuration["Jwt:Audience"]??"ctustay-client",IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),ClockSkew=TimeSpan.Zero};
o.Events=new JwtBearerEvents{OnTokenValidated=async context=>{
 var raw=context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
 if(!Guid.TryParse(raw,out var id)){context.Fail("Invalid user");return;}
 using var scope=context.HttpContext.RequestServices.CreateScope();
 var db=scope.ServiceProvider.GetRequiredService<HousingDb>();
 if(!await db.Users.AsNoTracking().AnyAsync(x=>x.Id==id&&x.IsActive))context.Fail("Inactive user");
}};});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(o=>o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o=>o.AddPolicy("client",p=>p.WithOrigins(builder.Configuration["ClientOrigin"]??"http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("auth",x=>{x.PermitLimit=10;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;}));
var app=builder.Build();
app.UseExceptionHandler(a=>a.Run(async c=>{c.Response.StatusCode=500;await c.Response.WriteAsJsonAsync(new{error="Lỗi máy chủ"});}));
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.UseCors("client");app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
var adminEmail=builder.Configuration["Bootstrap:AdminEmail"];
var adminPassword=builder.Configuration["Bootstrap:AdminPassword"];
if(!string.IsNullOrWhiteSpace(adminEmail)&&!string.IsNullOrWhiteSpace(adminPassword)){
 if(adminPassword.Length<12) throw new InvalidOperationException("Bootstrap admin password must have at least 12 characters");
 using var scope=app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<HousingDb>();
 var email=adminEmail.Trim().ToLowerInvariant();
 if(!await db.Users.AnyAsync(x=>x.Email==email)){
  db.Users.Add(new User{FullName="Quản trị viên",Email=email,PasswordHash=BCrypt.Net.BCrypt.HashPassword(adminPassword),Role=Role.Admin});await db.SaveChangesAsync();
 }
}
app.Run();
