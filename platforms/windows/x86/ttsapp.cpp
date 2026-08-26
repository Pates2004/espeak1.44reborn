// Lightweight 32-bit replacement for the SAPI test application shipped with
// the historical Windows package.  It uses only Win32 and SAPI.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <sapi.h>

namespace {

constexpr int kVoiceCombo = 1001;
constexpr int kTextEdit = 1002;
constexpr int kSpeakButton = 1003;
constexpr int kMaxVoices = 128;

ISpVoice* g_voice = nullptr;
ISpObjectToken* g_tokens[kMaxVoices]{};
int g_token_count = 0;

void ReleaseVoices()
{
    if (g_voice != nullptr) {
        g_voice->Speak(nullptr, SPF_PURGEBEFORESPEAK, nullptr);
        g_voice->Release();
        g_voice = nullptr;
    }
    for (int index = 0; index < g_token_count; ++index) {
        g_tokens[index]->Release();
        g_tokens[index] = nullptr;
    }
    g_token_count = 0;
}

void PopulateVoices(HWND combo)
{
    ISpObjectTokenCategory* category = nullptr;
    IEnumSpObjectTokens* enumerator = nullptr;

    if (FAILED(CoCreateInstance(CLSID_SpObjectTokenCategory, nullptr,
                                CLSCTX_INPROC_SERVER,
                                IID_ISpObjectTokenCategory,
                                reinterpret_cast<void**>(&category))))
        return;

    HRESULT result = category->SetId(SPCAT_VOICES, FALSE);
    if (SUCCEEDED(result))
        result = category->EnumTokens(nullptr, nullptr, &enumerator);

    while (SUCCEEDED(result) && enumerator != nullptr && g_token_count < kMaxVoices) {
        ISpObjectToken* token = nullptr;
        ULONG fetched = 0;
        if (enumerator->Next(1, &token, &fetched) != S_OK || fetched != 1)
            break;

        wchar_t* name = nullptr;
        if (FAILED(token->GetStringValue(nullptr, &name)) || name == nullptr) {
            CoTaskMemFree(name);
            name = nullptr;
        }

        const int item = static_cast<int>(SendMessageW(
            combo, CB_ADDSTRING, 0,
            reinterpret_cast<LPARAM>(name != nullptr ? name : L"SAPI voice")));
        CoTaskMemFree(name);

        if (item >= 0) {
            g_tokens[g_token_count] = token;
            SendMessageW(combo, CB_SETITEMDATA, item, g_token_count);
            ++g_token_count;
        } else {
            token->Release();
        }
    }

    if (enumerator != nullptr) enumerator->Release();
    category->Release();

    if (g_token_count > 0) SendMessageW(combo, CB_SETCURSEL, 0, 0);
}

void Layout(HWND window)
{
    RECT client{};
    GetClientRect(window, &client);
    const int margin = 12;
    const int button_width = 110;
    const int row_height = 28;

    MoveWindow(GetDlgItem(window, kVoiceCombo), margin, margin,
               client.right - 3 * margin - button_width, row_height, TRUE);
    MoveWindow(GetDlgItem(window, kSpeakButton),
               client.right - margin - button_width, margin,
               button_width, row_height, TRUE);
    MoveWindow(GetDlgItem(window, kTextEdit), margin, margin * 2 + row_height,
               client.right - 2 * margin,
               client.bottom - (margin * 3 + row_height), TRUE);
}

LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    switch (message) {
    case WM_CREATE: {
        HFONT font = static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
        HWND combo = CreateWindowExW(0, L"COMBOBOX", L"",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | CBS_DROPDOWNLIST | WS_VSCROLL,
            0, 0, 0, 0, window, reinterpret_cast<HMENU>(static_cast<INT_PTR>(kVoiceCombo)),
            GetModuleHandleW(nullptr), nullptr);
        HWND button = CreateWindowExW(0, L"BUTTON", L"Mów",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON,
            0, 0, 0, 0, window, reinterpret_cast<HMENU>(static_cast<INT_PTR>(kSpeakButton)),
            GetModuleHandleW(nullptr), nullptr);
        HWND edit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT",
            L"To jest test 32-bitowego silnika eSpeak.",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_MULTILINE | ES_AUTOVSCROLL |
                ES_WANTRETURN | WS_VSCROLL,
            0, 0, 0, 0, window, reinterpret_cast<HMENU>(static_cast<INT_PTR>(kTextEdit)),
            GetModuleHandleW(nullptr), nullptr);
        SendMessageW(combo, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        SendMessageW(button, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        SendMessageW(edit, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);

        if (SUCCEEDED(CoCreateInstance(CLSID_SpVoice, nullptr, CLSCTX_ALL,
                                       IID_ISpVoice,
                                       reinterpret_cast<void**>(&g_voice))))
            PopulateVoices(combo);
        return 0;
    }

    case WM_SIZE:
        Layout(window);
        return 0;

    case WM_COMMAND:
        if (LOWORD(wparam) == kSpeakButton && HIWORD(wparam) == BN_CLICKED &&
            g_voice != nullptr) {
            HWND combo = GetDlgItem(window, kVoiceCombo);
            const LRESULT selection = SendMessageW(combo, CB_GETCURSEL, 0, 0);
            if (selection != CB_ERR) {
                const LRESULT index = SendMessageW(combo, CB_GETITEMDATA, selection, 0);
                if (index >= 0 && index < g_token_count)
                    g_voice->SetVoice(g_tokens[index]);
            }

            HWND edit = GetDlgItem(window, kTextEdit);
            const int length = GetWindowTextLengthW(edit);
            wchar_t* text = static_cast<wchar_t*>(
                HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY,
                          static_cast<SIZE_T>(length + 1) * sizeof(wchar_t)));
            if (text != nullptr) {
                GetWindowTextW(edit, text, length + 1);
                g_voice->Speak(text, SPF_ASYNC | SPF_PURGEBEFORESPEAK, nullptr);
                HeapFree(GetProcessHeap(), 0, text);
            }
            return 0;
        }
        break;

    case WM_DESTROY:
        ReleaseVoices();
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(window, message, wparam, lparam);
}

}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int show_command)
{
    const HRESULT com_result = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(com_result)) return 1;

    WNDCLASSEXW window_class{};
    window_class.cbSize = sizeof(window_class);
    window_class.lpfnWndProc = WindowProc;
    window_class.hInstance = instance;
    window_class.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    window_class.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    window_class.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    window_class.lpszClassName = L"ESpeakTTSApp32";
    RegisterClassExW(&window_class);

    HWND window = CreateWindowExW(0, window_class.lpszClassName,
        L"eSpeak SAPI 5 — test x86", WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 640, 360,
        nullptr, nullptr, instance, nullptr);
    if (window == nullptr) {
        CoUninitialize();
        return 2;
    }

    ShowWindow(window, show_command);
    UpdateWindow(window);

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    CoUninitialize();
    return static_cast<int>(message.wParam);
}
