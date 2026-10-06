#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <sapi.h>
#include <sapiddk.h>
#include <stdio.h>
#include <vector>
#include <string>
#include "windows_sapi/vario_settings.h"

static bool VerifySettingsParsing() {
    using VarioSettings::LegacySpeedSettings;
    struct Scenario {
        const wchar_t* boost;
        const wchar_t* mode;
        LegacySpeedSettings legacy;
        bool expectedBoost;
        DWORD expectedMode;
    };
    const Scenario scenarios[] = {
        {L"", L"", {false, false, 0}, true, 2},
        {L"", L"", {true, false, 1}, false, 1},
        {L"", L"", {true, true, 99}, true, 1},
        {L"0", L"2", {true, true, 1}, false, 2},
        {L"invalid", L"99", {true, false, 0}, true, 2},
        {L"", L"99", {true, false, 1}, false, 1},
        {L"2", L"1", {true, false, 0}, true, 1},
        {L"1", L"0", {false, false, 0}, true, 0},
        {L"0", L"", {true, true, 2}, false, 2}
    };
    for (const Scenario& scenario : scenarios) {
        const auto result = VarioSettings::ResolveSpeedSettings(scenario.boost, scenario.mode, scenario.legacy);
        if (result.boost != scenario.expectedBoost || result.mode != scenario.expectedMode) return false;
    }
    return true;
}

struct RecordedEvent {
    SPEVENTENUM type;
    ULONGLONG offset;
    WPARAM value;
    LPARAM position;
    std::wstring bookmark;
};

