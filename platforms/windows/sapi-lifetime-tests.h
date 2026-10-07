// Bounded lifetime checks shared by the x64 and x86 synthesis test executable.
// No registry token, system registration, UI or audio device is used.
#include <psapi.h>
#include <dbghelp.h>
#include <thread>
#include "../../src/speak_lib.h"
#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "dbghelp.lib")

static LONG WINAPI ReportLifetimeException(EXCEPTION_POINTERS* exception) {
    fwprintf(stderr, L"Native test exception: %08lx\n", exception->ExceptionRecord->ExceptionCode);
    HANDLE process = GetCurrentProcess();
    SymInitialize(process, nullptr, TRUE);
    CONTEXT context = *exception->ContextRecord;
    STACKFRAME64 frame{};
#ifdef _WIN64
    const DWORD machine = IMAGE_FILE_MACHINE_AMD64;
    frame.AddrPC.Offset = context.Rip;
    frame.AddrStack.Offset = context.Rsp;
    frame.AddrFrame.Offset = context.Rbp;
#else
    const DWORD machine = IMAGE_FILE_MACHINE_I386;
    frame.AddrPC.Offset = context.Eip;
    frame.AddrStack.Offset = context.Esp;
    frame.AddrFrame.Offset = context.Ebp;
#endif
    frame.AddrPC.Mode = frame.AddrStack.Mode = frame.AddrFrame.Mode = AddrModeFlat;
    for (int index = 0; index < 12 && frame.AddrPC.Offset; ++index) {
        alignas(SYMBOL_INFO) char storage[sizeof(SYMBOL_INFO) + MAX_SYM_NAME]{};
        SYMBOL_INFO* symbol = reinterpret_cast<SYMBOL_INFO*>(storage);
        symbol->SizeOfStruct = sizeof(SYMBOL_INFO);
        symbol->MaxNameLen = MAX_SYM_NAME;
        DWORD64 displacement = 0;
        if (SymFromAddr(process, frame.AddrPC.Offset, &displacement, symbol))
            fprintf(stderr, "  %s + %llu\n", symbol->Name, displacement);
        if (!StackWalk64(machine, process, GetCurrentThread(), &frame, &context,
                nullptr, SymFunctionTableAccess64, SymGetModuleBase64, nullptr)) break;
    }
    SymCleanup(process);
    return EXCEPTION_EXECUTE_HANDLER;
}

using CanUnloadFunction = HRESULT(STDAPICALLTYPE*)();
using GetFactoryFunction = HRESULT(STDAPICALLTYPE*)(REFCLSID, REFIID, void**);
static const CLSID LifetimeEngineClsid =
    {0xBE985C8D, 0xBE32, 0x4A22, {0xAA, 0x93, 0x55, 0xC1, 0x6A, 0x6D, 0x1D, 0x91}};

