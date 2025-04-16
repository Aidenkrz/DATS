using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace DATS.Web.Interfaces;

public interface IFileStorageService
{







    Task<string> SaveFileAsync(IFormFile file, Guid ticketId, Guid attachmentId);






    Task<bool> DeleteFileAsync(string storedFilePath);






    string GetPhysicalPath(string storedFilePath);
}