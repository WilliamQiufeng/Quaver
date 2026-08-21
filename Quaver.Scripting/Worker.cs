using System.Diagnostics;
using System.Text;

namespace Quaver.Scripting;

public class Worker
{
    private const int Size = 1024 * 1024;
    private readonly AnonymousSharedMemory _sharedMemory;
    private readonly WorkerSharedMemory _workerSharedMemory;
    private readonly Process _process;
    private readonly byte[] _readBuffer = new byte[Size / 2];

    private Worker()
    {
        _sharedMemory = AnonymousSharedMemory.Create(Size);
        _workerSharedMemory = new WorkerSharedMemory(_sharedMemory.MemoryMappedFile, Size);

        var psi = new ProcessStartInfo("worker")
        {
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(_sharedMemory.Identifier);
        psi.ArgumentList.Add(Size.ToString());
        _process = Process.Start(psi) ?? throw new InvalidOperationException();
    }

    public static Worker Create()
    {
        return new Worker();
    }

    public void Update()
    {
        var size = _workerSharedMemory.Read(_readBuffer);
        if (size > 0)
        {
            var str = Encoding.ASCII.GetString(_readBuffer, 0, size);
            Console.WriteLine(str);
        }
    }
}