using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quaver.Scripting;

[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct SharedMemoryChannel
{
    [FieldOffset(0)] public ulong Offset;
    [FieldOffset(8)] public ulong Write;
    [FieldOffset(16)] public ulong Read;

    public void Reset()
    {
        Write = 0;
        Read = 0;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct SharedMemoryLayout
{
    public const uint ConstMagic = 0x95abe799;
    public const uint ConstVersion = 1;
    [FieldOffset(0)] public uint Magic;
    [FieldOffset(4)] public uint Version;
    [FieldOffset(8)] public ulong ChannelSize;
    [FieldOffset(16)] public SharedMemoryChannel HostToWorker;
    [FieldOffset(40)] public SharedMemoryChannel WorkerToHost;
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
    private readonly UnmanagedMemoryManager<byte> _hostToWorkerPayload;
    private readonly UnmanagedMemoryManager<byte> _workerToHostPayload;
    private byte* _pointer;

    public SharedMemory(MemoryMappedFile file, int size)
    {
        var layoutSize = Unsafe.SizeOf<SharedMemoryLayout>();
        if (size <= layoutSize)
        {
            throw new InvalidOperationException("Insufficient size allocated to shared memory");
        }

        _accessor = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);
        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _pointer = ptr + _accessor.PointerOffset;
        _layout = (SharedMemoryLayout*)_pointer;

        SharedMemoryLayout.HostToWorker.Reset();
        SharedMemoryLayout.WorkerToHost.Reset();

        const int align = 8;
        var channelSize = ((size - layoutSize) / 2) & ~(align - 1);
        SharedMemoryLayout.ChannelSize = (ulong)channelSize;
        SharedMemoryLayout.HostToWorker.Offset = 0;
        SharedMemoryLayout.WorkerToHost.Offset = SharedMemoryLayout.ChannelSize;
        _hostToWorkerPayload =
            new UnmanagedMemoryManager<byte>(
                _pointer + layoutSize + SharedMemoryLayout.HostToWorker.Offset,
                channelSize);
        _workerToHostPayload =
            new UnmanagedMemoryManager<byte>(
                _pointer + layoutSize + SharedMemoryLayout.WorkerToHost.Offset,
                channelSize);

        // Set it last so everything is initialized before rust checks
        SharedMemoryLayout.Magic = SharedMemoryLayout.ConstMagic;
        SharedMemoryLayout.Version = SharedMemoryLayout.ConstVersion;
    }

    private ref SharedMemoryLayout SharedMemoryLayout =>
        ref Unsafe.AsRef<SharedMemoryLayout>(_layout);

    public int Read(Span<byte> output)
    {
        ref var channel = ref SharedMemoryLayout.WorkerToHost;
        var buffer = _workerToHostPayload.GetSpan();

        if (buffer.IsEmpty || output.IsEmpty)
            return 0;

        var writePosition = Interlocked.Read(ref channel.Write);
        var readPosition = Interlocked.Read(ref channel.Read);
        var available = Math.Min(SaturatingSubtract(writePosition, readPosition), buffer.Length);
        var count = Math.Min(available, output.Length);

        CopyFromRing(output, buffer, checked((int)(readPosition % (ulong)buffer.Length)), count);
        ulong newValue = readPosition + (uint)count;
        Interlocked.Exchange(ref channel.Read, newValue);

        return count;
    }

    public int Write(ReadOnlySpan<byte> input)
    {
        ref var channel = ref SharedMemoryLayout.HostToWorker;
        var buffer = _hostToWorkerPayload.GetSpan();

        if (buffer.IsEmpty || input.IsEmpty)
            return 0;

        var writePosition = Interlocked.Read(ref channel.Write);
        var readPosition = Interlocked.Read(ref channel.Read);
        var used = Math.Min(SaturatingSubtract(writePosition, readPosition), buffer.Length);
        var available = buffer.Length - used;
        var count = Math.Min(available, input.Length);

        CopyToRing(buffer, checked((int)(writePosition % (ulong)buffer.Length)), input[..count]);
        ulong newValue = writePosition + (uint)count;
        Interlocked.Exchange(ref channel.Write, newValue);

        return count;
    }

    private static int SaturatingSubtract(ulong left, ulong right)
    {
        if (left <= right)
            return 0;

        var difference = left - right;
        return difference > int.MaxValue ? int.MaxValue : (int)difference;
    }

    private static void CopyFromRing(Span<byte> output, ReadOnlySpan<byte> buffer, int start,
        int count)
    {
        var firstCount = Math.Min(count, buffer.Length - start);
        var secondCount = count - firstCount;

        buffer.Slice(start, firstCount).CopyTo(output);
        buffer[..secondCount].CopyTo(output[firstCount..]);
    }

    private static void CopyToRing(Span<byte> buffer, int start, ReadOnlySpan<byte> input)
    {
        var firstCount = Math.Min(input.Length, buffer.Length - start);
        var secondCount = input.Length - firstCount;

        input[..firstCount].CopyTo(buffer[start..]);
        input.Slice(firstCount, secondCount).CopyTo(buffer);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        ((IDisposable)_hostToWorkerPayload).Dispose();
        ((IDisposable)_workerToHostPayload).Dispose();

        if (_pointer != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _pointer = null;
        }

        _accessor.Dispose();
    }
}