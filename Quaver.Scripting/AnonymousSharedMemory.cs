using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Quaver.Scripting;

public enum AnonymousSharedMemoryKind
{
    WindowsNamed,
    LinuxDevShm,
    MacOsPosixShm,
}

public sealed partial class AnonymousSharedMemory : IDisposable
{
    private readonly Action? _cleanup;

    private AnonymousSharedMemory(
        MemoryMappedFile memoryMappedFile,
        AnonymousSharedMemoryKind kind,
        long size,
        string identifier,
        Action? cleanup)
    {
        MemoryMappedFile = memoryMappedFile;
        Kind = kind;
        Size = size;
        Identifier = identifier;
        _cleanup = cleanup;
    }

    public MemoryMappedFile MemoryMappedFile { get; }

    public AnonymousSharedMemoryKind Kind { get; }

    public long Size { get; }

    /// <summary>
    /// Windows: memory-map name. Linux: /dev/shm path. macOS: POSIX shm name.
    /// </summary>
    public string Identifier { get; }

    public static AnonymousSharedMemory Create(long size)
    {
        if (OperatingSystem.IsWindows())
            return CreateWindows(size);

        if (OperatingSystem.IsLinux())
            return CreateLinux(size);

        if (OperatingSystem.IsMacOS())
            return CreateMacOs(size);

        throw new PlatformNotSupportedException("Unsupported shared memory platform");
    }

    private static AnonymousSharedMemory CreateWindows(long size)
    {
        ValidateSize(size);

        var name = $@"Local\quaver-ipc-{Guid.NewGuid():N}";
        var memoryMappedFile = MemoryMappedFile.CreateNew(
            name,
            size,
            MemoryMappedFileAccess.ReadWrite,
            MemoryMappedFileOptions.None,
            HandleInheritability.None);

        return new AnonymousSharedMemory(
            memoryMappedFile,
            AnonymousSharedMemoryKind.WindowsNamed,
            size,
            name,
            cleanup: null);
    }

    private static AnonymousSharedMemory CreateLinux(long size)
    {
        ValidateSize(size);

        var path = Path.Combine("/dev/shm", $"quaver-ipc-{Guid.NewGuid():N}");
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);

        stream.SetLength(size);

        var memoryMappedFile = MemoryMappedFile.CreateFromFile(
            stream,
            mapName: null,
            capacity: size,
            access: MemoryMappedFileAccess.ReadWrite,
            inheritability: HandleInheritability.None,
            leaveOpen: false);

        return new AnonymousSharedMemory(
            memoryMappedFile,
            AnonymousSharedMemoryKind.LinuxDevShm,
            size,
            path,
            cleanup: () => TryDelete(path));
    }

    private static AnonymousSharedMemory CreateMacOs(long size)
    {
        ValidateSize(size);

        var name = $"/quaver-ipc-{Guid.NewGuid():N}";
        var fd = MacOs.shm_open(name, MacOs.O_RDWR | MacOs.O_CREAT | MacOs.O_EXCL,
            MacOs.UserReadWrite);
        if (fd < 0)
            throw CreateLastPInvokeException("shm_open");

        SafeFileHandle? handle = null;
        var fdTransferredToStream = false;
        try
        {
            if (MacOs.ftruncate(fd, size) != 0)
                throw CreateLastPInvokeException("ftruncate");

            handle = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
            using var stream = new FileStream(handle, FileAccess.ReadWrite);
            fdTransferredToStream = true;
            handle = null;

            var memoryMappedFile = MemoryMappedFile.CreateFromFile(
                stream,
                mapName: null,
                capacity: size,
                access: MemoryMappedFileAccess.ReadWrite,
                inheritability: HandleInheritability.None,
                leaveOpen: false);

            return new AnonymousSharedMemory(
                memoryMappedFile,
                AnonymousSharedMemoryKind.MacOsPosixShm,
                size,
                name,
                cleanup: () => MacOs.shm_unlink(name));
        }
        catch
        {
            handle?.Dispose();
            if (!fdTransferredToStream)
                MacOs.close(fd);
            MacOs.shm_unlink(name);
            throw;
        }
    }

    public void Dispose()
    {
        MemoryMappedFile.Dispose();
        _cleanup?.Invoke();
    }

    private static void ValidateSize(long size)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size,
                "Shared memory size must be positive");
    }

    private static IOException CreateLastPInvokeException(string operation)
    {
        var errno = Marshal.GetLastPInvokeError();
        return new IOException(
            string.Format(CultureInfo.InvariantCulture, "{0} failed with errno {1}", operation,
                errno));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static partial class MacOs
    {
        public const int O_RDONLY = 0x0000;
        public const int O_RDWR = 0x0002;
        public const int O_CREAT = 0x0200;
        public const int O_EXCL = 0x0800;
        public const int UserReadWrite = 0x0180;

        [LibraryImport("libc", SetLastError = true)]
        public static partial int shm_open(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            int oflag,
            int mode);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int shm_unlink(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int ftruncate(int fd, long length);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int close(int fd);
    }
}