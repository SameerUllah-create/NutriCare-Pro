namespace NutriCare.Infrastructure.Storage;

using Microsoft.Extensions.Hosting;
using NutriCare.Domain.Interfaces;

/// <summary>Private development-only storage. Files are served exclusively through authorized API handlers.</summary>
public sealed class DevelopmentFileStorageService : IBlobStorageService
{
    private readonly string root;
    public DevelopmentFileStorageService(IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Local file storage is available only in Development");
        root=Path.GetFullPath(Path.Combine(environment.ContentRootPath,".local","private-storage"));
        Directory.CreateDirectory(root);
    }
    public async Task<BlobUploadResult> UploadAsync(Stream content,string fileName,string contentType,string folder,CancellationToken cancellationToken=default)
    {
        var stored=Guid.NewGuid().ToString("N")+Path.GetExtension(fileName).ToLowerInvariant();
        var blob=$"{folder.Trim('/')}/{DateTime.UtcNow:yyyy/MM}/{stored}";
        var path=Resolve("private",blob);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { await using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);await content.CopyToAsync(file,cancellationToken); }
        catch { if(File.Exists(path))File.Delete(path);throw; }
        return new BlobUploadResult("private",blob,stored,new Uri("https://development-storage.invalid/"+Uri.EscapeDataString(blob)));
    }
    public Task<BlobDownloadResult> DownloadAsync(string containerName,string blobName,string contentType,CancellationToken cancellationToken=default)
    {
        var file=new FileStream(Resolve(containerName,blobName),FileMode.Open,FileAccess.Read,FileShare.Read);
        return Task.FromResult(new BlobDownloadResult(file,contentType));
    }
    public Task DeleteAsync(string containerName,string blobName,CancellationToken cancellationToken=default)
    {
        File.Delete(Resolve(containerName,blobName));return Task.CompletedTask;
    }
    private string Resolve(string container,string blob)
    {
        if(container!="private" || Path.IsPathRooted(blob) || blob.Contains(':') || blob.Split('/','\\').Any(p=>p is ".." or "."))throw new ArgumentException("Invalid private storage reference");
        var result=Path.GetFullPath(Path.Combine(root,container,blob.Replace('/',Path.DirectorySeparatorChar)));
        if(!result.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Invalid private storage reference");
        return result;
    }
}
