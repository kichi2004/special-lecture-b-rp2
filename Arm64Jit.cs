namespace final;

public static class Arm64Jit
{
    static byte[] IntToBytes(uint value)
    {
        byte[] arr = [
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 24) & 0xFF)
        ];
        return arr;
    }

    private const short BitFlag12Bit = (1 << 12) - 1;
    private const sbyte BitFlag7Bit = (1 << 7) - 1;
    private const int BitFlag26Bit = (1 << 26) - 1;
    private const int BitFlag19Bit = (1 << 19) - 1;
    public const byte SP = 31;
    public const byte XZR = 31;
    public const byte WZR = 31;

    private static void CheckRegister(byte rn)
    {
        if (rn > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(rn), "register number out of range");
        }
    }

    // X[d] = X[n] + imm12
    public static byte[] AddImmediate(short imm12, byte rn, byte rd)
    {
        if ((imm12 & BitFlag12Bit) != imm12)
        {
            throw new ArgumentOutOfRangeException(nameof(imm12), "imm12 must be 12bit integer");
        }

        CheckRegister(rn);
        CheckRegister(rd);

        uint ret = 0b0001_0001_0000_0000_0000_0000_0000_0000
                  | (uint) imm12 << 10
                  | (uint) rn << 5
                  | rd;
        Console.Error.WriteLine($"ADD W{rd}, W{rn}, #{imm12} -> {ret:X8}");
        return IntToBytes(ret);
    }
    
    public static byte[] AddImmediate64(short imm12, byte rn, byte rd)
    {
        if ((imm12 & BitFlag12Bit) != imm12)
        {
            throw new ArgumentOutOfRangeException(nameof(imm12), "imm12 must be 12bit integer");
        }

        CheckRegister(rn);
        CheckRegister(rd);

        uint ret = 0b1001_0001_0000_0000_0000_0000_0000_0000
                   | (uint) imm12 << 10
                   | (uint) rn << 5
                   | rd;
        Console.Error.WriteLine($"ADD X{rd}, X{rn}, #{imm12} -> {ret:X8}");
        return IntToBytes(ret);
    }


    public static byte[] SubImmediate(short imm12, byte rn, byte rd)
    {
        if ((imm12 & BitFlag12Bit) != imm12)
        {
            throw new ArgumentOutOfRangeException(nameof(imm12), "imm12 must be 12bit integer");
        }
        CheckRegister(rn);
        CheckRegister(rd);
        
        uint ret = 0b0101_0001_0000_0000_0000_0000_0000_0000
                  | (uint) imm12 << 10
                  | (uint) rn << 5
                  | rd;
        Console.Error.WriteLine($"SUB W{rd}, W{rn}, #{imm12} -> {ret:X8}");
        return IntToBytes(ret);
    }
    
    public static byte[] SubImmediate64(short imm12, byte rn, byte rd)
    {
        if ((imm12 & BitFlag12Bit) != imm12)
        {
            throw new ArgumentOutOfRangeException(nameof(imm12), "imm12 must be 12bit integer");
        }

        CheckRegister(rn);
        CheckRegister(rd);

        uint ret = 0b1101_0001_0000_0000_0000_0000_0000_0000
                   | (uint) imm12 << 10
                   | (uint) rn << 5
                   | rd;
        Console.Error.WriteLine($"SUB X{rd}, X{rn}, #{imm12} -> {ret:X8}");
        return IntToBytes(ret);
    }


    public static byte[] Ret(byte rn = 30)
    {
        CheckRegister(rn);
        uint ret = 0b1101_0110_0101_1111_0000_0000_0000_0000
                  | (uint) (rn << 5);
        Console.Error.WriteLine($"RET W{rn} -> {ret:X8}");
        return IntToBytes(ret);
    }

    /// <summary>
    /// STP (Pre-Index, 64 bit)
    /// STP Xt1, Xt2, [Xn|SP, #imm]!
    /// </summary>
    public static byte[] StpPreIndex64(sbyte imm7, byte rt2, byte rn, byte rt)
    {
        if (rn != SP) CheckRegister(rn);
        CheckRegister(rt);
        CheckRegister(rt2);

        if ((imm7 & BitFlag7Bit) != imm7)
        {
            throw new ArgumentOutOfRangeException(nameof(imm7), "imm7 must be 7bit integer");
        }
        
        uint ret = 0b1010_1001_1000_0000_0000_0000_0000_0000
                  | (uint) imm7 << 15
                  | (uint) rt2 << 10
                  | (uint) rn << 5
                  | rt;
        
        Console.Error.WriteLine($"STP W{rt}, W{rt2}, [{rn}, #{imm7}]! -> {ret:X8}");
        return IntToBytes(ret);
    }

    /// <summary>
    /// MOV Wd|WSP, Wn|WSP 
    /// </summary>
    /// <param name="rd">Wd|WSP</param>
    /// <param name="rn">Wn|WSP</param>
    public static byte[] MovToFromSp(byte rd, byte rn)
    {
        if (rn != SP) CheckRegister(rn);
        if (rd != SP) CheckRegister(rd);
        
        uint ret = 0b0001_0001_0000_0000_0000_0000_0000_0000
                  | (uint) rn << 5
                  | rd;
        
        Console.Error.WriteLine($"MOV W{rd}, W{rn} -> {ret:X8}");
        return IntToBytes(ret);
    }

    public static byte[] MovToFromSp64(byte rd, byte rn)
    {
        if (rn != SP) CheckRegister(rn);
        if (rd != SP) CheckRegister(rd);
        
        uint ret = 0b1001_0001_0000_0000_0000_0000_0000_0000
                   | (uint) rn << 5
                   | rd;
        
        Console.Error.WriteLine($"MOV X{rd}, X{rn} -> {ret:X8}");
        return IntToBytes(ret);
    }

    /// <summary>
    /// MOV Wd, Wm
    /// </summary>
    public static byte[] MovRegister(byte rd, byte rm)
    {
        CheckRegister(rm);
        CheckRegister(rd);
        Console.Error.WriteLine($"MOV W{rd}, W{rm}");
        return OrrShiftedRegisterInternal(false, rd, WZR, rm);
    }
    
    /// <summary>
    /// MOV Wd, Wm
    /// </summary>
    public static byte[] MovRegister64(byte rd, byte rm)
    {
        CheckRegister(rm);
        CheckRegister(rd);
        Console.Error.WriteLine($"MOV X{rd}, X{rm}");
        return OrrShiftedRegisterInternal(true, rd, XZR, rm);
    }


    public static byte[] Movz64(byte wd, ushort imm, byte shift)
    {
        uint ret = 0b1101_0010_1000_0000_0000_0000_0000_0000
                  | (uint) shift << 21
                  | (uint) imm << 5
                  | wd;
       
        Console.Error.WriteLine($"MOVZ X{wd}, #{imm}, LSL #{shift} -> {ret:X8}");
        return IntToBytes(ret); 
    }

    public static byte[] Movk64(byte wd, ushort imm, byte shift)
    {   
        uint ret = 0b1111_0010_1000_0000_0000_0000_0000_0000
                  | (uint) shift << 21
                  | (uint) imm << 5
                  | wd;
        Console.Error.WriteLine($"MOVK X{wd}, #{imm}, LSL #{shift} -> {ret:X8}");
        return IntToBytes(ret);
    }

    // ORR Wd, Wn, Wm[, shift, #amount]
    private static byte[] OrrShiftedRegisterInternal(bool sf, byte rd, byte rn, byte rm, byte shift = 0, byte imm6 = 0)
    {
        uint ret = 0b0010_1010_0000_0000_0000_0000_0000_0000
                  | (uint) (sf ? 1 : 0) << 31
                  | (uint) shift << 22
                  | (uint) rm << 16
                  | (uint) imm6 << 10
                  | (uint) rn << 5
                  | rd;
        
        return IntToBytes(ret);
    } 
    
    /// <summary>
    /// LDP (Pre-Index, 64bit)
    /// LDP Xt1, Xt2, [Xn|SP, #imm]!
    /// </summary>
    public static byte[] Ldp64(byte rt, byte rt2, byte rn, sbyte imm7, bool preIndex = true)
    {
        if (rn != SP) CheckRegister(rn);
        CheckRegister(rt);
        CheckRegister(rt2);

        uint ret = 0b1010_1000_1100_0000_0000_0000_0000_0000
                   | (uint)(preIndex ? 1 : 0) << 24
                   | (uint)imm7 << 15
                   | (uint)rt2 << 10
                   | (uint)rn << 5
                   | rt;
        
        Console.Error.WriteLine($"LDP W{rt}, W{rt2}, [{rn}, #{imm7}]! -> {ret:X8}");
        return IntToBytes(ret);
    }

    /// <summary>
    /// Load register byte (immediate)
    /// LDRB Wt, [Xn|SP], #simm
    /// </summary>
    public static byte[] LdrbImmediate(byte rt, byte rn, short imm9, bool preIndex = true)
    {
        uint ret = 0b0011_1000_0100_0000_0000_0100_0000_0000
                   | (uint)imm9 << 12
                   | (uint)(preIndex ? 1 : 0) << 11
                   | (uint)rn << 5
                   | rt;
        
        Console.Error.WriteLine($"LDRB W{rt}, [{rn}, #{imm9}]{(preIndex ? "!" : "")} -> {ret:X8}");
        return IntToBytes(ret);
    }

    public static byte[] LdrbImmediateUnsignedOffset(byte rt, byte rn, int imm12)
    {
        uint ret = 0b11_1001_0100_0000_0000_0000_0000_0000
                   | (uint)imm12 << 10
                   | (uint)rn << 5
                   | rt;
        
        Console.Error.WriteLine($"LDRB W{rt}, [{rn}, #{imm12}] -> {ret:X8}");
        return IntToBytes(ret);
    }
    
    /// <summary>
    /// Store register byte (immediate)
    /// STRB Wt, [Xn|SP], #simm
    /// </summary>
    public static byte[] StrbImmediateUnsignedOffset(byte rt, byte rn, short imm12)
    {
        uint ret = 0b0011_1001_0000_0000_0000_0000_0000_0000
                   | (uint)imm12 << 10
                   | (uint)rn << 5
                   | rt;
        
        Console.Error.WriteLine($"STRB W{rt}, [{rn}, #{imm12}] -> {ret:X8}");
        return IntToBytes(ret);
    }
    

    public static byte[] Bl(int imm26)
    {
        if ((imm26 & BitFlag26Bit) != imm26)
            throw new ArgumentOutOfRangeException(nameof(imm26), "imm26 must be 26bit integer");
        uint ret = 0b1001_0100_0000_0000_0000_0000_0000_0000 
                   | (uint)imm26;
        return IntToBytes(ret);
    }

    /// <summary>
    /// Compare (immediate)
    /// </summary>
    public static byte[] CmpImmediate(byte rn, short imm12, bool shift = false)
    {
        if ((imm12 & BitFlag12Bit) != imm12)
        {
            throw new ArgumentOutOfRangeException(nameof(imm12), "imm12 must be 12bit integer");
        }
        if (rn != SP) CheckRegister(rn);
        
        uint ret = 0b0111_0001_0000_0000_0000_0000_0001_1111
                  | (uint) (shift ? 1 : 0) << 22
                  | (uint) imm12 << 10
                  | (uint) rn << 5;
        
        Console.Error.WriteLine($"CMP W{rn}, #{imm12}{(shift ? ", LSL #12" : "")}");
        return IntToBytes(ret);
    }
    
    public enum BranchCondition : byte {
        EQ, NE, CS, CC, MI, PL, VS, VC, HI, LS, GE, LT, GT, LE, AL, NV
    }

    public static byte[] B(int imm26)
    {
        if ((imm26 & BitFlag26Bit) != imm26)
            throw new ArgumentOutOfRangeException(nameof(imm26), "imm26 must be 26bit integer");
        uint ret = 0b0001_0100_0000_0000_0000_0000_0000_0000 
                   | (uint)imm26;

        Console.Error.WriteLine($"B #{((imm26 & (1 << 25)) == 0 ? imm26 : imm26 - (1 << 26))}");
        return IntToBytes(ret);
    }

    public static byte[] BCond(int imm19, BranchCondition cond)
    {
        if ((imm19 & BitFlag19Bit) != imm19)
        {
            throw new ArgumentOutOfRangeException(nameof(imm19), "imm19 must be 19bit integer");
        }

        uint ret = 0b0101_0100_0000_0000_0000_0000_0000_0000
                   | (uint)imm19 << 5
                   | (uint)cond;
        
        Console.Error.WriteLine($"B.{cond} #{imm19}");
        return IntToBytes(ret);
    }

    public static byte[] Blr(byte rn)
    {
        CheckRegister(rn);
        uint ret = 0b1101_0110_0011_1111_0000_0000_0000_0000
                   | (uint)rn << 5;
        
        Console.Error.WriteLine($"BLR W{rn}");
        return IntToBytes(ret);
    }
}