class TestSite final : public ISpTTSEngineSite {
public:
    long rate = 0;
    std::vector<BYTE> audio;
    std::vector<RecordedEvent> events;
    size_t textLength = 0;
    bool failOutput = false;
    bool failEvents = false;
    bool zeroWrite = false;
    ULONG writeLimit = 0;
    size_t writeCalls = 0;
    bool abortDuringSpeech = false;
    size_t abortThreshold = 4096;
    bool abortReported = false;
    size_t audioAtAbort = 0;
    size_t eventsAtAbort = 0;
    size_t writesAfterAbort = 0;
    size_t eventsAfterAbort = 0;
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID id, void** object) override {
        if (!object) return E_POINTER;
        *object = nullptr;
        if (id != IID_IUnknown && id != __uuidof(ISpEventSink) &&
            id != __uuidof(ISpTTSEngineSite)) return E_NOINTERFACE;
        *object = static_cast<ISpTTSEngineSite*>(this);
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE AddEvents(const SPEVENT* input, ULONG count) override {
        if (abortReported) eventsAfterAbort += count;
        if (failEvents && count != 0) return E_FAIL;
        if (!input && count != 0) return E_POINTER;
        for (ULONG index = 0; index < count; index++) {
            const SPEVENT& event = input[index];
            RecordedEvent recorded{event.eEventId, event.ullAudioStreamOffset,
                event.wParam, event.lParam, {}};
            if (event.eEventId == SPEI_TTS_BOOKMARK) {
                if (event.elParamType != SPET_LPARAM_IS_STRING || !event.lParam) return E_FAIL;
                recorded.bookmark = reinterpret_cast<const wchar_t*>(event.lParam);
            }
            events.push_back(recorded);
        }
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetEventInterest(ULONGLONG* value) override {
        if (!value) return E_POINTER;
        *value = SPFEI(SPEI_WORD_BOUNDARY) | SPFEI(SPEI_TTS_BOOKMARK) | SPFEI(SPEI_VISEME);
        return S_OK;
    }
    DWORD STDMETHODCALLTYPE GetActions() override {
        if (abortDuringSpeech && audio.size() >= abortThreshold) {
            if (!abortReported) {
                abortReported = true;
                audioAtAbort = audio.size();
                eventsAtAbort = events.size();
            }
            return SPVES_ABORT;
        }
        return SPVES_RATE | SPVES_VOLUME;
    }
    HRESULT STDMETHODCALLTYPE Write(const void* data, ULONG length, ULONG* written) override {
        writeCalls++;
        if (abortReported) writesAfterAbort++;
        if (!written) return E_POINTER;
        *written = 0;
        if (failOutput) return E_FAIL;
        if (zeroWrite) return S_OK;
        if (!data || audio.size() + length > 16000000) return E_FAIL;
        if (writeLimit != 0 && length > writeLimit) length = writeLimit;
        const BYTE* bytes = static_cast<const BYTE*>(data);
        audio.insert(audio.end(), bytes, bytes + length);
        *written = length;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetRate(long* value) override { *value = rate; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetVolume(USHORT* value) override { *value = 100; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetSkipInfo(SPVSKIPTYPE*, long*) override { return E_NOTIMPL; }
    HRESULT STDMETHODCALLTYPE CompleteSkip(long) override { return S_OK; }

    bool ValidateEvents(bool mixed) const {
        ULONGLONG lastWord = 0;
        ULONGLONG lastViseme = 0;
        ULONGLONG lastBookmark = 0;
        size_t words = 0;
        size_t visemes = 0;
        std::vector<RecordedEvent> bookmarks;
        for (const RecordedEvent& event : events) {
            if (event.offset > audio.size() || event.offset % sizeof(short) != 0) {
                wprintf(L"Event outside PCM or between frames: type=%u, offset=%llu, PCM bytes=%zu\n",
                    static_cast<unsigned int>(event.type), event.offset, audio.size());
                return false;
            }
            if (event.type == SPEI_WORD_BOUNDARY) {
                if (event.offset < lastWord || event.position < 0 || event.value == 0 ||
                    static_cast<size_t>(event.position) > textLength ||
                    event.value > textLength - static_cast<size_t>(event.position)) {
                    wprintf(L"Invalid word event: offset=%llu, previous=%llu, text=%lld+%zu/%zu\n",
                        event.offset, lastWord, static_cast<long long>(event.position),
                        static_cast<size_t>(event.value), textLength);
                    return false;
                }
                lastWord = event.offset;
                words++;
            } else if (event.type == SPEI_VISEME) {
                const ULONGLONG duration = (static_cast<ULONGLONG>(event.position) >> 16) & 0xffffu;
                const ULONGLONG durationFrames = (duration * 22050u + 999u) / 1000u;
                if (durationFrames > (audio.size() - event.offset) / sizeof(short)) {
                    wprintf(L"Viseme ends outside PCM: offset=%llu, duration=%llums, PCM bytes=%zu\n",
                        event.offset, duration, audio.size());
                    return false;
                }
                if (event.offset < lastViseme) {
                    wprintf(L"Reversed viseme offset: %llu < %llu\n", event.offset, lastViseme);
                    return false;
                }
                lastViseme = event.offset;
                visemes++;
            } else if (event.type == SPEI_TTS_BOOKMARK) {
                if (event.offset < lastBookmark) {
                    wprintf(L"Reversed bookmark offset: %llu < %llu\n", event.offset, lastBookmark);
                    return false;
                }
                lastBookmark = event.offset;
                bookmarks.push_back(event);
            }
        }
        if (words == 0 || visemes == 0) {
            wprintf(L"Missing events: words=%zu, visemes=%zu\n", words, visemes);
            return false;
        }
        if (!mixed) {
            if (!bookmarks.empty()) wprintf(L"Unexpected public bookmarks: %zu\n", bookmarks.size());
            return bookmarks.empty();
        }
        if (visemes == 0 || bookmarks.size() != 3 || bookmarks[0].bookmark != L"1" ||
            bookmarks[1].bookmark != L"" || bookmarks[2].bookmark != L"3") {
            wprintf(L"Incorrect mixed bookmarks: count=%zu\n", bookmarks.size());
            return false;
        }
        const ULONGLONG first = bookmarks[0].offset;
        const ULONGLONG second = bookmarks[1].offset - bookmarks[0].offset;
        const ULONGLONG third = bookmarks[2].offset - bookmarks[1].offset;
        wprintf(L"Fragment PCM boundaries: %llu, %llu, %llu; words=%zu, visemes=%zu\n",
            first, second, third, words, visemes);
        return second < first && second < third;
    }
};

static bool WriteWave(const wchar_t* path, const std::vector<BYTE>& audio) {
    FILE* file = nullptr;
    if (_wfopen_s(&file, path, L"wb") != 0) return false;
    const DWORD header[] = {0x46464952, static_cast<DWORD>(audio.size() + 36),
        0x45564157, 0x20746d66, 16, 0x00010001, 22050, 44100, 0x00100002,
        0x61746164, static_cast<DWORD>(audio.size())};
    const bool success = fwrite(header, 1, sizeof(header), file) == sizeof(header) &&
        fwrite(audio.data(), 1, audio.size(), file) == audio.size();
    return fclose(file) == 0 && success;
}

int wmain(int argc, wchar_t** argv) {
    if (!VerifySettingsParsing()) return 9;
    // Caller provides a dedicated build directory, source data and output WAV.
    // No registration or replacement of the installed synthesizer is needed.
    if (argc != 5 && argc != 6) return 1;
    if (FAILED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED))) return 2;
    HMODULE module = LoadLibraryW(argv[1]);
    if (!module) return 3;
    using FactoryFunction = HRESULT(STDAPICALLTYPE*)(REFCLSID, REFIID, void**);
    auto get_factory = reinterpret_cast<FactoryFunction>(GetProcAddress(module, "DllGetClassObject"));
    if (!get_factory) return 4;
    const CLSID clsid = {0xBE985C8D, 0xBE32, 0x4A22, {0xAA, 0x93, 0x55, 0xC1, 0x6A, 0x6D, 0x1D, 0x91}};
    IClassFactory* factory = nullptr;
    ISpTTSEngine* engine = nullptr;
    HRESULT result = get_factory(clsid, IID_IClassFactory, reinterpret_cast<void**>(&factory));
    if (FAILED(result)) return 5;
    result = factory->CreateInstance(nullptr, __uuidof(ISpTTSEngine), reinterpret_cast<void**>(&engine));
    factory->Release();
    if (FAILED(result)) return 6;

    wchar_t token_path[200];
    swprintf_s(token_path, L"HKEY_CURRENT_USER\\SOFTWARE\\eSpeakSynthesisTest-%lu", GetCurrentProcessId());
    ISpObjectToken* token = nullptr;
    result = CoCreateInstance(CLSID_SpObjectToken, nullptr, CLSCTX_INPROC_SERVER,
                              __uuidof(ISpObjectToken), reinterpret_cast<void**>(&token));
    if (FAILED(result)) return 7;
    result = token->SetId(nullptr, token_path, TRUE);
    if (SUCCEEDED(result)) result = token->SetStringValue(L"VoiceName", L"pl");
    if (SUCCEEDED(result)) result = token->SetStringValue(L"Path", argv[2]);
    ISpObjectWithToken* token_owner = nullptr;
    if (SUCCEEDED(result)) result = engine->QueryInterface(__uuidof(ISpObjectWithToken),
                                                           reinterpret_cast<void**>(&token_owner));
    if (SUCCEEDED(result)) result = token_owner->SetObjectToken(token);
    if (token_owner) token_owner->Release();

    TestSite site;
    site.rate = _wtoi(argv[4]);
    const bool mixed = argc == 6 && wcscmp(argv[5], L"mixed") == 0;
    const bool repeat = argc == 6 && wcscmp(argv[5], L"repeat") == 0;
    site.failOutput = argc == 6 && wcscmp(argv[5], L"failwrite") == 0;
    site.failEvents = argc == 6 && wcscmp(argv[5], L"failevents") == 0;
    site.zeroWrite = argc == 6 && wcscmp(argv[5], L"zerowrite") == 0;
    site.abortDuringSpeech = argc == 6 && (wcscmp(argv[5], L"abort") == 0 ||
        wcscmp(argv[5], L"abortpartial") == 0 || wcscmp(argv[5], L"abortfirst") == 0);
    if (argc == 6 && wcscmp(argv[5], L"abortfirst") == 0) site.abortThreshold = 0;
    if (argc == 6 && (wcscmp(argv[5], L"partialwrite") == 0 ||
        wcscmp(argv[5], L"abortpartial") == 0)) site.writeLimit = 510;
    const wchar_t* text = L"Pierwszy bezinteresowny uczestnik powiedzia\u0142 sze\u015b\u0107set. To jest zwyk\u0142a pr\u00f3ba mowy.";
    SPVTEXTFRAG fragment{};
    fragment.State.eAction = SPVA_Speak;
    fragment.State.RateAdj = argc == 6 && !mixed ? _wtoi(argv[5]) : 0;
    fragment.State.Volume = 100;
    fragment.pTextStart = text;
    fragment.ulTextLen = static_cast<ULONG>(wcslen(text));
    site.textLength = fragment.ulTextLen;
    SPVTEXTFRAG mixedFragments[6]{};
    if (mixed) {
        const int adjustments[] = {-4, 4, -4};
        const wchar_t* bookmarks[] = {L"1", L"", L"3"};
        site.textLength = 0;
        for (size_t index = 0; index < 3; index++) {
            SPVTEXTFRAG& speech = mixedFragments[index * 2];
            speech = fragment;
            speech.State.RateAdj = adjustments[index];
            speech.ulTextSrcOffset = static_cast<ULONG>(site.textLength);
            site.textLength += fragment.ulTextLen + 1;
            speech.pNext = &mixedFragments[index * 2 + 1];
            SPVTEXTFRAG& bookmark = mixedFragments[index * 2 + 1];
            bookmark.State.eAction = SPVA_Bookmark;
            bookmark.pTextStart = bookmarks[index];
            bookmark.ulTextLen = static_cast<ULONG>(wcslen(bookmarks[index]));
            if (index < 2) bookmark.pNext = &mixedFragments[index * 2 + 2];
        }
    }
    WAVEFORMATEX format{WAVE_FORMAT_PCM, 1, 22050, 44100, 2, 16, 0};
    if (SUCCEEDED(result)) result = engine->Speak(0, SPDFID_WaveFormatEx, &format,
        mixed ? mixedFragments : &fragment, &site);
    bool nonzero = false;
    for (BYTE sample : site.audio) nonzero |= sample != 0;
    const bool expectsFailure = site.failOutput || site.failEvents || site.zeroWrite;
    bool eventsPassed = !expectsFailure && !site.abortDuringSpeech && site.ValidateEvents(mixed);
    bool passed = expectsFailure ? FAILED(result)
        : SUCCEEDED(result) && nonzero && site.audio.size() > 1000 &&
            eventsPassed && (repeat || WriteWave(argv[3], site.audio));
    if (site.writeLimit != 0 && !site.abortDuringSpeech) passed = passed && site.writeCalls > 1;
    if (site.abortDuringSpeech || repeat) {
        const bool stopped = repeat ? passed : result == S_OK && site.abortReported &&
            site.audio.size() == site.audioAtAbort && site.events.size() == site.eventsAtAbort &&
            site.writesAfterAbort == 0 && site.eventsAfterAbort == 0 &&
            (site.abortThreshold == 0 || !site.audio.empty());
        TestSite resumed;
        resumed.rate = site.rate;
        resumed.textLength = site.textLength;
        const HRESULT resumedResult = engine->Speak(0, SPDFID_WaveFormatEx, &format, &fragment, &resumed);
        bool resumedNonzero = false;
        for (BYTE sample : resumed.audio) resumedNonzero |= sample != 0;
        eventsPassed = resumed.ValidateEvents(false);
        passed = stopped && resumedResult == S_OK && resumedNonzero && eventsPassed &&
            resumed.audio.size() > 1000 && (repeat || resumed.audio.size() > site.audio.size()) &&
            WriteWave(argv[3], resumed.audio);
        wprintf(L"SAPI continuation: passed=%d, tail writes=%zu, tail events=%zu, resumed=%08lx, resumed PCM=%zu, events=%zu\n",
            stopped, site.writesAfterAbort, site.eventsAfterAbort, resumedResult, resumed.audio.size(), resumed.events.size());
    }
    engine->Release();
    token->Release();
    wchar_t subkey[160];
    swprintf_s(subkey, L"SOFTWARE\\eSpeakSynthesisTest-%lu", GetCurrentProcessId());
    const LONG cleanup = RegDeleteTreeW(HKEY_CURRENT_USER, subkey);
    FreeLibrary(module);
    CoUninitialize();
    wprintf(L"SAPI synthesis: HRESULT=%08lx, PCM bytes=%zu, events=%zu, valid=%d\n",
        result, site.audio.size(), site.events.size(), eventsPassed);
    return passed && cleanup == ERROR_SUCCESS ? 0 : 8;
}
