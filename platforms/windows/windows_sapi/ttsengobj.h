/******************************************************************************
* TtsEngObj.h - native Win64 SAPI 5 engine object
******************************************************************************/
#ifndef TtsEngObj_h
#define TtsEngObj_h

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <sapi.h>
#include <sapiddk.h>

#include "TtsEng.h"

extern volatile LONG g_module_object_count;
void LockSapiEngine();
void UnlockSapiEngine();

class CTTSEngObj final : public ISpTTSEngine, public ISpObjectWithToken
{
public:
    CTTSEngObj();
    virtual ~CTTSEngObj();

    CTTSEngObj(const CTTSEngObj&) = delete;
    CTTSEngObj& operator=(const CTTSEngObj&) = delete;

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) override;
    STDMETHOD_(ULONG, AddRef)() override;
    STDMETHOD_(ULONG, Release)() override;

    // ISpObjectWithToken
    STDMETHOD(SetObjectToken)(ISpObjectToken* pToken) override;
    STDMETHOD(GetObjectToken)(ISpObjectToken** ppToken) override;

    // ISpTTSEngine
    STDMETHOD(Speak)(DWORD dwSpeakFlags,
                     REFGUID rguidFormatId,
                     const WAVEFORMATEX* pWaveFormatEx,
                     const SPVTEXTFRAG* pTextFragList,
                     ISpTTSEngineSite* pOutputSite) override;
    STDMETHOD(GetOutputFormat)(const GUID* pTargetFormatId,
                               const WAVEFORMATEX* pTargetWaveFormatEx,
                               GUID* pDesiredFormatId,
                               WAVEFORMATEX** ppCoMemDesiredWaveFormatEx) override;

    HRESULT CheckActions(ISpTTSEngineSite* pOutputSite);
    HRESULT ProcessFragList(const SPVTEXTFRAG* pTextFragList, wchar_t* output,
                            size_t output_capacity, ISpTTSEngineSite* output_site,
                            size_t* output_length, size_t* text_fragment_count);
    HRESULT WritePhonemes(const SPPHONEID* phons, wchar_t* output,
                          size_t output_capacity, size_t* output_length);

private:
    HRESULT FinalConstruct();
    void FinalRelease();

    volatile LONG m_ref_count;
    ISpObjectToken* m_cpToken;
    const SPVTEXTFRAG* m_pCurrFrag;
    const WCHAR* m_pNextChar;
    const WCHAR* m_pEndChar;
    ULONGLONG m_ullAudioOff;
    char voice_name[80];
};

#endif
