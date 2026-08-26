// Minimal PortAudio v18 compatibility layer for the eSpeak 1.44 wavegen code.
// It uses the Windows multimedia API and removes the dependency on the
// bundled legacy PAStaticWMME.lib.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmsystem.h>

#include <new>

#include "../../../src/portaudio.h"

namespace {

constexpr unsigned int kBufferCount = 4;

struct WinMmStream {
    HWAVEOUT wave_out;
    HANDLE buffer_event;
    HANDLE stop_event;
    HANDLE worker;
    PortAudioCallback* callback;
    void* user_data;
    unsigned long frames_per_buffer;
    unsigned int channels;
    unsigned int bytes_per_frame;
    WAVEHDR headers[kBufferCount];
    unsigned char* buffers[kBufferCount];
    volatile LONG active;
    volatile LONG stop_requested;
    volatile LONG callback_finished;
    volatile LONG queued;
};

long g_last_host_error = MMSYSERR_NOERROR;

PaError MmResultToPa(MMRESULT result)
{
    g_last_host_error = result;
    switch (result) {
    case MMSYSERR_NOERROR:
        return paNoError;
    case MMSYSERR_NOMEM:
        return paInsufficientMemory;
    case MMSYSERR_BADDEVICEID:
        return paInvalidDeviceId;
    case WAVERR_BADFORMAT:
        return paSampleFormatNotSupported;
    case MMSYSERR_ALLOCATED:
        return paDeviceUnavailable;
    default:
        return paHostError;
    }
}

bool QueueBuffer(WinMmStream* stream, unsigned int index)
{
    WAVEHDR& header = stream->headers[index];
    ZeroMemory(&header, sizeof(header));
    header.lpData = reinterpret_cast<LPSTR>(stream->buffers[index]);
    header.dwBufferLength = stream->frames_per_buffer * stream->bytes_per_frame;

    const int finished = stream->callback(
        nullptr,
        header.lpData,
        stream->frames_per_buffer,
        0,
        stream->user_data);

    if (finished != 0) {
        InterlockedExchange(&stream->callback_finished, 1);
    }

    MMRESULT result = waveOutPrepareHeader(stream->wave_out, &header, sizeof(header));
    if (result != MMSYSERR_NOERROR) {
        g_last_host_error = result;
        return false;
    }

    result = waveOutWrite(stream->wave_out, &header, sizeof(header));
    if (result != MMSYSERR_NOERROR) {
        g_last_host_error = result;
        waveOutUnprepareHeader(stream->wave_out, &header, sizeof(header));
        return false;
    }

    InterlockedIncrement(&stream->queued);
    return true;
}

DWORD WINAPI AudioWorker(void* parameter)
{
    WinMmStream* stream = static_cast<WinMmStream*>(parameter);

    for (unsigned int index = 0;
         index < kBufferCount && stream->callback_finished == 0;
         ++index) {
        if (!QueueBuffer(stream, index)) {
            InterlockedExchange(&stream->stop_requested, 1);
            break;
        }
    }

    HANDLE events[2] = {stream->stop_event, stream->buffer_event};
    while (stream->queued > 0 && stream->stop_requested == 0) {
        const DWORD wait_result = WaitForMultipleObjects(2, events, FALSE, INFINITE);
        if (wait_result == WAIT_OBJECT_0) {
            break;
        }
        if (wait_result != WAIT_OBJECT_0 + 1) {
            break;
        }

        for (unsigned int index = 0; index < kBufferCount; ++index) {
            WAVEHDR& header = stream->headers[index];
            if ((header.dwFlags & WHDR_PREPARED) != 0 &&
                (header.dwFlags & WHDR_DONE) != 0) {
                waveOutUnprepareHeader(stream->wave_out, &header, sizeof(header));
                InterlockedDecrement(&stream->queued);

                if (stream->stop_requested == 0 &&
                    stream->callback_finished == 0 &&
                    !QueueBuffer(stream, index)) {
                    InterlockedExchange(&stream->stop_requested, 1);
                }
            }
        }
    }

    if (stream->stop_requested != 0) {
        waveOutReset(stream->wave_out);
    }

    for (unsigned int index = 0; index < kBufferCount; ++index) {
        WAVEHDR& header = stream->headers[index];
        if ((header.dwFlags & WHDR_PREPARED) != 0) {
            while (waveOutUnprepareHeader(stream->wave_out, &header, sizeof(header)) ==
                   WAVERR_STILLPLAYING) {
                Sleep(1);
            }
        }
    }

    InterlockedExchange(&stream->queued, 0);
    InterlockedExchange(&stream->active, 0);
    return 0;
}

void StopWorker(WinMmStream* stream, bool abort)
{
    if (stream == nullptr) {
        return;
    }

    if (abort && stream->active != 0) {
        InterlockedExchange(&stream->stop_requested, 1);
        SetEvent(stream->stop_event);
        waveOutReset(stream->wave_out);
    }

    if (stream->worker != nullptr) {
        WaitForSingleObject(stream->worker, INFINITE);
        CloseHandle(stream->worker);
        stream->worker = nullptr;
    }
    InterlockedExchange(&stream->active, 0);
}

}  // namespace

