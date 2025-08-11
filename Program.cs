using System.Runtime.InteropServices;
using static JitShim;

TinyJit.Demo();


internal static partial class JitShim
{
    [LibraryImport("libjitshim", EntryPoint = "jit_alloc")]
    internal static partial IntPtr Alloc(nuint size);
    
    [LibraryImport("libjitshim", EntryPoint = "jit_free")]
    internal static partial int Free(nint ptr);
    
    [LibraryImport("libjitshim", EntryPoint = "jit_make_executable")]
    internal static partial int MakeExecutable(nint ptr, nuint size);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int MyFunc();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int BinOp(int a, int b);

// 0x0131 C9C0
public static class TinyJit
{
    private static readonly byte[] RetNumber =
    [
        0x00, 0x38, 0x99, 0x52,  // MOVZ w0, 0xc9c0
        0x20, 0x26, 0xA0, 0x72,  // MOVK w0, 0x0131, LSL 16
        0xC0, 0x03, 0x5F, 0xD6,  // RET
    ];

    private static readonly byte[] Add =
    [
        0x20, 0x00, 0x00, 0x0B, // ADD w0, w1, w0
        0xC0, 0x03, 0x5F, 0xD6, // RET
    ];

    public static T CreateFuncFromMachineCode<T>(ReadOnlySpan<byte> machineCode) where T : Delegate
    {
        nuint size = (nuint) machineCode.Length;
        var mem = Alloc(size);
        if (mem == IntPtr.Zero)
        {
            throw new OutOfMemoryException("Failed jit_alloc");
        }

        try
        {
            unsafe
            {
                var span = new Span<byte>((void*)mem, (int)size);
                machineCode.CopyTo(span);
            }

            if (MakeExecutable(mem, size) != 0)
                throw new InvalidOperationException("Failed jit_make_executable");

            return Marshal.GetDelegateForFunctionPointer<T>(mem);
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            if (Free(mem) != 0)
            {
                Console.WriteLine("Failed jit_free");
            }
            throw;
        }
    }

    public static void FreeFunction<T>(T func) where T : Delegate
    {
        var ptr = Marshal.GetFunctionPointerForDelegate(func);
        if (Free(ptr) != 0)
        {
            Console.WriteLine("Failed jit_free");
        }
    }

    public static void Demo()
    {
        Console.WriteLine("Starting...");
        var retNumberFn = CreateFuncFromMachineCode<MyFunc>(RetNumber);
        Console.WriteLine($"ret_number() = {retNumberFn()}");
        FreeFunction(retNumberFn);;
        
        var addFn = CreateFuncFromMachineCode<BinOp>(Add);
        Console.WriteLine($"add(1, 2) = {addFn(1, 2)}");
        Console.WriteLine($"add(314159265, -256) = {addFn(314159265, -256)}");
        FreeFunction(addFn);
    }
}
