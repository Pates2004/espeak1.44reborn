#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <sapi.h>
#include <sapiddk.h>
#include <stdio.h>
#include "../windows_sapi/sonic_speed.h"

static bool VerifySonicModes()
{
    using SonicSpeed::Mode;
    int previous = 0;
    for (int rate = -10; rate <= 10; ++rate) {
        const int native_rate = SonicSpeed::NativeRate(rate, 0, true, Mode::Smooth);
        const int target = SonicSpeed::Target(rate, true, Mode::Smooth);
        const int effective = target > 0 ? target : native_rate;
        if (native_rate < 80 || native_rate > 300 || effective <= previous) return false;
        previous = effective;
    }
    if (SonicSpeed::SmoothRate(-2147483647, -100) != 80 ||
        SonicSpeed::SmoothRate(2147483647, 100) != 1350 ||
        SonicSpeed::NativeRate(-10, 0, true, Mode::Smooth) != 80 ||
        SonicSpeed::NativeRate(0, 0, true, Mode::Smooth) != 300 ||
        SonicSpeed::Target(0, true, Mode::Smooth) != 0 ||
        SonicSpeed::Target(10, true, Mode::Smooth) != 1350 ||
        SonicSpeed::NativeRate(0, 0, false, Mode::Smooth) != 187) return false;
    return SonicSpeed::Rate(-10) == 80 && SonicSpeed::Rate(0) == 187 &&
        SonicSpeed::Rate(10) == 450 && SonicSpeed::Rate(10, 10) == 450 &&
        SonicSpeed::NativeRate(-10, 0, true, Mode::Nvda) == 240 &&
        SonicSpeed::Target(-10, true, Mode::Nvda) == 0 &&
        SonicSpeed::NativeRate(0, 0, true, Mode::Nvda) == 450 &&
        SonicSpeed::Target(0, true, Mode::Nvda) == 561 &&
        SonicSpeed::Target(10, true, Mode::Nvda) == 1350 &&
        SonicSpeed::NativeRate(0, 0, true, Mode::Legacy) == 187 &&
        SonicSpeed::Target(4, true, Mode::Legacy) == 0 &&
        SonicSpeed::Target(5, true, Mode::Legacy) == 540 &&
        SonicSpeed::Target(10, false, Mode::Nvda) == 0;
}

int wmain(int argc, wchar_t** argv)
{
    if (!VerifySonicModes()) return 7;
    const wchar_t* path = argc > 1 ? argv[1] : L"espeak_sapi.dll";
    HMODULE module = LoadLibraryW(path);
    if (module == nullptr) {
        fwprintf(stderr, L"LoadLibrary failed: %lu\n", GetLastError());
        return 1;
    }

    using GetClassObjectFn = HRESULT(STDAPICALLTYPE*)(REFCLSID, REFIID, void**);
    auto get_class_object = reinterpret_cast<GetClassObjectFn>(
        GetProcAddress(module, "DllGetClassObject"));
    if (get_class_object == nullptr) {
        FreeLibrary(module);
        return 2;
    }

    const CLSID engine_clsid =
        {0xBE985C8D, 0xBE32, 0x4A22, {0xAA, 0x93, 0x55, 0xC1, 0x6A, 0x6D, 0x1D, 0x91}};
    IClassFactory* factory = nullptr;
    HRESULT result = get_class_object(engine_clsid, IID_IClassFactory,
                                      reinterpret_cast<void**>(&factory));
    if (FAILED(result)) {
        FreeLibrary(module);
        return 3;
    }

    ISpTTSEngine* engine = nullptr;
    result = factory->CreateInstance(nullptr, __uuidof(ISpTTSEngine),
                                     reinterpret_cast<void**>(&engine));
    factory->Release();
    if (FAILED(result)) {
        FreeLibrary(module);
        return 4;
    }

    GUID format_id{};
    WAVEFORMATEX* format = nullptr;
    result = engine->GetOutputFormat(nullptr, nullptr, &format_id, &format);
    if (FAILED(result) || format == nullptr || format->wFormatTag != WAVE_FORMAT_PCM ||
        format->nChannels != 1 || format->wBitsPerSample != 16 ||
        format->nSamplesPerSec != 22050) {
        CoTaskMemFree(format);
        engine->Release();
        FreeLibrary(module);
        return 5;
    }

    wprintf(L"SAPI engine OK: %lu Hz, %u-bit mono\n",
            format->nSamplesPerSec, format->wBitsPerSample);
    CoTaskMemFree(format);
    engine->Release();

    using CanUnloadFn = HRESULT(STDAPICALLTYPE*)();
    auto can_unload = reinterpret_cast<CanUnloadFn>(GetProcAddress(module, "DllCanUnloadNow"));
    const bool unload_ok = can_unload != nullptr && can_unload() == S_OK;
    FreeLibrary(module);
    return unload_ok ? 0 : 6;
}
