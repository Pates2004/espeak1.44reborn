#pragma once

#include <windows.h>
#include <wchar.h>

namespace VarioSettings {

struct SpeedSettings {
    bool boost;
    DWORD mode;
};

struct LegacySpeedSettings {
    bool present;
    bool boost;
    DWORD mode;
};

inline bool LegacyExists(const wchar_t* name)
{
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\eSpeak\\Vario", 0,
                     KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return false;
    const LONG result = RegQueryValueExW(key, name, nullptr, nullptr, nullptr, nullptr);
    RegCloseKey(key);
    return result == ERROR_SUCCESS;
}

inline DWORD ReadLegacy(const wchar_t* name, DWORD fallback)
{
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\eSpeak\\Vario", 0,
                     KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return fallback;
    DWORD value = fallback;
    DWORD type = 0;
    DWORD size = sizeof(value);
    const LONG result = RegQueryValueExW(key, name, nullptr, &type,
        reinterpret_cast<BYTE*>(&value), &size);
    RegCloseKey(key);
    return result == ERROR_SUCCESS && type == REG_DWORD && size == sizeof(value)
        ? value : fallback;
}

inline SpeedSettings ResolveSpeedSettings(const wchar_t* boost, const wchar_t* mode,
                                         const LegacySpeedSettings& legacy)
{
    const bool migrate = (boost[0] == L'\0' || mode[0] == L'\0') && legacy.present;
    SpeedSettings result = {migrate ? legacy.boost : true,
        migrate ? (legacy.mode <= 2 ? legacy.mode : 1) : 2};
    if (wcscmp(boost, L"0") == 0) result.boost = false;
    else if (wcscmp(boost, L"1") == 0) result.boost = true;
    if (wcscmp(mode, L"0") == 0) result.mode = 0;
    else if (wcscmp(mode, L"1") == 0) result.mode = 1;
    else if (wcscmp(mode, L"2") == 0) result.mode = 2;
    return result;
}

inline bool ReadSpeedValues(wchar_t* boost, DWORD boost_capacity, wchar_t* mode, DWORD mode_capacity)
{
    static const wchar_t module_marker = 0;
    HMODULE module = nullptr;
    wchar_t path[32768];
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                           GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           &module_marker, &module)) return false;
    const DWORD length = GetModuleFileNameW(module, path, _countof(path));
    if (length == 0 || length >= _countof(path)) return false;
    wchar_t* separator = wcsrchr(path, L'\\');
    if (separator == nullptr) return false;
    const size_t remaining = _countof(path) - static_cast<size_t>(separator + 1 - path);
    if (wcscpy_s(separator + 1, remaining, L"vario.ini") != 0) return false;
    GetPrivateProfileStringW(L"Settings", L"SonicBoost", L"", boost, boost_capacity, path);
    GetPrivateProfileStringW(L"Settings", L"SonicMode", L"", mode, mode_capacity, path);
    return true;
}

inline SpeedSettings ReadSpeedSettings()
{
    wchar_t boost[32] = {};
    wchar_t mode[32] = {};
    ReadSpeedValues(boost, _countof(boost), mode, _countof(mode));
    LegacySpeedSettings legacy = {};
    if (boost[0] == L'\0' || mode[0] == L'\0') {
        legacy.present = LegacyExists(L"SonicBoost") || LegacyExists(L"SonicMode");
        legacy.boost = ReadLegacy(L"SonicBoost", 0) != 0;
        legacy.mode = ReadLegacy(L"SonicMode", 1);
    }
    return ResolveSpeedSettings(boost, mode, legacy);
}

} // namespace VarioSettings
