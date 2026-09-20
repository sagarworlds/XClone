namespace XCloneAPI.Services
{
    // Where uploaded images are kept. Only names made by MediaNames are accepted, so nothing can point outside the folder.
    public interface IMediaStorage
    {
        Task SaveAsync(string name, Stream content, CancellationToken cancellationToken);
        bool Exists(string name);

        // Removing a file that is already gone is not an error
        void Delete(string name);
    }

    // Images in a folder on this machine (served from /uploads). Fine for one server; a shared store (S3, Azure Blob)
    // would be another IMediaStorage.
    public sealed class LocalMediaStorage : IMediaStorage
    {
        public string Directory { get; }

        public LocalMediaStorage(string directory)
        {
            Directory = Path.GetFullPath(directory);
            System.IO.Directory.CreateDirectory(Directory);
        }

        public async Task SaveAsync(string name, Stream content, CancellationToken cancellationToken)
        {
            // CreateNew: a name is never reused, so an upload can never overwrite another one
            await using var file = new FileStream(PathFor(name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, cancellationToken);
        }

        public bool Exists(string name) => File.Exists(PathFor(name));

        public void Delete(string name) => File.Delete(PathFor(name));

        private string PathFor(string name)
        {
            if (!MediaNames.IsValidName(name))
                throw new ArgumentException("Not a name made for an uploaded image", nameof(name));

            return Path.Combine(Directory, name);
        }
    }
}
