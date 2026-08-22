using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Quaver.Scripting;

[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct SimplexChannel
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
public struct DuplexLayout
{
    public const uint ConstMagic = 0x95abe799;
    public const uint ConstVersion = 1;
    [FieldOffset(0)] public uint Magic;
    [FieldOffset(4)] public uint Version;
    [FieldOffset(8)] public ulong ChannelSize;
    [FieldOffset(16)] public SimplexChannel HostToWorker;
    [FieldOffset(40)] public SimplexChannel WorkerToHost;
}

public class Duplex : IDisposable
{
    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly unsafe DuplexLayout* _layout;
    private readonly UnmanagedMemoryManager<byte> _hostToWorkerPayload;
    private readonly UnmanagedMemoryManager<byte> _workerToHostPayload;
    private unsafe byte* _pointer;

    public unsafe Duplex(MemoryMappedFile file, int size)
    {
        _file = file;
        var layoutSize = Unsafe.SizeOf<DuplexLayout>();
        if (size <= layoutSize)
        {
            throw new InvalidOperationException("Insufficient size allocated to shared memory");
        }

        _accessor = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite);
        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _pointer = ptr + _accessor.PointerOffset;
        _layout = (DuplexLayout*)_pointer;

        DuplexLayout.HostToWorker.Reset();
        DuplexLayout.WorkerToHost.Reset();

        const int align = 8;
        var channelSize = ((size - layoutSize) / 2) & ~(align - 1);
        DuplexLayout.ChannelSize = (ulong)channelSize;
        DuplexLayout.HostToWorker.Offset = 0;
        DuplexLayout.WorkerToHost.Offset = DuplexLayout.ChannelSize;
        _hostToWorkerPayload =
            new UnmanagedMemoryManager<byte>(
                _pointer + layoutSize + DuplexLayout.HostToWorker.Offset,
                channelSize);
        _workerToHostPayload =
            new UnmanagedMemoryManager<byte>(
                _pointer + layoutSize + DuplexLayout.WorkerToHost.Offset,
                channelSize);

        // Set it last so everything is initialized before rust checks
        DuplexLayout.Magic = DuplexLayout.ConstMagic;
        DuplexLayout.Version = DuplexLayout.ConstVersion;
    }

    private ref DuplexLayout DuplexLayout
    {
        get
        {
            unsafe
            {
                return ref Unsafe.AsRef<DuplexLayout>(_layout);
            }
        }
    }

    public int Read(Span<byte> output)
    {
        ref var channel = ref DuplexLayout.WorkerToHost;
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
        ref var channel = ref DuplexLayout.HostToWorker;
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

        unsafe
        {
            if (_pointer != null)
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _pointer = null;
            }
        }

        _accessor.Dispose();
        _file.Dispose();
    }
}