class MemoryVoiceToken final : public ISpObjectToken {
    ULONG references_ = 1;
    std::wstring path_;
public:
    explicit MemoryVoiceToken(const wchar_t* path) : path_(path) {}
    ULONG References() const { return references_; }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (id != IID_IUnknown && id != __uuidof(ISpDataKey) && id != __uuidof(ISpObjectToken))
            return E_NOINTERFACE;
        *result = static_cast<ISpObjectToken*>(this);
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++references_; }
    ULONG STDMETHODCALLTYPE Release() override { return --references_; }
    HRESULT STDMETHODCALLTYPE GetStringValue(LPCWSTR name, LPWSTR* result) override {
        if (!result) return E_POINTER;
        *result = nullptr;
        if (!name) return E_INVALIDARG;
        const wchar_t* value = wcscmp(name, L"Path") == 0 ? path_.c_str() :
            wcscmp(name, L"VoiceName") == 0 ? L"pl" : nullptr;
        if (!value) return E_INVALIDARG;
        const size_t bytes = (wcslen(value) + 1) * sizeof(wchar_t);
        *result = static_cast<wchar_t*>(CoTaskMemAlloc(bytes));
        if (!*result) return E_OUTOFMEMORY;
        memcpy(*result, value, bytes);
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetDWORD(LPCWSTR name, DWORD* result) override {
        if (!result) return E_POINTER;
        if (!name || wcscmp(name, L"Inflection") != 0) return E_INVALIDARG;
        *result = 50;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE SetData(LPCWSTR, ULONG, const BYTE*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetData(LPCWSTR, ULONG*, BYTE*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE SetStringValue(LPCWSTR, LPCWSTR) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE SetDWORD(LPCWSTR, DWORD) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE OpenKey(LPCWSTR, ISpDataKey**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE CreateKey(LPCWSTR, ISpDataKey**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE DeleteKey(LPCWSTR) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE DeleteValue(LPCWSTR) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE EnumKeys(ULONG, LPWSTR*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE EnumValues(ULONG, LPWSTR*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE SetId(LPCWSTR, LPCWSTR, BOOL) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetId(LPWSTR*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetCategory(ISpObjectTokenCategory**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown*, DWORD, REFIID, void**) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE GetStorageFileName(REFCLSID, LPCWSTR, LPCWSTR, ULONG, LPWSTR*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE RemoveStorageFileName(REFCLSID, LPCWSTR, BOOL) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE Remove(const CLSID*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE IsUISupported(LPCWSTR, void*, ULONG, IUnknown*, BOOL*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE DisplayUI(HWND, LPCWSTR, LPCWSTR, void*, ULONG, IUnknown*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE MatchesAttributes(LPCWSTR, BOOL*) override { return E_NOTIMPL; }
};

static SIZE_T LifetimePrivateBytes() {
    PROCESS_MEMORY_COUNTERS_EX memory{};
    memory.cb = sizeof(memory);
    return GetProcessMemoryInfo(GetCurrentProcess(), reinterpret_cast<PROCESS_MEMORY_COUNTERS*>(&memory),
        sizeof(memory)) ? memory.PrivateUsage : 0;
}

static ISpTTSEngine* CreateLifetimeEngine(IClassFactory* factory, MemoryVoiceToken& token) {
    ISpTTSEngine* engine = nullptr;
    ISpObjectWithToken* owner = nullptr;
    HRESULT result = factory->CreateInstance(nullptr, __uuidof(ISpTTSEngine), reinterpret_cast<void**>(&engine));
    if (SUCCEEDED(result)) result = engine->QueryInterface(__uuidof(ISpObjectWithToken), reinterpret_cast<void**>(&owner));
    if (SUCCEEDED(result)) result = owner->SetObjectToken(&token);
    if (owner) owner->Release();
    if (FAILED(result) && engine) { engine->Release(); engine = nullptr; }
    return engine;
}

static bool SpeakLifetime(ISpTTSEngine* engine, CanUnloadFunction canUnload, const wchar_t* wavePath) {
    const wchar_t* text = L"To jest zwykla proba ponownego uzycia syntezatora.";
    SPVTEXTFRAG fragment{};
    fragment.State.eAction = SPVA_Speak;
    fragment.State.Volume = 100;
    fragment.pTextStart = text;
    fragment.ulTextLen = static_cast<ULONG>(wcslen(text));
    WAVEFORMATEX format{WAVE_FORMAT_PCM, 1, 22050, 44100, 2, 16, 0};
    TestSite site;
    site.textLength = fragment.ulTextLen;
    site.checkCanUnload = canUnload;
    const HRESULT result = engine->Speak(0, SPDFID_WaveFormatEx, &format, &fragment, &site);
    bool nonzero = false;
    for (BYTE sample : site.audio) nonzero |= sample != 0;
    return result == S_OK && nonzero && site.audio.size() > 1000 && site.moduleStayedBusy &&
        site.ValidateEvents(false) && (!wavePath || WriteWave(wavePath, site.audio));
}

static bool CheckFactoryOnOtherThread(GetFactoryFunction getFactory, CanUnloadFunction canUnload) {
    HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    HANDLE release = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!ready || !release) {
        if (ready) CloseHandle(ready);
        if (release) CloseHandle(release);
        return false;
    }
    bool acquired = false;
    std::thread worker([&]() {
        IClassFactory* factory = nullptr;
        acquired = SUCCEEDED(getFactory(LifetimeEngineClsid, IID_IClassFactory, reinterpret_cast<void**>(&factory)));
        SetEvent(ready);
        WaitForSingleObject(release, 5000);
        if (factory) factory->Release();
    });
    const bool held = WaitForSingleObject(ready, 5000) == WAIT_OBJECT_0 && canUnload() == S_FALSE;
    SetEvent(release);
    worker.join();
    CloseHandle(release);
    CloseHandle(ready);
    return acquired && held && canUnload() == S_OK;
}

static bool RunSapiLifetimeTests(const wchar_t* dllPath, const wchar_t* dataPath, const wchar_t* wavePath) {
    if (FAILED(CoInitializeEx(nullptr, COINIT_MULTITHREADED))) return false;
    bool passed = true;
    const SIZE_T baseline = LifetimePrivateBytes();
    for (int cycle = 0; cycle < 3 && passed; ++cycle) {
        HMODULE module = LoadLibraryW(dllPath);
        if (!module) { passed = false; break; }
        const auto getFactory = reinterpret_cast<GetFactoryFunction>(GetProcAddress(module, "DllGetClassObject"));
        const auto canUnload = reinterpret_cast<CanUnloadFunction>(GetProcAddress(module, "DllCanUnloadNow"));
        passed = getFactory && canUnload && canUnload() == S_OK;
        MemoryVoiceToken token(dataPath);
        IClassFactory* factory = nullptr;
        if (passed) passed = SUCCEEDED(getFactory(LifetimeEngineClsid, IID_IClassFactory, reinterpret_cast<void**>(&factory)));
        if (factory) {
            passed = passed && canUnload() == S_FALSE && factory->LockServer(TRUE) == S_OK;
            factory->Release(); factory = nullptr;
            passed = passed && canUnload() == S_FALSE;
            if (SUCCEEDED(getFactory(LifetimeEngineClsid, IID_IClassFactory, reinterpret_cast<void**>(&factory)))) {
                passed = factory->LockServer(FALSE) == S_OK && passed;
            } else passed = false;
        }
        if (passed) {
            ISpTTSEngine* first = CreateLifetimeEngine(factory, token);
            ISpTTSEngine* second = CreateLifetimeEngine(factory, token);
            passed = first && second && canUnload() == S_FALSE;
            if (passed) passed = SpeakLifetime(first, canUnload, nullptr);
            if (first) first->Release();
            if (passed) passed = SpeakLifetime(second, canUnload, nullptr);
            if (second) second->Release();
            passed = canUnload() == S_FALSE && passed; // The factory still owns the module.
        }
        if (factory) { factory->Release(); factory = nullptr; }
        if (passed) passed = canUnload() == S_OK && canUnload() == S_OK && token.References() == 1;
        // COM may ask about unloading but keep the DLL loaded. Reinitialize it.
        if (passed) passed = SUCCEEDED(getFactory(LifetimeEngineClsid, IID_IClassFactory, reinterpret_cast<void**>(&factory)));
        if (passed) {
            ISpTTSEngine* next = CreateLifetimeEngine(factory, token);
            factory->Release(); factory = nullptr;
            passed = next && canUnload() == S_FALSE && SpeakLifetime(next, canUnload, cycle == 2 ? wavePath : nullptr);
            if (next) next->Release();
            passed = canUnload() == S_OK && token.References() == 1 && passed;
        }
        if (factory) factory->Release();
        if (passed) passed = CheckFactoryOnOtherThread(getFactory, canUnload);
        const bool freed = FreeLibrary(module) != FALSE;
        passed = freed && GetModuleHandleW(dllPath) == nullptr && passed;
        wprintf(L"SAPI lifetime cycle %d: passed=%d, private bytes=%zu, baseline=%zu\n",
            cycle + 1, passed, LifetimePrivateBytes(), baseline);
    }
    CoUninitialize();
    return passed;
}

static std::vector<BYTE> lifetimeApiAudio;
static int lifetimeApiCompletions;
static int LifetimeApiCallback(short* samples, int count, espeak_EVENT*) {
    if (!samples) { ++lifetimeApiCompletions; return 0; }
    if (count < 0 || lifetimeApiAudio.size() + static_cast<size_t>(count) * 2 > 16000000) return 1;
    const BYTE* bytes = reinterpret_cast<BYTE*>(samples);
    lifetimeApiAudio.insert(lifetimeApiAudio.end(), bytes, bytes + count * 2);
    return 0;
}

static bool RunApiLifetimeTests(const wchar_t* dllPath, const wchar_t* dataPath, const wchar_t* wavePath) {
    HMODULE module = LoadLibraryW(dllPath);
    if (!module) return false;
    const auto initialize = reinterpret_cast<decltype(&espeak_Initialize)>(GetProcAddress(module, "espeak_Initialize"));
    const auto terminate = reinterpret_cast<decltype(&espeak_Terminate)>(GetProcAddress(module, "espeak_Terminate"));
    const auto setCallback = reinterpret_cast<decltype(&espeak_SetSynthCallback)>(GetProcAddress(module, "espeak_SetSynthCallback"));
    const auto listVoices = reinterpret_cast<decltype(&espeak_ListVoices)>(GetProcAddress(module, "espeak_ListVoices"));
    const auto setVoice = reinterpret_cast<decltype(&espeak_SetVoiceByName)>(GetProcAddress(module, "espeak_SetVoiceByName"));
    const auto setProperties = reinterpret_cast<decltype(&espeak_SetVoiceByProperties)>(GetProcAddress(module, "espeak_SetVoiceByProperties"));
    const auto synth = reinterpret_cast<decltype(&espeak_Synth)>(GetProcAddress(module, "espeak_Synth"));
    char path[32768];
    bool passed = initialize && terminate && setCallback && listVoices && setVoice && setProperties && synth &&
        WideCharToMultiByte(CP_ACP, 0, dataPath, -1, path, sizeof(path), nullptr, nullptr) > 0;
    if (passed) {
        // Termination must tolerate both a fresh module and a failed Initialize.
        passed = terminate() == EE_OK && terminate() == EE_OK;
        const std::string missing = std::string(path) + "/__missing_lifetime_test_data__";
        passed = GetFileAttributesA(missing.c_str()) == INVALID_FILE_ATTRIBUTES && passed;
        if (passed) passed = initialize(AUDIO_OUTPUT_SYNCHRONOUS, 100, missing.c_str(), 0) == EE_INTERNAL_ERROR;
        passed = terminate() == EE_OK && terminate() == EE_OK && passed;
    }
    const SIZE_T baseline = LifetimePrivateBytes();
    for (int cycle = 0; cycle < 3 && passed; ++cycle) {
        lifetimeApiAudio.clear();
        lifetimeApiCompletions = 0;
        passed = initialize(AUDIO_OUTPUT_SYNCHRONOUS, 100, path, 0) == 22050;
        setCallback(LifetimeApiCallback);
        const espeak_VOICE** voices = listVoices(nullptr);
        size_t languages = 0;
        if (voices) while (voices[languages]) ++languages;
        passed = languages > 0 && passed;
        espeak_VOICE filter{};
        filter.languages = "variant";
        voices = listVoices(&filter);
        std::vector<std::string> variants;
        if (voices) for (size_t index = 0; voices[index]; ++index) {
            const char* identifier = voices[index]->identifier;
            const char* separator = strrchr(identifier, '/');
            const char* backslash = strrchr(identifier, '\\');
            if (backslash && (!separator || backslash > separator)) separator = backslash;
            variants.emplace_back(std::string("pl+") + (separator ? separator + 1 : identifier));
        }
        // The shipped catalog has 104 variants, as well as ordinary languages.
        // No generated oversized input/catalog is used by this regression test.
        passed = variants.size() >= 104 && passed;
        for (const std::string& variant : variants) passed = setVoice(variant.c_str()) == EE_OK && passed;
        filter = espeak_VOICE{};
        filter.languages = "pl";
        filter.gender = 2;
        filter.variant = 2;
        passed = setProperties(&filter) == EE_OK && setVoice("pl") == EE_OK && passed;
        wprintf(L"C API catalog cycle %d: languages=%zu, variants=%zu, selected=%d\n",
            cycle + 1, languages, variants.size(), passed);
        const char text[] = "To jest zwykla proba publicznego API.";
        if (passed) passed = synth(text, sizeof(text), 0, POS_CHARACTER, 0, espeakCHARS_UTF8, nullptr, nullptr) == EE_OK;
        bool nonzero = false;
        for (BYTE sample : lifetimeApiAudio) nonzero |= sample != 0;
        passed = passed && nonzero && lifetimeApiCompletions == 1;
        if (passed && cycle == 2) passed = WriteWave(wavePath, lifetimeApiAudio);
        passed = terminate() == EE_OK && terminate() == EE_OK && passed;
        wprintf(L"C API lifetime cycle %d: passed=%d, completions=%d, private bytes=%zu, baseline=%zu\n",
            cycle + 1, passed, lifetimeApiCompletions, LifetimePrivateBytes(), baseline);
    }
    if (terminate) terminate();
    const bool freed = FreeLibrary(module) != FALSE;
    return freed && passed;
}