extern "C" {

PaError Pa_Initialize(void)
{
    return paNoError;
}

PaError Pa_Terminate(void)
{
    return paNoError;
}

long Pa_GetHostError(void)
{
    return g_last_host_error;
}

const char* Pa_GetErrorText(PaError error)
{
    switch (error) {
    case paNoError: return "No error";
    case paHostError: return "Windows multimedia error";
    case paInvalidChannelCount: return "Invalid channel count";
    case paInvalidSampleRate: return "Invalid sample rate";
    case paInvalidDeviceId: return "Invalid device";
    case paSampleFormatNotSupported: return "Unsupported sample format";
    case paInsufficientMemory: return "Insufficient memory";
    case paNullCallback: return "Null audio callback";
    case paBadStreamPtr: return "Invalid stream";
    case paDeviceUnavailable: return "Audio device unavailable";
    default: return "Audio error";
    }
}

int Pa_CountDevices(void)
{
    return static_cast<int>(waveOutGetNumDevs());
}

PaDeviceID Pa_GetDefaultInputDeviceID(void)
{
    return paNoDevice;
}

PaDeviceID Pa_GetDefaultOutputDeviceID(void)
{
    return waveOutGetNumDevs() == 0 ? paNoDevice : 0;
}

const PaDeviceInfo* Pa_GetDeviceInfo(PaDeviceID device)
{
    static const double sample_rates[] = {8000, 11025, 16000, 22050, 32000, 44100, 48000};
    static const PaDeviceInfo device_info = {
        1, "Windows default waveOut device", 0, 2,
        static_cast<int>(sizeof(sample_rates) / sizeof(sample_rates[0])),
        sample_rates, paInt16
    };
    return (device >= 0 && device < Pa_CountDevices()) ? &device_info : nullptr;
}

PaError Pa_OpenDefaultStream(
    PortAudioStream** result_stream,
    int input_channels,
    int output_channels,
    PaSampleFormat sample_format,
    double sample_rate,
    unsigned long frames_per_buffer,
    unsigned long,
    PortAudioCallback* callback,
    void* user_data)
{
    if (result_stream == nullptr) return paBadStreamPtr;
    *result_stream = nullptr;
    if (callback == nullptr) return paNullCallback;
    if (input_channels != 0 || output_channels < 1 || output_channels > 2)
        return paInvalidChannelCount;
    if (sample_format != paInt16) return paSampleFormatNotSupported;
    if (sample_rate < 1000 || sample_rate > 192000) return paInvalidSampleRate;
    if (frames_per_buffer == 0) frames_per_buffer = 512;

#pragma warning(suppress: 28182)  // allocation is checked before the first dereference
    WinMmStream* stream = new (std::nothrow) WinMmStream{};
    if (stream == nullptr) return paInsufficientMemory;
#ifdef _MSC_VER
    __analysis_assume(stream != nullptr);
#endif

    stream->callback = callback;
    stream->user_data = user_data;
    stream->frames_per_buffer = frames_per_buffer;
    stream->channels = static_cast<unsigned int>(output_channels);
    stream->bytes_per_frame = stream->channels * sizeof(short);
    stream->buffer_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    stream->stop_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (stream->buffer_event == nullptr || stream->stop_event == nullptr) {
        Pa_CloseStream(reinterpret_cast<PortAudioStream*>(stream));
        return paInsufficientMemory;
    }

    for (unsigned int index = 0; index < kBufferCount; ++index) {
        stream->buffers[index] = static_cast<unsigned char*>(
            HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY,
                      frames_per_buffer * stream->bytes_per_frame));
        if (stream->buffers[index] == nullptr) {
            Pa_CloseStream(reinterpret_cast<PortAudioStream*>(stream));
            return paInsufficientMemory;
        }
    }

    WAVEFORMATEX format{};
    format.wFormatTag = WAVE_FORMAT_PCM;
    format.nChannels = static_cast<WORD>(output_channels);
    format.nSamplesPerSec = static_cast<DWORD>(sample_rate);
    format.wBitsPerSample = 16;
    format.nBlockAlign = static_cast<WORD>(output_channels * sizeof(short));
    format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;

    const MMRESULT open_result = waveOutOpen(
        &stream->wave_out, WAVE_MAPPER, &format,
        reinterpret_cast<DWORD_PTR>(stream->buffer_event), 0, CALLBACK_EVENT);
    if (open_result != MMSYSERR_NOERROR) {
        const PaError result = MmResultToPa(open_result);
        Pa_CloseStream(reinterpret_cast<PortAudioStream*>(stream));
        return result;
    }

    *result_stream = reinterpret_cast<PortAudioStream*>(stream);
    return paNoError;
}

