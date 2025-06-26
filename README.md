# ImGui Benchmarks

## Introduction

This repo contains microbenchmarks to test certain behaviors of the Hexa.Net.ImGui bindings for Dear ImGui.
All benchmark code is licensed under the MIT license.

## Benchmarks

### InputTextBenchmark

This is a family of benchmarks that measures performance of various methods for calling `ImGui.InputText(...)`.
It does this by creating an ImGui window, rendering a batch of 64 `ImGui.InputText` controls, and finally returning
the draw data. The variants of this benchmark explore how various calling styles affect the performance of this call,
particularly comparing the `ref string` and `byte*` buffer types as well as performance related to various ways of
handling the `ImGuiInputTextFlags.EnterReturnsTrue` flag. The `Utils` class from `Hexa.Net.ImGui` is used for
in-place UTF8 marshaling to reduce GC pressure in the same way that the wrapper does.

Representative results obtained on an older PC:

```
BenchmarkDotNet v0.15.2, Linux Arch Linux
AMD FX(tm)-4350 4.20GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX
  DefaultJob : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX
```

| Method                     | Mean         | Error    | StdDev   |
|--------------------------- |-------------:|---------:|---------:|
| InputTextRefBaselineNoFlag | 79.89 us     | 0.710 us | 0.664 us |
| InputTextRefBaseline       | **82.05 us** | 0.642 us | 0.601 us |
| InputTextPtrBaseline       | 76.38 us     | 0.757 us | 0.709 us |
| InputTextPtrNoCopies       | **81.65 us** | 0.380 us | 0.355 us |
| InputTextPtrMinimalCopies  | **81.43 us** | 0.447 us | 0.418 us |
| InputTextPtrManyCopies     | 85.11 us     | 0.580 us | 0.514 us |

As expected, `byte*` buffers give a noticeable performance improvement as compared to `ref string` buffers (which are converted to `byte*` buffers under the hood as needed). The `InputTextRefBaseline` (`ref string` buffers), `InputTextPtrNoCopies` (`byte*` buffer that is marshaled from a string buffer before each call, as a proxy for the underlying behavior of the `ref string` method), and `InputTextPtrMinimalCopies` (which is like the other two, but using an extra call to `ImGui.IsItemDeactivatedAfterEdit()` to determine whether to marshal back to the string buffer when the `EnterReturnsTrue` input flag is set) have means that lie within their mutual error bounds (99.9% CL). Finally, `InputTextPtrManyCopies` (naive implementation for `EnterReturnsTrue` flag where the buffer is marshaled back to the string on every call) shows a noticeable performance hit from the extra marshaling.

The key takeaways from this benchmark are:
- `byte*` buffers are the fastest (as expected)
- `ref string` buffers are slightly slower
- When the `EnterReturnsTrue` flag is used in conjunction with a `ref string` buffer, a call to `ImGui.IsItemDeactivatedAfterEdit()` can yield the correct behavior of the API with only a negligible (within error bounds) performance impact

