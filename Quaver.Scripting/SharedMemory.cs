using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quaver.Scripting;

[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct SharedMemoryChannel
{
    [FieldOffset(0)] public IntPtr Offset;
    [FieldOffset(8)] public IntPtr Write;
    [FieldOffset(16)] public IntPtr Read;
}

[StructLayout(LayoutKind.Explicit, Size = 56)]
public struct SharedMemoryLayout
{
    [FieldOffset(0)] public Int32 Magic;
    [FieldOffset(4)] public Int32 Version;
    [FieldOffset(8)] public SharedMemoryChannel HostToWorker;
    [FieldOffset(32)] public SharedMemoryChannel WorkerToHost;
}

public unsafe sealed class UnmanagedMemoryManager<T>(T* pointer, int length) : MemoryManager<T>
    where T : unmanaged
{
    public override Span<T> GetSpan() => new(pointer, length);

    public override MemoryHandle Pin(int elementIndex = 0) => new(pointer + elementIndex);

    public override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}

public unsafe class SharedMemory : IDisposable
{
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly SharedMemoryLayout* _layout;
    private readonly UnmanagedMemoryManager<byte> _payload;

    public SharedMemory(MemoryMappedFile file, int size)
    {
        var layoutSize = Unsafe.SizeOf<SharedMemoryLayout>();
        Debug.Assert(size > layoutSize);
        _accessor = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);
        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _layout = (SharedMemoryLayout*)ptr;
        _payload = new UnmanagedMemoryManager<byte>(ptr + layoutSize, size);
    }

    private Span<byte> Payload => _payload.GetSpan();

    private ref SharedMemoryLayout SharedMemoryLayout =>
        ref Unsafe.AsRef<SharedMemoryLayout>(_layout);

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _accessor.Dispose();
        ((IDisposable)_payload).Dispose();
    }
}