PaError Pa_OpenStream(
    PortAudioStream** stream, PaDeviceID, int input_channels,
    PaSampleFormat, void*, PaDeviceID, int output_channels,
    PaSampleFormat output_format, void*, double sample_rate,
    unsigned long frames_per_buffer, unsigned long number_of_buffers,
    PaStreamFlags, PortAudioCallback* callback, void* user_data)
{
    return Pa_OpenDefaultStream(stream, input_channels, output_channels,
                                output_format, sample_rate, frames_per_buffer,
                                number_of_buffers, callback, user_data);
}

PaError Pa_StartStream(PortAudioStream* opaque_stream)
{
    WinMmStream* stream = reinterpret_cast<WinMmStream*>(opaque_stream);
    if (stream == nullptr || stream->wave_out == nullptr) return paBadStreamPtr;
    if (stream->active != 0) return paNoError;

    // A naturally completed worker leaves a signaled thread handle behind.
    // Reap it before starting the next utterance on the same stream.
    StopWorker(stream, false);

    ResetEvent(stream->stop_event);
    InterlockedExchange(&stream->stop_requested, 0);
    InterlockedExchange(&stream->callback_finished, 0);
    InterlockedExchange(&stream->queued, 0);
    InterlockedExchange(&stream->active, 1);
    stream->worker = CreateThread(nullptr, 0, AudioWorker, stream, 0, nullptr);
    if (stream->worker == nullptr) {
        InterlockedExchange(&stream->active, 0);
        return paInsufficientMemory;
    }
    return paNoError;
}

PaError Pa_StopStream(PortAudioStream* opaque_stream)
{
    WinMmStream* stream = reinterpret_cast<WinMmStream*>(opaque_stream);
    if (stream == nullptr) return paBadStreamPtr;
    StopWorker(stream, false);
    return paNoError;
}

PaError Pa_AbortStream(PortAudioStream* opaque_stream)
{
    WinMmStream* stream = reinterpret_cast<WinMmStream*>(opaque_stream);
    if (stream == nullptr) return paBadStreamPtr;
    StopWorker(stream, true);
    return paNoError;
}

PaError Pa_CloseStream(PortAudioStream* opaque_stream)
{
    WinMmStream* stream = reinterpret_cast<WinMmStream*>(opaque_stream);
    if (stream == nullptr) return paNoError;
    StopWorker(stream, true);
    if (stream->wave_out != nullptr) waveOutClose(stream->wave_out);
    if (stream->buffer_event != nullptr) CloseHandle(stream->buffer_event);
    if (stream->stop_event != nullptr) CloseHandle(stream->stop_event);
    for (unsigned int index = 0; index < kBufferCount; ++index) {
        if (stream->buffers[index] != nullptr)
            HeapFree(GetProcessHeap(), 0, stream->buffers[index]);
    }
    delete stream;
    return paNoError;
}

PaError Pa_StreamActive(PortAudioStream* opaque_stream)
{
    WinMmStream* stream = reinterpret_cast<WinMmStream*>(opaque_stream);
    return stream == nullptr ? paBadStreamPtr : (stream->active != 0 ? 1 : 0);
}

PaTimestamp Pa_StreamTime(PortAudioStream*) { return 0; }
double Pa_GetCPULoad(PortAudioStream*) { return 0; }
int Pa_GetMinNumBuffers(int, double) { return kBufferCount; }
void Pa_Sleep(long milliseconds) { Sleep(static_cast<DWORD>(milliseconds)); }
PaError Pa_GetSampleSize(PaSampleFormat format)
{
    return format == paInt16 ? static_cast<PaError>(sizeof(short)) : paSampleFormatNotSupported;
}

}  // extern "C"
