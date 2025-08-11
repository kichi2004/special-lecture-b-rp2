using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using final;
using static JitShim;

TinyJit.DemoBf();
;

internal static partial class NativeMethods
{

    [DllImport("libdl", SetLastError = true)]
    public static extern IntPtr dlopen(string? filename, int flags);

    [DllImport("libdl", SetLastError = true)]
    public static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libdl", SetLastError = true)]
    public static extern int dlclose(IntPtr handle);

    // dlopenのフラグ (macOS用)
    public const int RTLD_LAZY = 0x1;
    public const int RTLD_NOW = 0x2;

    // 自分自身のプロセスを指す特別なハンドル
    public static readonly IntPtr RTLD_DEFAULT = -2;
}

internal static partial class JitShim
{
    [LibraryImport("libjitshim", EntryPoint = "jit_alloc")]
    internal static partial IntPtr Alloc(nuint size);

    [LibraryImport("libjitshim", EntryPoint = "jit_free")]
    internal static partial int Free(nint ptr, nuint size);

    [LibraryImport("libjitshim", EntryPoint = "jit_make_executable")]
    internal static partial int MakeExecutable(nint ptr, nuint size);

    [LibraryImport("libjitshim", EntryPoint = "get_errno")]
    internal static partial int GetErrno();

    [LibraryImport("libjitshim", EntryPoint = "add")]
    internal static partial int Add(int a, int b);

    internal static SortedDictionary<int, string> ErrnoMap = new()
    {
        { 0, "Error" },
        { 1, "EPERM" },
        { 2, "ENOENT" },
        { 3, "ESRCH" },
        { 4, "EINTR" },
        { 5, "EIO" },
        { 6, "ENXIO" },
        { 7, "E2BIG" },
        { 8, "ENOEXEC" },
        { 9, "EBADF" },
        { 10, "ECHILD" },
        { 11, "EDEADLK" },
        { 12, "ENOMEM" },
        { 13, "EACCES" },
        { 14, "EFAULT" },
        { 15, "ENOTBLK" },
        { 16, "EBUSY" },
        { 17, "EEXIST" },
        { 18, "EXDEV" },
        { 19, "ENODEV" },
        { 20, "ENOTDIR" },
        { 21, "EISDIR" },
        { 22, "EINVAL" },
        { 23, "ENFILE" },
        { 24, "EMFILE" },
        { 25, "ENOTTY" },
        { 26, "ETXTBSY" },
        { 27, "EFBIG" },
        { 28, "ENOSPC" },
        { 29, "ESPIPE" },
        { 30, "EROFS" },
        { 31, "EMLINK" },
        { 32, "EPIPE" },
        { 33, "EDOM" },
        { 34, "ERANGE" },
        { 35, "EAGAIN" },
        { 36, "EINPROGRESS" },
        { 37, "EALREADY" },
        { 38, "ENOTSOCK" },
        { 39, "EDESTADDRREQ" },
        { 40, "EMSGSIZE" },
        { 41, "EPROTOTYPE" },
        { 42, "ENOPROTOOPT" },
        { 43, "EPROTONOSUPPORT" },
        { 44, "ESOCKTNOSUPPORT" },
        { 45, "ENOTSUP" },
        { 46, "EPFNOSUPPORT" },
        { 47, "EAFNOSUPPORT" },
        { 48, "EADDRINUSE" },
        { 49, "EADDRNOTAVAIL" },
        { 50, "ENETDOWN" },
        { 51, "ENETUNREACH" },
        { 52, "ENETRESET" },
        { 53, "ECONNABORTED" },
        { 54, "ECONNRESET" },
        { 55, "ENOBUFS" },
        { 56, "EISCONN" },
        { 57, "ENOTCONN" },
        { 58, "ESHUTDOWN" },
        { 60, "ETIMEDOUT" },
        { 61, "ECONNREFUSED" },
        { 62, "ELOOP" },
        { 63, "ENAMETOOLONG" },
        { 64, "EHOSTDOWN" },
        { 65, "EHOSTUNREACH" },
        { 66, "ENOTEMPTY" },
        { 67, "EPROCLIM" },
        { 68, "EUSERS" },
        { 69, "EDQUOT" },
        { 70, "ESTALE" },
        { 72, "EBADRPC" },
        { 73, "ERPCMISMATCH" },
        { 74, "EPROGUNAVAIL" },
        { 75, "EPROGMISMATCH" },
        { 76, "EPROCUNAVAIL" },
        { 77, "ENOLCK" },
        { 78, "ENOSYS" },
        { 79, "EFTYPE" },
        { 80, "EAUTH" },
        { 81, "ENEEDAUTH" },
        { 82, "EPWROFF" },
        { 83, "EDEVERR" },
        { 84, "EOVERFLOW" },
        { 85, "EBADEXEC" },
        { 86, "EBADARCH" },
        { 87, "ESHLIBVERS" },
        { 88, "EBADMACHO" },
        { 89, "ECANCELED" },
        { 90, "EIDRM" },
        { 91, "ENOMSG" },
        { 92, "EILSEQ" },
        { 93, "ENOATTR" },
        { 94, "EBADMSG" },
        { 95, "EMULTIHOP" },
        { 96, "ENODATA" },
        { 97, "ENOLINK" },
        { 98, "ENOSR" },
        { 99, "ENOSTR" },
        { 100, "EPROTO" },
        { 101, "ETIME" },
        { 102, "EOPNOTSUPP" },
    };
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int MyFunc();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int BinOp(int a, int b);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void BrainfuckRunnerDelegate(IntPtr ptr);

// 0x0131 C9C0
public static class TinyJit
{
    internal static readonly byte[] RetNumber =
    [
        0x00, 0x38, 0x99, 0x52,  // MOVZ w0, 0xc9c0
        0x20, 0x26, 0xA0, 0x72,  // MOVK w0, 0x0131, LSL 16
        0xC0, 0x03, 0x5F, 0xD6,  // RET
    ];

