using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using Hexa.NET.ImGui;

namespace ImguiBenchmarks;

[SimpleJob]
public class InputTextBenchmarks
{
    private const int BatchSize = 32;
    private const int BufferSize = 64;

    private readonly string[] _labelStrings = new string[BatchSize];

    private string _buffer = "Hello World!";
    private byte[] _byteBuffer = new byte[BufferSize+1];
    private GCHandle _byteBufferHandle;
    private unsafe byte* _byteBufferPtr;

    [GlobalSetup]
    public unsafe void Setup()
    {
        for (int i = 0; i < BatchSize; ++i)
        {
            _labelStrings[i] = $"##input{i}";
        }
        
        var bytes = Encoding.UTF8.GetBytes(_buffer);
        Array.Copy(bytes, _byteBuffer, bytes.Length);

        _byteBufferHandle = GCHandle.Alloc(_byteBuffer, GCHandleType.Pinned);
        _byteBufferPtr = (byte*)_byteBufferHandle.AddrOfPinnedObject();

        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);

        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(1920.0f, 1080.0f);
        io.Fonts.AddFontDefault();
        io.Fonts.Build();
    }

    [GlobalCleanup]
    public unsafe void Cleanup()
    {
        _byteBufferPtr = null;
        if (_byteBufferHandle.IsAllocated)
        {
            _byteBufferHandle.Free();
        }
    }
    
    /// <summary>
    ///     Baseline variant using ref string without flag set.
    /// </summary>
    [Benchmark]
    public ImDrawDataPtr InputTextRefBaselineNoFlag()
    {
        ImGui.NewFrame();
        if (ImGui.Begin("Window"u8))
        {
            for (int i = 0; i < BatchSize; ++i)
            {
                ImGui.InputText(_labelStrings[i], ref _buffer, BufferSize);
            }
            ImGui.End();
        }

        ImGui.Render();
        return ImGui.GetDrawData();
    }
    
    /// <summary>
    ///     Baseline variant using ref string with flag set.
    /// </summary>
    [Benchmark]
    public ImDrawDataPtr InputTextRefBaseline()
    {
        ImGui.NewFrame();
        if (ImGui.Begin("Window"u8))
        {
            for (int i = 0; i < BatchSize; ++i)
            {
                ImGui.InputText(_labelStrings[i], ref _buffer, BufferSize, ImGuiInputTextFlags.EnterReturnsTrue);
            }
            ImGui.End();
        }

        ImGui.Render();
        return ImGui.GetDrawData();
    }
    
    /// <summary>
    ///     Variant using byte array w/o marshaling/copying.
    /// </summary>
    [Benchmark]
    public unsafe ImDrawDataPtr InputTextPtrBaseline()
    {
        ImGui.NewFrame();
        if (ImGui.Begin("Window"u8))
        {
            for (int i = 0; i < BatchSize; ++i)
            {
                ImGui.InputText(_labelStrings[i], _byteBufferPtr, BufferSize, ImGuiInputTextFlags.EnterReturnsTrue);
            }
            
            ImGui.End();
        }

        ImGui.Render();
        return ImGui.GetDrawData();
    }
    /// <summary>
    ///     Variant using byte array w/ no marshaling/copying back from the byte array (should be comparable to
    ///     ref string where the value is copied in but not back out). Comparing this to MinimalCopies should show
    ///     the impact of the extra call to IsItemDeactivatedAfterEdit() in the case where the flag is set but that
    ///     call is returning false.
    /// </summary>
    [Benchmark]
    public unsafe ImDrawDataPtr InputTextPtrNoCopies()
    {
        ImGui.NewFrame();
        if (ImGui.Begin("Window"u8))
        {
            for (int i = 0; i < BatchSize; ++i)
            {
                var bytes = Encoding.UTF8.GetBytes(_buffer);
                Marshal.Copy(bytes, 0, new IntPtr(_byteBufferPtr), StrLen(bytes));
                ImGui.InputText(_labelStrings[i], _byteBufferPtr, BufferSize, ImGuiInputTextFlags.EnterReturnsTrue);
            }
            
            ImGui.End();
        }

        ImGui.Render();
        return ImGui.GetDrawData();
    }
    
    /// <summary>
    ///     Variant using byte array w/ marshaling/copying only when needed.
    /// </summary>
    [Benchmark]
    public unsafe ImDrawDataPtr InputTextPtrMinimalCopies()
    {
        ImGui.NewFrame();
        if (ImGui.Begin("Window"u8))
        {
            for (int i = 0; i < BatchSize; ++i)
            {
                var bytes = Encoding.UTF8.GetBytes(_buffer);
                Marshal.Copy(bytes, 0, new IntPtr(_byteBufferPtr), StrLen(bytes));
                ImGui.InputText(_labelStrings[i], _byteBufferPtr, BufferSize, ImGuiInputTextFlags.EnterReturnsTrue);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    // Will never get here in the benchmark, but the vast majority of frames in practice will not take
                    // this branch so this is realistic.
                    _buffer = Marshal.PtrToStringUTF8(new IntPtr(_byteBufferPtr)) ?? "";
                }
            }
            
            ImGui.End();
        }

        ImGui.Render();
        return ImGui.GetDrawData();
    }
    
    /// <summary>
    ///     Variant using byte array w/ marshaling/copying every frame (assuming the flag is set, which we don't check here).
    /// </summary>
    [Benchmark]
    public unsafe ImDrawDataPtr InputTextPtrManyCopies()
    {
        ImGui.NewFrame();
        if (ImGui.Begin("Window"u8))
        {
            for (int i = 0; i < BatchSize; ++i)
            {
                var bytes = Encoding.UTF8.GetBytes(_buffer);
                Marshal.Copy(bytes, 0, new IntPtr(_byteBufferPtr), StrLen(bytes));
                ImGui.InputText(_labelStrings[i], _byteBufferPtr, BufferSize, ImGuiInputTextFlags.EnterReturnsTrue);
                _buffer = Marshal.PtrToStringUTF8(new IntPtr(_byteBufferPtr)) ?? "";
            }
            
            ImGui.End();
        }

        ImGui.Render();
        return ImGui.GetDrawData();
    }

    private int StrLen(byte[] arr)
    {
        int len = 0;
        int maxLen = arr.Length;
        while (len < maxLen && arr[len] != 0) len++;
        return len;
    }
}