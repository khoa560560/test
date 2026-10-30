using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace StudentHousing.Api;
[ApiController,Route("api/amenities")]
public class AmenitiesController(HousingDb db):ControllerBase { [HttpGet] public async Task<IActionResult> List()=>Ok(await db.Amenities.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Icon}).ToListAsync()); }
[Authorize(Roles="Tenant"),ApiController,Route("api/favorites")]
public class FavoritesController(HousingDb db):ControllerBase { Guid Id=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); [HttpGet] public async Task<IActionResult> List()=>Ok(await db.Favorites.AsNoTracking().Where(x=>x.UserId==Id&&x.Property.Status==PropertyStatus.Active).Select(x=>new{x.Property.Id,x.Property.Title,x.Property.Price,x.Property.Area,x.Property.District}).ToListAsync()); [HttpPost("{propertyId:int}")] public async Task<IActionResult> Add(int propertyId){if(!await db.Properties.AnyAsync(x=>x.Id==propertyId&&x.Status==PropertyStatus.Active))return NotFound();if(await db.Favorites.AnyAsync(x=>x.UserId==Id&&x.PropertyId==propertyId))return NoContent();db.Favorites.Add(new Favorite{UserId=Id,PropertyId=propertyId});await db.SaveChangesAsync();return NoContent();} [HttpDelete("{propertyId:int}")] public async Task<IActionResult> Remove(int propertyId){var f=await db.Favorites.FindAsync(Id,propertyId);if(f==null)return NotFound();db.Favorites.Remove(f);await db.SaveChangesAsync();return NoContent();}}
public record ViewingInput(int PropertyId,DateTime RequestedAt,string? Note);
[Authorize,ApiController,Route("api/viewing-requests")]
public class ViewingRequestsController(HousingDb db):ControllerBase {Guid Id=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [Authorize(Roles="Tenant"),HttpPost] public async Task<IActionResult> Create(ViewingInput d){if(d.RequestedAt.Kind!=DateTimeKind.Utc||d.RequestedAt<=DateTime.UtcNow)return BadRequest(new{error="Thời gian phải ở tương lai và theo UTC"});if(!await db.Properties.AnyAsync(x=>x.Id==d.PropertyId&&x.Status==PropertyStatus.Active))return NotFound();if(await db.ViewingRequests.AnyAsync(x=>x.TenantId==Id&&x.PropertyId==d.PropertyId&&x.RequestedAt==d.RequestedAt&&x.Status==RequestStatus.Pending))return Conflict(new{error="Lịch hẹn đã tồn tại"});var r=new ViewingRequest{TenantId=Id,PropertyId=d.PropertyId,RequestedAt=d.RequestedAt,Note=d.Note};db.ViewingRequests.Add(r);await db.SaveChangesAsync();return Created($"/api/viewing-requests/{r.Id}",new{r.Id,r.Status});}
 [Authorize(Roles="Tenant"),HttpGet("my")] public async Task<IActionResult> My()=>Ok(await db.ViewingRequests.AsNoTracking().Where(x=>x.TenantId==Id).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.PropertyId,PropertyTitle=x.Property.Title,x.RequestedAt,x.Status,x.Note}).ToListAsync());
 [Authorize(Roles="Tenant"),HttpPatch("{id:int}/cancel")] public async Task<IActionResult> Cancel(int id){var r=await db.ViewingRequests.SingleOrDefaultAsync(x=>x.Id==id&&x.TenantId==Id);if(r==null)return NotFound();if(r.Status!=RequestStatus.Pending)return Conflict();r.Status=RequestStatus.Cancelled;r.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return NoContent();}
}
[Authorize(Roles="Landlord"),ApiController,Route("api/landlord")]
public class LandlordController(HousingDb db):ControllerBase {Guid Id=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [HttpGet("properties")] public async Task<IActionResult> Properties()=>Ok(await db.Properties.AsNoTracking().Where(x=>x.LandlordId==Id).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.Title,x.Price,x.Area,x.Status,x.CreatedAt}).ToListAsync());
 [HttpGet("properties/{id:int}")] public async Task<IActionResult> Property(int id){var p=await db.Properties.AsNoTracking().Where(x=>x.Id==id&&x.LandlordId==Id).Select(x=>new{x.Id,x.Title,x.Description,x.Address,x.District,x.Ward,x.Price,x.Area,AmenityIds=x.PropertyAmenities.Select(a=>a.AmenityId),Images=x.Images.Select(i=>new{i.Id,i.ImageUrl})}).FirstOrDefaultAsync();return p==null?NotFound():Ok(p);}
 [HttpGet("viewing-requests")] public async Task<IActionResult> Requests()=>Ok(await db.ViewingRequests.AsNoTracking().Where(x=>x.Property.LandlordId==Id).OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.PropertyId,PropertyTitle=x.Property.Title,x.RequestedAt,x.Status,TenantName=x.Tenant.FullName}).ToListAsync());
 [HttpPatch("viewing-requests/{id:int}/approve")] public Task<IActionResult> Approve(int id)=>Decide(id,RequestStatus.Approved);
 [HttpPatch("viewing-requests/{id:int}/reject")] public Task<IActionResult> Reject(int id)=>Decide(id,RequestStatus.Rejected);
 async Task<IActionResult> Decide(int id,RequestStatus status){var r=await db.ViewingRequests.SingleOrDefaultAsync(x=>x.Id==id&&x.Property.LandlordId==Id);if(r==null)return NotFound();if(r.Status!=RequestStatus.Pending)return Conflict();r.Status=status;r.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return NoContent();}
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(new{total=await db.Properties.CountAsync(x=>x.LandlordId==Id),active=await db.Properties.CountAsync(x=>x.LandlordId==Id&&x.Status==PropertyStatus.Active),rented=await db.Properties.CountAsync(x=>x.LandlordId==Id&&x.Status==PropertyStatus.Rented),pendingRequests=await db.ViewingRequests.CountAsync(x=>x.Property.LandlordId==Id&&x.Status==RequestStatus.Pending)});
}
[Authorize(Roles="Admin"),ApiController,Route("api/admin")]
public class AdminController(HousingDb db):ControllerBase {
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard()=>Ok(new{users=await db.Users.CountAsync(),landlords=await db.Users.CountAsync(x=>x.Role==Role.Landlord),properties=await db.Properties.CountAsync(),pending=await db.Properties.CountAsync(x=>x.Status==PropertyStatus.Pending),active=await db.Properties.CountAsync(x=>x.Status==PropertyStatus.Active)});
 [HttpGet("properties/pending")] public async Task<IActionResult> Pending()=>Ok(await db.Properties.AsNoTracking().Where(x=>x.Status==PropertyStatus.Pending).OrderBy(x=>x.CreatedAt).Select(x=>new{x.Id,x.Title,x.Price,x.Area,x.District,x.CreatedAt,x.LandlordId}).ToListAsync());
 [HttpGet("properties")] public async Task<IActionResult> Properties()=>Ok(await db.Properties.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.Title,x.Status,x.Price,x.District}).ToListAsync());
 [HttpPatch("properties/{id:int}/approve")] public Task<IActionResult> Approve(int id)=>Decide(id,PropertyStatus.Active);
 [HttpPatch("properties/{id:int}/reject")] public Task<IActionResult> Reject(int id)=>Decide(id,PropertyStatus.Rejected);
 async Task<IActionResult> Decide(int id,PropertyStatus status){var p=await db.Properties.FindAsync(id);if(p==null)return NotFound();if(p.Status!=PropertyStatus.Pending)return Conflict();p.Status=status;p.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();return NoContent();}
 [HttpGet("users")] public async Task<IActionResult> Users()=>Ok(await db.Users.AsNoTracking().Select(x=>new{x.Id,x.FullName,x.Email,x.Role,x.IsActive}).ToListAsync());
 [HttpPatch("users/{id:guid}/lock")] public Task<IActionResult> Lock(Guid id)=>SetActive(id,false);
 [HttpPatch("users/{id:guid}/unlock")] public Task<IActionResult> Unlock(Guid id)=>SetActive(id,true);
 async Task<IActionResult> SetActive(Guid id,bool active){var u=await db.Users.FindAsync(id);if(u==null)return NotFound();if(u.Role==Role.Admin)return Conflict();u.IsActive=active;await db.SaveChangesAsync();return NoContent();}
 [HttpPost("amenities")] public async Task<IActionResult> AddAmenity(Amenity d){if(string.IsNullOrWhiteSpace(d.Name))return BadRequest();var a=new Amenity{Name=d.Name.Trim(),Icon=d.Icon};db.Amenities.Add(a);await db.SaveChangesAsync();return Ok(new{a.Id,a.Name,a.Icon});}
 [HttpPut("amenities/{id:int}")] public async Task<IActionResult> EditAmenity(int id,Amenity d){var a=await db.Amenities.FindAsync(id);if(a==null)return NotFound();a.Name=d.Name.Trim();a.Icon=d.Icon;await db.SaveChangesAsync();return NoContent();}
 [HttpDelete("amenities/{id:int}")] public async Task<IActionResult> DeleteAmenity(int id){var a=await db.Amenities.FindAsync(id);if(a==null)return NotFound();if(await db.PropertyAmenities.AnyAsync(x=>x.AmenityId==id))return Conflict();db.Amenities.Remove(a);await db.SaveChangesAsync();return NoContent();}
}