    internal static readonly byte[] Add =
    [
        0x20, 0x00, 0x00, 0x0B, // ADD w0, w1, w0
        0xC0, 0x03, 0x5F, 0xD6, // RET
    ];

    public static (T, nuint) CreateFuncFromMachineCode<T>(ReadOnlySpan<byte> machineCode) where T : Delegate
    {
        nuint size = (nuint) machineCode.Length;
        var mem = Alloc(size);
        if (mem == IntPtr.Zero)
        {
            Console.Error.WriteLine($"Failed jit_alloc (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");
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
                throw new InvalidOperationException($"Failed jit_make_executable (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");

            return (Marshal.GetDelegateForFunctionPointer<T>(mem), size);
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            if (Free(mem, size) != 0)
            {
                Console.WriteLine($"Failed jit_free (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");
            }
            throw;
        }
    }

    public static void FreeFunction<T>(T func, nuint size) where T : Delegate
    {
        var ptr = Marshal.GetFunctionPointerForDelegate(func);
        if (Free(ptr, size) != 0)
        {
            Console.WriteLine($"Failed jit_free (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");
        }
    }

    public static void Demo()
    {
        Console.WriteLine("Starting...");
        var (retNumberFn, retSz) = CreateFuncFromMachineCode<MyFunc>(RetNumber);
        Console.WriteLine($"ret_number() = {retNumberFn()}");
        FreeFunction(retNumberFn, retSz);
        
        var (addFn, addSz) = CreateFuncFromMachineCode<BinOp>(Add);
        Console.WriteLine($"add(1, 2) = {addFn(1, 2)}");
        Console.WriteLine($"add(314159265, -256) = {addFn(314159265, -256)}");
        FreeFunction(addFn, addSz);
    }

    public static void DemoBf()
    {
        const string brainfuckSource = @"++++++[->++++>>+>+>-<<<<<]>[<++++>>+++>++++>>+++>+++++>+++++>>>>>>++>>++<
<<<<<<<<<<<<<-]<++++>+++>-->+++>->>--->++>>>+++++[->++>++<<]<<<<<<<<<<[->
-[>>>>>>>]>[<+++>.>.>>>>..>>>+<]<<<<<-[>>>>]>[<+++++>.>.>..>>>+<]>>>>+<-[
<<<]<[[-<<+>>]>>>+>+<<<<<<[->>+>+>-<<<<]<]>>[[-]<]>[>>>[>.<<.<<<]<[.<<<<]
>]>.<<<<<<<<<<<]";
            
        // 20 KB
        nuint size = 1024 * 20;
        var tape = new byte[30000];
        var mem = Alloc(size);
        if (mem == IntPtr.Zero)
        {
            Console.Error.WriteLine($"Failed jit_alloc (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");
            throw new OutOfMemoryException("Failed jit_alloc");
        }
        
        try
        {
            unsafe
            {
                fixed (byte* tapePtr = tape)
                {
                    Console.WriteLine($"Pointer to tape = {(IntPtr) tapePtr:X}");
                    var byteCode = BrainfuckParser.MakeArm64(BrainfuckParser.Parse(brainfuckSource), new IntPtr(tapePtr));
                    Console.WriteLine(string.Join("", byteCode.Select(x => $"{x:X2}")));
                    
                    var span = new Span<byte>((void*)mem, (int) size);
                    byteCode.CopyTo(span);
                    if (MakeExecutable(mem, size) != 0)
                        throw new InvalidOperationException($"Failed jit_make_executable (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");


                    var fn = Marshal.GetDelegateForFunctionPointer<BrainfuckRunnerDelegate>(mem);
                    fn(new IntPtr(tapePtr));
                    FreeFunction(fn, size);

                    Console.WriteLine("Tape: ");
                    for (int i = 0; i < tape.Length; i++)
                    {
                        if (tape[i] == 0) break;
                        Console.WriteLine($"{tape[i]} ({(char) tape[i]})");
                    }
                    Console.WriteLine();
                }
            }

        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            if (Free(mem, size) != 0)
            {
                Console.WriteLine($"Failed jit_free (errno = {GetErrno()} {ErrnoMap[GetErrno()]})");
            }
            throw;
        }

    }
}

[SimpleJob(RuntimeMoniker.Net90)]
public class Measurement01
{
    [Params(-1189309492, 1133828965)]
    public int A;
    
    [Params(-1751846844, 393380237)]
    public int B;

    private BinOp? _op;
    private nuint _opSize;

    [Benchmark]
    public int PInvoke()
    {
        return Add(A, B);
    }

    [GlobalSetup(Target = nameof(NativeReuse))]
    public void NativeSetup()
    {
        var (op, opSize) = TinyJit.CreateFuncFromMachineCode<BinOp>(TinyJit.Add);
        _op = op;
        _opSize = opSize;
    }

    [Benchmark]
    public int NativeInit()
    {
        var (op, size) = TinyJit.CreateFuncFromMachineCode<BinOp>(TinyJit.Add);
        int ret = op(A, B);
        TinyJit.FreeFunction(op, size);
        return ret;
    }

    [GlobalCleanup(Target = nameof(NativeReuse))]
    public void NativeCleanup()
    {
        TinyJit.FreeFunction(_op!, _opSize);
    }

    [Benchmark]
    public int NativeReuse()
    {
        return _op!(A, B);
    }

    [Benchmark]
    public int Csharp()
    {
        return A + B;
    }
}
