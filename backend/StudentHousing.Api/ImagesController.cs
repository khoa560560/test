using System.Security.Claims;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace StudentHousing.Api;
[Authorize(Roles="Landlord"),ApiController,Route("api/properties/{propertyId:int}/images")]
public class ImagesController(HousingDb db,IConfiguration config):ControllerBase {
 Guid Id=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 Cloudinary Cloud()=>new(new Account(config["Cloudinary:CloudName"]??throw new InvalidOperationException("Cloudinary cloud missing"),config["Cloudinary:ApiKey"]??throw new InvalidOperationException("Cloudinary key missing"),config["Cloudinary:ApiSecret"]??throw new InvalidOperationException("Cloudinary secret missing")));
 [HttpPost,RequestSizeLimit(5_242_880)] public async Task<IActionResult> Upload(int propertyId,IFormFile file){var p=await db.Properties.Include(x=>x.Images).SingleOrDefaultAsync(x=>x.Id==propertyId&&x.LandlordId==Id);if(p==null)return NotFound();if(p.Images.Count>=10||file.Length==0||file.Length>5_242_880||!new[]{"image/jpeg","image/png","image/webp"}.Contains(file.ContentType))return BadRequest(new{error="Tối đa 10 ảnh JPG, PNG hoặc WebP, mỗi ảnh tối đa 5 MB"});await using var stream=file.OpenReadStream();var result=await Cloud().UploadAsync(new ImageUploadParams{File=new FileDescription(file.FileName,stream),Folder="ctustay/properties"});if(result.Error!=null||result.SecureUrl==null)return StatusCode(502,new{error="Tải ảnh thất bại"});var image=new PropertyImage{PropertyId=propertyId,ImageUrl=result.SecureUrl.ToString(),CloudinaryPublicId=result.PublicId,DisplayOrder=p.Images.Count,IsPrimary=p.Images.Count==0};db.PropertyImages.Add(image);await db.SaveChangesAsync();return Ok(new{image.Id,image.ImageUrl,image.IsPrimary});}
 [HttpDelete("{imageId:int}")] public async Task<IActionResult> Delete(int propertyId,int imageId){var image=await db.PropertyImages.Include(x=>x.Property).SingleOrDefaultAsync(x=>x.Id==imageId&&x.PropertyId==propertyId&&x.Property.LandlordId==Id);if(image==null)return NotFound();var result=await Cloud().DestroyAsync(new DeletionParams(image.CloudinaryPublicId));if(result.Result!="ok"&&result.Result!="not found")return StatusCode(502,new{error="Xóa ảnh thất bại"});db.PropertyImages.Remove(image);await db.SaveChangesAsync();return NoContent();}
}
