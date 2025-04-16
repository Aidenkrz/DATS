using DATS.Web.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DATS.Web.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {

        _basePath = configuration["FileStorage:BasePath"]
            ?? throw new InvalidOperationException("FileStorage:BasePath configuration is missing.");



        if (!Path.IsPathRooted(_basePath))
        {


            var contentRoot = Directory.GetCurrentDirectory();
            _basePath = Path.GetFullPath(Path.Combine(contentRoot, _basePath));
        }

        _logger = logger;


        try
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Created base storage directory: {BasePath}", _basePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or access base storage directory: {BasePath}", _basePath);

            throw new InvalidOperationException($"Could not create or access the storage directory: {_basePath}", ex);
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, Guid ticketId, Guid attachmentId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is null or empty.", nameof(file));
        }


        var ticketDirectory = Path.Combine(_basePath, ticketId.ToString());
        Directory.CreateDirectory(ticketDirectory);


        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{attachmentId}{fileExtension}";
        var relativePath = Path.Combine(ticketId.ToString(), uniqueFileName);
        var fullPath = Path.Combine(_basePath, relativePath);

        _logger.LogInformation("Attempting to save file to: {FullPath}", fullPath);

        try
        {
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            _logger.LogInformation("Successfully saved file: {RelativePath}", relativePath);
            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} to {FullPath}", file.FileName, fullPath);

            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch {}
            }
            throw;
        }
    }

    public Task<bool> DeleteFileAsync(string storedFilePath)
    {
        if (string.IsNullOrEmpty(storedFilePath))
        {
            _logger.LogWarning("Attempted to delete file with null or empty path.");
            return Task.FromResult(true);
        }

        var fullPath = GetPhysicalPath(storedFilePath);

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file: {FullPath}", fullPath);
                return Task.FromResult(true);
            }
            else
            {
                _logger.LogWarning("Attempted to delete file that does not exist: {FullPath}", fullPath);
                return Task.FromResult(true);
            }
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FullPath}", fullPath);
            return Task.FromResult(false);
        }
    }

    public string GetPhysicalPath(string storedFilePath)
    {

        return Path.Combine(_basePath, storedFilePath);
    }
}