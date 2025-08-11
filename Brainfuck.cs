namespace final;

public enum BrainfuckToken
{
    IncrementPtr,
    DecrementPtr,
    IncrementValue,
    DecrementValue,
    OutputValue,
    InputValue,
    LoopStart,
    LoopEnd,
}

public static class BrainfuckParser
{
    public static BrainfuckToken[] Parse(string code)
    {
        return code.Select(c => c switch
            {
                '>' => BrainfuckToken.IncrementPtr,
                '<' => BrainfuckToken.DecrementPtr,
                '+' => BrainfuckToken.IncrementValue,
                '-' => BrainfuckToken.DecrementValue,
                '.' => BrainfuckToken.OutputValue,
                ',' => BrainfuckToken.InputValue,
                '[' => BrainfuckToken.LoopStart,
                ']' => BrainfuckToken.LoopEnd,
                _ => null as BrainfuckToken?,
            }).Where(x => x != null)
            .Select(x => (BrainfuckToken)x!)
            .ToArray();
    }
    
    public static byte[] MakeArm64(BrainfuckToken[] tokens, nint basePtr)
    {
        var addrPutchar = NativeMethods.dlsym(NativeMethods.RTLD_DEFAULT, "putchar");
        
        if (addrPutchar == IntPtr.Zero) throw new Exception("_putchar not found");
        
        var addrGetchar = NativeMethods.dlsym(NativeMethods.RTLD_DEFAULT, "getchar");
        if (addrGetchar == IntPtr.Zero) throw new Exception("_getchar not found");
        
        const int ptrRegister = 9;
        const int workRegister = 10;
        var codes = new List<byte>();
        codes.AddRange(Arm64Jit.StpPreIndex64((1 << 7) - 2, 30, Arm64Jit.SP, 29));
        codes.AddRange(Arm64Jit.MovToFromSp64(29, Arm64Jit.SP));
        codes.AddRange(Arm64Jit.MovRegister64(ptrRegister, 0));
        
        var loopStack = new Stack<int>();

        
        foreach (BrainfuckToken token in tokens)
        {
            int pc = codes.Count;
            switch (token)
            {
                case BrainfuckToken.IncrementPtr:
                    codes.AddRange(Arm64Jit.AddImmediate64(1, ptrRegister, ptrRegister));
                    break;
                case BrainfuckToken.DecrementPtr:
                    codes.AddRange(Arm64Jit.SubImmediate64(1, ptrRegister, ptrRegister));
                    break;
                case BrainfuckToken.IncrementValue:
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister, 0));
                    codes.AddRange(Arm64Jit.AddImmediate(1, workRegister, workRegister));
                    codes.AddRange(Arm64Jit.StrbImmediateUnsignedOffset(workRegister, ptrRegister, 0));
                    break;
                case BrainfuckToken.DecrementValue:
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister, 0));
                    codes.AddRange(Arm64Jit.SubImmediate(1, workRegister, workRegister));
                    codes.AddRange(Arm64Jit.StrbImmediateUnsignedOffset(workRegister, ptrRegister, 0));
                    break;
                case BrainfuckToken.OutputValue:
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(0, ptrRegister, 0));
                    codes.AddRange(Arm64Jit.Movz64(16, (ushort)(addrPutchar & 0xFFFF), 0));
                    codes.AddRange(Arm64Jit.Movk64(16, (ushort) ((addrPutchar >> 16) & 0xFFFF), 1));
                    codes.AddRange(Arm64Jit.Movk64(16, (ushort) ((addrPutchar >> 32) & 0xFFFF), 2));
                    codes.AddRange(Arm64Jit.Movk64(16, (ushort) ((addrPutchar >> 48) & 0xFFFF), 3));
                    codes.AddRange(Arm64Jit.Blr(16));
                    break;
                case BrainfuckToken.InputValue:
                    codes.AddRange(Arm64Jit.Movz64(16, (ushort)(addrGetchar & 0xFFFF), 0));
                    codes.AddRange(Arm64Jit.Movk64(16, (ushort) ((addrGetchar >> 16) & 0xFFFF), 1));
                    codes.AddRange(Arm64Jit.Movk64(16, (ushort) ((addrGetchar >> 32) & 0xFFFF), 2));
                    codes.AddRange(Arm64Jit.Movk64(16, (ushort) ((addrGetchar >> 48) & 0xFFFF), 3));
                    codes.AddRange(Arm64Jit.Blr(16));
                    codes.AddRange(Arm64Jit.StrbImmediateUnsignedOffset(0, ptrRegister, 0));
                    break;
                case BrainfuckToken.LoopStart:
                    loopStack.Push(pc);
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister, 0));
                    codes.AddRange(Arm64Jit.CmpImmediate(workRegister, 0));
                    codes.AddRange(new byte[4]);
                    break;
                case BrainfuckToken.LoopEnd:
                    var loopStart = loopStack.Pop();
                    // 無条件分岐
                    var backJumpOffset = (loopStart - codes.Count) / 4;
                    codes.AddRange(Arm64Jit.B((1 << 26) + backJumpOffset));

                    var bCondOffset = (codes.Count - (loopStart + 8)) / 4;
                    var jump = Arm64Jit.BCond(bCondOffset & ((1 << 19) - 1), Arm64Jit.BranchCondition.EQ);
                    codes[loopStart + 8] = jump[0];
                    codes[loopStart + 9] = jump[1];
                    codes[loopStart + 10] = jump[2];
                    codes[loopStart + 11] = jump[3];
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        codes.AddRange(Arm64Jit.Ldp64(29, 30, Arm64Jit.SP, 2, false));
        codes.AddRange(Arm64Jit.Ret());

        return codes.ToArray();
    }
}

