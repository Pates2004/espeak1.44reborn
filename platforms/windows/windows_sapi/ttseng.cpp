// Native Win64 COM server for the eSpeak SAPI 5 engine.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>
#include <new>

#include "TtsEng.h"
#include "TtsEngObj.h"

#include "TtsEng_i.c"

volatile LONG g_module_object_count = 0;
static volatile LONG g_server_lock_count = 0;
static HMODULE g_module = nullptr;
static CRITICAL_SECTION g_sapi_engine_lock;
static bool g_sapi_engine_lock_initialized = false;

void LockSapiEngine()
{
    EnterCriticalSection(&g_sapi_engine_lock);
}

void UnlockSapiEngine()
{
    LeaveCriticalSection(&g_sapi_engine_lock);
}

namespace {

class TtsClassFactory final : public IClassFactory
{
public:
    TtsClassFactory() : ref_count_(1) {}

    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) override
    {
        if (ppvObject == nullptr) return E_POINTER;
        *ppvObject = nullptr;
        if (riid == IID_IUnknown || riid == IID_IClassFactory) {
            *ppvObject = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        return E_NOINTERFACE;
    }

    STDMETHOD_(ULONG, AddRef)() override
    {
        return static_cast<ULONG>(InterlockedIncrement(&ref_count_));
    }

    STDMETHOD_(ULONG, Release)() override
    {
        const ULONG count = static_cast<ULONG>(InterlockedDecrement(&ref_count_));
        if (count == 0) delete this;
        return count;
    }

    STDMETHOD(CreateInstance)(IUnknown* outer, REFIID riid, void** ppvObject) override
    {
        if (ppvObject == nullptr) return E_POINTER;
        *ppvObject = nullptr;
        if (outer != nullptr) return CLASS_E_NOAGGREGATION;

        CTTSEngObj* object = new (std::nothrow) CTTSEngObj();
        if (object == nullptr) return E_OUTOFMEMORY;
        const HRESULT result = object->QueryInterface(riid, ppvObject);
        object->Release();
        return result;
    }

    STDMETHOD(LockServer)(BOOL lock) override
    {
        if (lock)
            InterlockedIncrement(&g_server_lock_count);
        else
            InterlockedDecrement(&g_server_lock_count);
        return S_OK;
    }

private:
    volatile LONG ref_count_;
};

HRESULT SetRegistryString(HKEY root, const wchar_t* subkey,
                          const wchar_t* value_name, const wchar_t* value)
{
    HKEY key = nullptr;
    const LSTATUS create_status = RegCreateKeyExW(
        root, subkey, 0, nullptr, REG_OPTION_NON_VOLATILE,
        KEY_SET_VALUE | KEY_WOW64_64KEY, nullptr, &key, nullptr);
    if (create_status != ERROR_SUCCESS)
        return HRESULT_FROM_WIN32(create_status);

    const size_t byte_count_wide = (wcslen(value) + 1) * sizeof(wchar_t);
    if (byte_count_wide > MAXDWORD) {
        RegCloseKey(key);
        return HRESULT_FROM_WIN32(ERROR_ARITHMETIC_OVERFLOW);
    }
    const DWORD byte_count = static_cast<DWORD>(byte_count_wide);
    const LSTATUS set_status = RegSetValueExW(
        key, value_name, 0, REG_SZ,
        reinterpret_cast<const BYTE*>(value), byte_count);
    RegCloseKey(key);
    return HRESULT_FROM_WIN32(set_status);
}

HRESULT RegisterServer()
{
    wchar_t module_path[MAX_PATH]{};
    if (GetModuleFileNameW(g_module, module_path, MAX_PATH) == 0)
        return HRESULT_FROM_WIN32(GetLastError());

    constexpr wchar_t kClsidKey[] =
        L"CLSID\\{BE985C8D-BE32-4A22-AA93-55C16A6D1D91}";
    constexpr wchar_t kInprocKey[] =
        L"CLSID\\{BE985C8D-BE32-4A22-AA93-55C16A6D1D91}\\InprocServer32";

    HRESULT result = SetRegistryString(HKEY_CLASSES_ROOT, kClsidKey, nullptr,
                                       L"eSpeak SAPI 5 Engine (32-bit)");
    if (SUCCEEDED(result))
        result = SetRegistryString(HKEY_CLASSES_ROOT, kInprocKey, nullptr, module_path);
    if (SUCCEEDED(result))
        result = SetRegistryString(HKEY_CLASSES_ROOT, kInprocKey,
                                   L"ThreadingModel", L"Both");
    return result;
}

HRESULT UnregisterServer()
{
    const LSTATUS status = RegDeleteTreeW(
        HKEY_CLASSES_ROOT,
        L"CLSID\\{BE985C8D-BE32-4A22-AA93-55C16A6D1D91}");
    return (status == ERROR_SUCCESS || status == ERROR_FILE_NOT_FOUND)
        ? S_OK : HRESULT_FROM_WIN32(status);
}

}  // namespace

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        g_module = instance;
        if (!InitializeCriticalSectionEx(&g_sapi_engine_lock, 4000, 0))
            return FALSE;
        g_sapi_engine_lock_initialized = true;
        DisableThreadLibraryCalls(instance);
    }
    else if (reason == DLL_PROCESS_DETACH && g_sapi_engine_lock_initialized) {
        DeleteCriticalSection(&g_sapi_engine_lock);
        g_sapi_engine_lock_initialized = false;
    }
    return TRUE;
}

STDAPI DllCanUnloadNow(void)
{
    return (g_module_object_count == 0 && g_server_lock_count == 0) ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr) return E_POINTER;
    *ppvObject = nullptr;
    if (rclsid != CLSID_SampleTTSEngine) return CLASS_E_CLASSNOTAVAILABLE;

    TtsClassFactory* factory = new (std::nothrow) TtsClassFactory();
    if (factory == nullptr) return E_OUTOFMEMORY;
    const HRESULT result = factory->QueryInterface(riid, ppvObject);
    factory->Release();
    return result;
}

STDAPI DllRegisterServer(void)
{
    return RegisterServer();
}

STDAPI DllUnregisterServer(void)
{
    return UnregisterServer();
}
