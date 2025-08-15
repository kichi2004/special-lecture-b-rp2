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
        
        ushort[] putCharAddrBytes =
        [
            (ushort)(addrPutchar & 0xFFFF),
            (ushort)((addrPutchar >> 16) & 0xFFFF),
            (ushort)((addrPutchar >> 32) & 0xFFFF),
            (ushort)((addrPutchar >> 48) & 0xFFFF)
        ];
        
        var addrGetchar = NativeMethods.dlsym(NativeMethods.RTLD_DEFAULT, "getchar");
        if (addrGetchar == IntPtr.Zero) throw new Exception("_getchar not found");

        ushort[] getCharAddrBytes =
        [
            (ushort)(addrGetchar & 0xFFFF),
            (ushort)((addrGetchar >> 16) & 0xFFFF),
            (ushort)((addrGetchar >> 32) & 0xFFFF),
            (ushort)((addrGetchar >> 48) & 0xFFFF)
        ];
        
        const int ptrRegister = 19;
        const int workRegister = 20;
        var codes = new List<byte>();
        var assemblyCodes = new List<string>();
        
        codes.AddRange(Arm64Jit.Stp64(29, 30, Arm64Jit.SP, (1 << 7) - 4, true));
        assemblyCodes.Add($"STP X29, X30, [SP, #-32]!");
        codes.AddRange(Arm64Jit.Stp64SignedOffset(ptrRegister, workRegister, Arm64Jit.SP, 2));
        assemblyCodes.Add($"STP X{ptrRegister}, X{workRegister}, [SP, #16]");
        codes.AddRange(Arm64Jit.MovToFromSp64(29, Arm64Jit.SP));
        assemblyCodes.Add($"MOV X29, SP");
        codes.AddRange(Arm64Jit.MovRegister64(ptrRegister, 0));
        assemblyCodes.Add($"MOV X{ptrRegister}, X0");
        
        var loopStack = new Stack<int>();

        foreach (BrainfuckToken token in tokens)
        {
            int pc = codes.Count;
            switch (token)
            {
                case BrainfuckToken.IncrementPtr:
                    codes.AddRange(Arm64Jit.AddImmediate64(1, ptrRegister, ptrRegister));
                    assemblyCodes.Add($"ADD X{ptrRegister}, X{ptrRegister}, #1");
                    break;
                case BrainfuckToken.DecrementPtr:
                    codes.AddRange(Arm64Jit.SubImmediate64(1, ptrRegister, ptrRegister));
                    assemblyCodes.Add($"SUB X{ptrRegister}, X{ptrRegister}, #1");
                    break;
                case BrainfuckToken.IncrementValue:
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister));
                    assemblyCodes.Add($"LDRB W{workRegister}, [X{ptrRegister}]");
                    codes.AddRange(Arm64Jit.AddImmediate(1, workRegister, workRegister));
                    assemblyCodes.Add($"ADD W{workRegister}, W{workRegister}, #1");
                    codes.AddRange(Arm64Jit.StrbImmediateUnsignedOffset(workRegister, ptrRegister));
                    assemblyCodes.Add($"STRB W{workRegister}, [X{ptrRegister}]");
                    break;
                case BrainfuckToken.DecrementValue:
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister));
                    assemblyCodes.Add($"LDRB W{workRegister}, [X{ptrRegister}]");
                    codes.AddRange(Arm64Jit.SubImmediate(1, workRegister, workRegister));
                    assemblyCodes.Add($"SUB W{workRegister}, W{workRegister}, #1");
                    codes.AddRange(Arm64Jit.StrbImmediateUnsignedOffset(workRegister, ptrRegister));
                    assemblyCodes.Add($"STRB W{workRegister}, [X{ptrRegister}]");
                    break;
                case BrainfuckToken.OutputValue:
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(0, ptrRegister));
                    assemblyCodes.Add($"LDRB W0, [X{ptrRegister}]");
                    codes.AddRange(Arm64Jit.Movz64(16, putCharAddrBytes[0], 0));
                    assemblyCodes.Add($"MOVZ X16, #0x{putCharAddrBytes[0]:X4}");
                    for (byte i = 1; i <= 3; ++i)
                    {
                        codes.AddRange(Arm64Jit.Movk64(16, putCharAddrBytes[i], i));
                        assemblyCodes.Add($"MOVK X16, #0x{putCharAddrBytes[i]:X4}, LSL #{16 * i}");
                    }
                    codes.AddRange(Arm64Jit.Blr(16));
                    assemblyCodes.Add("BLR X16  ; call putchar");
                    break;
                case BrainfuckToken.InputValue:
                    codes.AddRange(Arm64Jit.Movz64(16, getCharAddrBytes[0], 0));
                    assemblyCodes.Add($"MOVZ X16, #0x{getCharAddrBytes[0]:X4}");
                    for (byte i = 1; i <= 3; ++i)
                    {
                        codes.AddRange(Arm64Jit.Movk64(16, getCharAddrBytes[i], i));
                        assemblyCodes.Add($"MOVK X16, #0x{getCharAddrBytes[i]:X4}, LSL #{16 * i}");
                    }
                    codes.AddRange(Arm64Jit.Blr(16));
                    assemblyCodes.Add("BLR X16  ; call getchar");
                    codes.AddRange(Arm64Jit.StrbImmediateUnsignedOffset(0, ptrRegister));
                    assemblyCodes.Add($"STRB W0, [X{ptrRegister}]");
                    break;
                case BrainfuckToken.LoopStart:
                    loopStack.Push(pc);
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister));
                    assemblyCodes.Add($"LDRB W{workRegister}, [X{ptrRegister}]");
                    codes.AddRange(Arm64Jit.CmpImmediate(workRegister, 0));
                    assemblyCodes.Add($"CMP W{workRegister}, #0");
                    codes.AddRange(new byte[4]);
                    assemblyCodes.Add(";[TEMPORARY]");
                    break;
                case BrainfuckToken.LoopEnd:
                    var loopStart = loopStack.Pop();
                    codes.AddRange(Arm64Jit.LdrbImmediateUnsignedOffset(workRegister, ptrRegister));
                    assemblyCodes.Add($"LDRB W{workRegister}, [X{ptrRegister}]");
                    codes.AddRange(Arm64Jit.CmpImmediate(workRegister, 0));
                    assemblyCodes.Add($"CMP W{workRegister}, #0");
                    
                    var backJumpOffset = (loopStart + 12 - codes.Count) / 4;
                    codes.AddRange(Arm64Jit.BCond((1 << 19) + backJumpOffset, Arm64Jit.BranchCondition.NE));
                    assemblyCodes.Add($"B.NE #{backJumpOffset * 4}");

                    var bCondOffset = (codes.Count - (loopStart + 8)) / 4;
                    var jump = Arm64Jit.BCond(bCondOffset & ((1 << 19) - 1), Arm64Jit.BranchCondition.EQ);
                    codes[loopStart + 8] = jump[0];
                    codes[loopStart + 9] = jump[1];
                    codes[loopStart + 10] = jump[2];
                    codes[loopStart + 11] = jump[3];
                    assemblyCodes[(loopStart + 8) / 4] = $"B.EQ #{bCondOffset * 4}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        codes.AddRange(Arm64Jit.Ldp64SignedOffset(ptrRegister, workRegister, Arm64Jit.SP, 2));
        assemblyCodes.Add($"LDP X{ptrRegister}, X{workRegister}, [SP, #16]");
        codes.AddRange(Arm64Jit.Ldp64(29, 30, Arm64Jit.SP, 4, false));
        assemblyCodes.Add("LDP X29, X30, [SP], #32");
        codes.AddRange(Arm64Jit.Ret());
        assemblyCodes.Add("RET");

        File.WriteAllLines("result.s", assemblyCodes);
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