public class BrainfuckRunner
{
    private const int ArraySize = 65535;
    private readonly byte[] _array = new byte[ArraySize];
    private int _ptr = 0;
    
    public byte[] Array => _array.ToArray();

    public BrainfuckRunner()
    {
    }

    public void Run(BrainfuckToken[] tokens, byte[]? input = null)
    {
        int inputPtr = 0;
        for (int index = 0; index < tokens.Length; index++)
        {
            BrainfuckToken token = tokens[index];
            switch (token)
            {
                case BrainfuckToken.IncrementValue:
                    ++_array[_ptr];
                    break;
                case BrainfuckToken.DecrementValue:
                    --_array[_ptr];
                    break;
                case BrainfuckToken.IncrementPtr:
                    ++_ptr;
                    break;
                case BrainfuckToken.DecrementPtr:
                    --_ptr;
                    break;
                case BrainfuckToken.OutputValue:
                    Console.Write((char)_array[_ptr]);
                    break;
                case BrainfuckToken.InputValue:
                    _array[_ptr] = input is null ? (byte)Console.Read() : input[inputPtr++];
                    break;
                case BrainfuckToken.LoopStart when _array[_ptr] != 0:
                    continue;
                case BrainfuckToken.LoopStart:
                {
                    int depth = 1;
                    for (int i = index + 1; i < tokens.Length; i++)
                    {
                        switch (tokens[i])
                        {
                            case BrainfuckToken.LoopStart:
                                ++depth;
                                break;
                            case BrainfuckToken.LoopEnd:
                                --depth;
                                break;
                        }

                        if (depth == 0)
                        {
                            index = i;
                            break;
                        }
                    }

                    break;
                }
                case BrainfuckToken.LoopEnd when _array[_ptr] == 0:
                    continue;
                case BrainfuckToken.LoopEnd:
                {
                    int depth = 1;
                    for (int i = index - 1; i >= 0; i--)
                    {
                        switch (tokens[i])
                        {
                            case BrainfuckToken.LoopStart:
                                --depth;
                                break;
                            case BrainfuckToken.LoopEnd:
                                ++depth;
                                break;
                        }

                        if (depth == 0)
                        {
                            index = i;
                            break;
                        }
                    }

                    break;
                }
            }
        }
    }
}
