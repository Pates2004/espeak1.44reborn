/***************************************************************************
 *   Copyright (C) 2005 to 2007 by Jonathan Duddington                     *
 *   email: jonsd@users.sourceforge.net                                    *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 3 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 *   This program is distributed in the hope that it will be useful,       *
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         *
 *   GNU General Public License for more details.                          *
 *                                                                         *
 *   You should have received a copy of the GNU General Public License     *
 *   along with this program; if not, write see:                           *
 *               <http://www.gnu.org/licenses/>.                           *
 ***************************************************************************/

#include "TtsEngObj.h"

#include "../../../src/speak_lib.h"
#include "../third_party/sonic/sonic.h"
#include <limits.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define CTRL_EMBEDDED  1

static CTTSEngObj *m_EngObj = NULL;
static ISpTTSEngineSite* m_OutputSite = NULL;
FILE *f_log2=NULL;
ULONGLONG event_interest;

extern int AddNameData(const char *name, int wide);
extern void InitNamedata(void);

int master_volume = 100;
int master_rate = 0;

int initialised = 0;
int gVolume = 100;
int gSpeed = -1;
int gPitch = -1;
int gRange = -1;
int gEmphasis = 0;
int gSayas = 0;
char g_voice_name[80];


char *path_install = NULL;

uint64_t audio_offset = 0;
uint64_t audio_latest = 0;
int prev_phoneme = 0;
uint64_t prev_phoneme_position = 0;
uint64_t prev_phoneme_time = 0;

size_t gBufCapacity = 0;
wchar_t *TextBuf=NULL;

typedef struct {
	size_t bufix;
	size_t textix;
	size_t cmdlen;
} FRAG_OFFSET;

int srate;   // samplerate, Hz/50
size_t n_frag_offsets = 0;
size_t frag_ix = 0;
size_t frag_count=0;
FRAG_OFFSET *frag_offsets = NULL;
sonicStream sonic_stream = NULL;
float sonic_speed = 1.0f;

namespace {

class SapiEngineGuard final
{
public:
	SapiEngineGuard() { LockSapiEngine(); }
	~SapiEngineGuard() { UnlockSapiEngine(); }

	SapiEngineGuard(const SapiEngineGuard&) = delete;
	SapiEngineGuard& operator=(const SapiEngineGuard&) = delete;
};

class SynthesisContext final
{
public:
	SynthesisContext(CTTSEngObj* engine, ISpTTSEngineSite* output_site)
		: previous_engine_(m_EngObj), previous_output_site_(m_OutputSite)
	{
		m_EngObj = engine;
		m_OutputSite = output_site;
	}

	~SynthesisContext()
	{
		m_EngObj = previous_engine_;
		m_OutputSite = previous_output_site_;
	}

	SynthesisContext(const SynthesisContext&) = delete;
	SynthesisContext& operator=(const SynthesisContext&) = delete;

private:
	CTTSEngObj* previous_engine_;
	ISpTTSEngineSite* previous_output_site_;
};

bool CheckedAddSize(size_t left, size_t right, size_t* result)
{
	const size_t maximum = ~(size_t)0;
	if((result == NULL) || (right > maximum - left))
		return false;
	*result = left + right;
	return true;
}

bool CheckedMultiplySize(size_t left, size_t right, size_t* result)
{
	const size_t maximum = ~(size_t)0;
	if((result == NULL) || ((left != 0) && (right > maximum / left)))
		return false;
	*result = left * right;
	return true;
}

ULONGLONG AudioStreamOffset(uint64_t audio_position_ms)
{
	if(srate <= 0)
		return ULLONG_MAX;
	const long double scaled = ((long double)audio_position_ms * (long double)srate) /
		(10.0L * ((sonic_speed > 1.0f) ? (long double)sonic_speed : 1.0L));
	return (scaled >= (long double)ULLONG_MAX) ? ULLONG_MAX : (ULONGLONG)scaled;
}

bool IsSonicBoostEnabled()
{
	HKEY key = NULL;
	DWORD value = 0;
	DWORD type = 0;
	DWORD size = sizeof(value);
	if(RegOpenKeyExW(HKEY_LOCAL_MACHINE,L"SOFTWARE\\eSpeak\\Vario",0,KEY_QUERY_VALUE,&key) != ERROR_SUCCESS)
		return false;
	const LONG result = RegQueryValueExW(key,L"SonicBoost",NULL,&type,
		reinterpret_cast<BYTE*>(&value),&size);
	RegCloseKey(key);
	return (result == ERROR_SUCCESS) && (type == REG_DWORD) && (value != 0);
}

float GetSonicSpeed()
{
	if(!IsSonicBoostEnabled() || (master_rate <= 0))
		return 1.0f;
	const int positive_rate = (master_rate > 10) ? 10 : master_rate;
	return 1.0f + ((float)positive_rate * 0.2f);
}

int DrainSonicOutput()
{
	short output[4096];
	while((sonic_stream != NULL) && (sonicSamplesAvailable(sonic_stream) > 0))
	{
		const int available = sonicSamplesAvailable(sonic_stream);
		const int requested = (available > (int)(sizeof(output)/sizeof(output[0])))
			? (int)(sizeof(output)/sizeof(output[0])) : available;
		const int count = sonicReadShortFromStream(sonic_stream,output,requested);
		if(count <= 0)
			return 1;
		const HRESULT result = m_OutputSite->Write(output,
			(ULONG)((unsigned int)count*sizeof(short)),NULL);
		if(FAILED(result))
			return 1;
	}
	return 0;
}

uint64_t AddAudioPosition(uint64_t base, int position)
{
	if(position <= 0)
		return base;
	const uint64_t increment = (uint64_t)position;
	return (increment > ULLONG_MAX - base) ? ULLONG_MAX : base + increment;
}

LPARAM SizeToLParam(size_t value)
{
	const size_t maximum = (size_t)INT64_MAX;
	return (LPARAM)((value > maximum) ? maximum : value);
}

bool AppendEmbeddedCommand(char* buffer, size_t capacity, size_t* length,
	int value, char suffix)
{
	if((buffer == NULL) || (length == NULL) || (*length >= capacity))
		return false;
	const int written = _snprintf_s(&buffer[*length],capacity-*length,_TRUNCATE,
		"%c%d%c",CTRL_EMBEDDED,value,suffix);
	if(written < 0)
		return false;
	*length += (size_t)written;
	return true;
}

bool EnsureFragOffsetCapacity(size_t required)
{
	if(required <= n_frag_offsets)
		return true;

	size_t new_capacity;
	size_t allocation_size;
	if(!CheckedAddSize(required,499u,&new_capacity))
		return false;
	new_capacity -= new_capacity % 500u;
	if(!CheckedMultiplySize(new_capacity,sizeof(FRAG_OFFSET),&allocation_size))
		return false;

	FRAG_OFFSET* resized = (FRAG_OFFSET*)realloc(frag_offsets,allocation_size);
	if(resized == NULL)
		return false;
	frag_offsets = resized;
	n_frag_offsets = new_capacity;
	return true;
}

}  // namespace


//#define TEST_INPUT    // printf input text received from SAPI to espeak_text_log.txt
#ifdef TEST_INPUT
static int utf8_out(unsigned int c, char *buf)
{//====================================
// write a unicode character into a buffer as utf8
// returns the number of bytes written
	int n_bytes;
	int j;
	int shift;
	static char unsigned code[4] = {0,0xc0,0xe0,0xf0};

	if(c < 0x80)
	{
		buf[0] = c;
		return(1);
	}
	if(c >= 0x110000)
	{
		buf[0] = ' ';      // out of range character code
		return(1);
	}
	if(c < 0x0800)
		n_bytes = 1;
	else
	if(c < 0x10000)
		n_bytes = 2;
	else
		n_bytes = 3;

	shift = 6*n_bytes;
	buf[0] = code[n_bytes] | (c >> shift);
	for(j=0; j<n_bytes; j++)
	{
		shift -= 6;
		buf[j+1] = 0x80 + ((c >> shift) & 0x3f);
	}
	return(n_bytes+1);
}  // end of utf8_out
#endif


int VisemeCode(unsigned int phoneme_name)
{//======================================
// Convert eSpeak phoneme name into a SAPI viseme code

	int ix;
	unsigned int ph;
	unsigned int ph_name;

#define PH(c1,c2)  (c2<<8)+c1          // combine two characters into an integer for phoneme name 

	const unsigned char initial_to_viseme[128] = {
		 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		 0, 0, 0, 0, 0, 0, 1, 0, 0, 0,19, 0, 0, 0, 0, 0,
		 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,255,
		 4, 2,18,16,17, 4,18,20,12, 6,16,20,14,21,20, 3,
		21,20,13,16,17, 4, 1, 5,20, 7,16, 0, 0, 0, 0, 0,
		 0, 1,21,16,19, 4,18,20,12, 6, 6,20,14,21,19, 8,
		21,20,13,15,19, 7,18, 7,20, 7,15, 0, 0, 0, 0, 0 };

	const unsigned int viseme_exceptions[] = {
		PH('a','I'), 11,
		PH('a','U'),  9,
		PH('O','I'), 10,
		PH('t','S'), 16,
		PH('d','Z'), 16,
		PH('_','|'), 255,
		0
	};
	
	ph_name = phoneme_name & 0xffff;
	for(ix=0; (ph = viseme_exceptions[ix]) != 0; ix+=2)
	{
		if(ph == ph_name)
		{
			return(viseme_exceptions[ix+1]);
		}
	}
	return(initial_to_viseme[phoneme_name & 0x7f]);
}


int SynthCallback(short *wav, int numsamples, espeak_EVENT *events);

int SynthCallback(short *wav, int numsamples, espeak_EVENT *events)
{//================================================================
	HRESULT hr;
	wchar_t *tailptr;
	size_t text_offset;
	size_t length;
	uint64_t phoneme_duration;
	int this_viseme;

	espeak_EVENT *event;
#define N_EVENTS 100
	int n_Events = 0;
	SPEVENT *Event;
	SPEVENT Events[N_EVENTS];

	if((m_EngObj == NULL) || (m_OutputSite == NULL) || (events == NULL))
		return(1);

	if(m_OutputSite->GetActions() & SPVES_ABORT)
		return(1);

	m_EngObj->CheckActions(m_OutputSite);

	// return the events
	for(event=events; (event->type != 0) && (n_Events < N_EVENTS); event++)
	{

		audio_latest = AddAudioPosition(audio_offset,event->audio_position);

		if((event->type == espeakEVENT_WORD) && (event->length > 0) &&
			(frag_count > 0) && (frag_offsets != NULL))
		{
			const size_t event_position = (event->text_position > 0)
				? (size_t)(event->text_position-1) : 0;
			while(((frag_ix+1) < frag_count) &&
				(event_position >= frag_offsets[frag_ix+1].bufix -
					((frag_offsets[frag_ix+1].cmdlen < frag_offsets[frag_ix+1].bufix)
						? frag_offsets[frag_ix+1].cmdlen : frag_offsets[frag_ix+1].bufix)))
			{
				frag_ix++;
			}

			const size_t command_length = frag_offsets[frag_ix].cmdlen;
			const size_t adjusted_position = event_position + command_length;
			const size_t relative_position = (adjusted_position >= frag_offsets[frag_ix].bufix)
				? adjusted_position-frag_offsets[frag_ix].bufix : 0;
			if(!CheckedAddSize(frag_offsets[frag_ix].textix,relative_position,&text_offset))
				text_offset = ~(size_t)0;
			length = ((size_t)event->length > command_length)
				? (size_t)event->length-command_length : 0;
			frag_offsets[frag_ix].cmdlen = 0;

			Event = &Events[n_Events++];
			Event->eEventId             = SPEI_WORD_BOUNDARY;
			Event->elParamType          = SPET_LPARAM_IS_UNDEFINED;
			Event->ullAudioStreamOffset = AudioStreamOffset(audio_latest);
			Event->lParam               = SizeToLParam(text_offset);
			Event->wParam               = (WPARAM)length;
		}
		if((event->type == espeakEVENT_MARK) && (event->id.name != NULL))
		{
			Event = &Events[n_Events++];
			Event->eEventId             = SPEI_TTS_BOOKMARK;
			Event->elParamType          = SPET_LPARAM_IS_STRING;
			Event->ullAudioStreamOffset = AudioStreamOffset(audio_latest);
			Event->lParam               = reinterpret_cast<LPARAM>(event->id.name);
			Event->wParam               = wcstol((wchar_t *)event->id.name,&tailptr,10);
		}
		if(event->type == espeakEVENT_PHONEME)
		{
			if(event_interest & SPEI_VISEME)
			{
				phoneme_duration = (audio_latest >= prev_phoneme_time)
					? audio_latest-prev_phoneme_time : 0;

				// ignore some phonemes (which translate to viseme=255)
				if((this_viseme = VisemeCode(event->id.number)) != 255)
				{
					Event = &Events[n_Events++];
					Event->eEventId             = SPEI_VISEME;
					Event->elParamType          = SPET_LPARAM_IS_UNDEFINED;
					Event->ullAudioStreamOffset = AudioStreamOffset(prev_phoneme_position);
					if(phoneme_duration > 0xffffu)
						phoneme_duration = 0xffffu;
					Event->lParam               = (LPARAM)((phoneme_duration << 16) | (uint64_t)this_viseme);
					Event->wParam               = VisemeCode(prev_phoneme);

					prev_phoneme = event->id.number;
					prev_phoneme_time = audio_latest;
					prev_phoneme_position = audio_latest;
				}
			}
		}
#ifdef deleted
		if(event->type == espeakEVENT_SENTENCE)
		{
			Event = &Events[n_Events++];
			Event->eEventId             = SPEI_SENTENCE_BOUNDARY;
			Event->elParamType          = SPET_LPARAM_IS_UNDEFINED;
			Event->ullAudioStreamOffset = ((event->audio_position + audio_offset) * srate)/10;  // ms -> bytes
			Event->lParam               = 0;
			Event->wParam               = 0;  // TEMP
		}
#endif
	}
	if(n_Events > 0)
	{
		hr = m_OutputSite->AddEvents(Events, n_Events );
		if(FAILED(hr))
			return(1);
	}

	// return the sound data
	if(numsamples <= 0)
		return(0);
	if((wav == NULL) || ((unsigned int)numsamples > (ULONG_MAX/sizeof(short))))
		return(1);
	if(sonic_stream != NULL)
	{
		if(!sonicWriteShortToStream(sonic_stream,wav,numsamples))
			return(1);
		return DrainSonicOutput();
	}
	hr = m_OutputSite->Write(wav,(ULONG)((unsigned int)numsamples*sizeof(short)),NULL);
	return(FAILED(hr) ? 1 : 0);
}



static int ConvertRate(int new_rate)
{//=================================

	int rate;

	static int rate_table[21] = {80,110,124,133,142,151,159,168,174,180,187,
				    196,208,220,240,270,300,335,369,390,450 };

	rate = new_rate + master_rate;
	if(rate < -10) rate = -10;
	if(rate > 10) rate = 10;
	return(rate_table[rate+10]);
}  // end of ConvertRate


static int ConvertPitch(int pitch)
{//===============================
	static int pitch_table[41] =
                {0, 0, 0, 0, 0, 0, 0, 0, 4, 8,12,16,20,24,28,32,36,40,44,47,50,
		54,58,62,66,70,74,78,82,84,88,92,96,99,99,99,99,99,99,99,99};
//		{0,3,5,8,10,13,15,18,20,23,25,28,30,33,35,38,40,43,45,48,50,
//		53,55,58,60,63,65,68,70,73,75,78,80,83,85,88,90,93,95,97,99};
	if(pitch < -20) pitch = -20;
	if(pitch > 20) pitch = 20;
	return(pitch_table[pitch+20]);
}

static int ConvertRange(int range)
{//===============================
	static int range_table[21] = {16,28,39,49,58,66,74,81,88,94,100,105,110,115,120,125,130,135,140,145,150};
	if(range < -10) range = -10;
	if(range > 10) range = 10;
	return(range_table[range+10]/2);
}

CTTSEngObj::CTTSEngObj()
    : m_ref_count(1),
      m_cpToken(nullptr),
      m_pCurrFrag(nullptr),
      m_pNextChar(nullptr),
      m_pEndChar(nullptr),
      m_ullAudioOff(0)
{
    voice_name[0] = 0;
    InterlockedIncrement(&g_module_object_count);
    FinalConstruct();
}

CTTSEngObj::~CTTSEngObj()
{
    FinalRelease();
    if (m_cpToken != nullptr)
        m_cpToken->Release();
    InterlockedDecrement(&g_module_object_count);
}

STDMETHODIMP CTTSEngObj::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr) return E_POINTER;
    *ppvObject = nullptr;

    if (riid == IID_IUnknown || riid == __uuidof(ISpTTSEngine))
        *ppvObject = static_cast<ISpTTSEngine*>(this);
    else if (riid == __uuidof(ISpObjectWithToken))
        *ppvObject = static_cast<ISpObjectWithToken*>(this);
    else
        return E_NOINTERFACE;

    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) CTTSEngObj::AddRef()
{
    return static_cast<ULONG>(InterlockedIncrement(&m_ref_count));
}

STDMETHODIMP_(ULONG) CTTSEngObj::Release()
{
    const ULONG count = static_cast<ULONG>(InterlockedDecrement(&m_ref_count));
    if (count == 0) delete this;
    return count;
}

HRESULT CTTSEngObj::FinalConstruct()
{//=================================
    HRESULT hr = S_OK;

#ifdef LOG_DEBUG
f_log2=fopen("C:\\log_espeak","a");
if(f_log2) fprintf(f_log2,"\n****\n");
#endif

    return hr;
} /* CTTSEngObj::FinalConstruct */


void CTTSEngObj::FinalRelease()
{//============================
#ifdef LOG_DEBUG
if(f_log2!=NULL) fclose(f_log2);
#endif
} /* CTTSEngObj::FinalRelease */


//
//=== ISpObjectWithToken Implementation ======================================
//

int WcharToChar(char *out, const wchar_t *in, int len)
{//====================================================
	if(out == NULL || len <= 0)
		return(0);
	out[0] = 0;
	if(in == NULL)
		return(0);
	if(WideCharToMultiByte(CP_ACP, 0, in, -1, out, len, NULL, NULL) == 0)
	{
		out[len-1] = 0;
		return(0);
	}
	return(1);
}

STDMETHODIMP CTTSEngObj::GetObjectToken(ISpObjectToken** ppToken)
{
	SapiEngineGuard guard;
	if(ppToken == NULL)
		return E_POINTER;
	*ppToken = m_cpToken;
	if(m_cpToken == NULL)
		return S_FALSE;
	m_cpToken->AddRef();
	return S_OK;
}


/*****************************************************************************
* CTTSEngObj::SetObjectToken *
*----------------------------*
*   Description:
*   Read the "VoiceName" attribute from the registry, and use it to select
*   an eSpeak voice file
*****************************************************************************/
STDMETHODIMP CTTSEngObj::SetObjectToken(ISpObjectToken * pToken)
{
	if(pToken == NULL)
		return E_INVALIDARG;
	SapiEngineGuard guard;
	if(m_cpToken != NULL)
		return HRESULT_FROM_WIN32(ERROR_ALREADY_INITIALIZED);

	char new_voice_name[sizeof(voice_name)];
	strcpy_s(new_voice_name,sizeof(new_voice_name),"default");
	char* new_path = NULL;
	wchar_t *voicename = NULL;
	wchar_t *path = NULL;

	HRESULT value_result = pToken->GetStringValue(L"VoiceName",&voicename);
	if(SUCCEEDED(value_result))
	{
		if(!WcharToChar(new_voice_name,voicename,(int)sizeof(new_voice_name)))
		{
			CoTaskMemFree(voicename);
			return E_INVALIDARG;
		}
		CoTaskMemFree(voicename);
	}

	value_result = pToken->GetStringValue(L"Path",&path);
	if(SUCCEEDED(value_result))
	{
		const int path_length = WideCharToMultiByte(CP_ACP,0,path,-1,NULL,0,NULL,NULL);
		if(path_length <= 0)
		{
			CoTaskMemFree(path);
			return E_INVALIDARG;
		}
		new_path = (char*)malloc((size_t)path_length);
		if(new_path == NULL)
		{
			CoTaskMemFree(path);
			return E_OUTOFMEMORY;
		}
		if(!WcharToChar(new_path,path,path_length))
		{
			free(new_path);
			CoTaskMemFree(path);
			return E_INVALIDARG;
		}
		CoTaskMemFree(path);
	}

	const char* initialization_path = (new_path != NULL) ? new_path : path_install;
	if(initialised==0)
	{
		const int initialized_sample_rate =
			espeak_Initialize(AUDIO_OUTPUT_SYNCHRONOUS,100,initialization_path,1);
		if(initialized_sample_rate <= 0)
		{
			free(new_path);
			return E_FAIL;
		}
		srate = initialized_sample_rate/50;
		espeak_SetSynthCallback(SynthCallback);
		initialised = 1;
	}

	if(espeak_SetVoiceByName(new_voice_name) != EE_OK)
	{
		free(new_path);
		return E_INVALIDARG;
	}

	if(new_path != NULL)
	{
		free(path_install);
		path_install = new_path;
	}
	strcpy_s(voice_name,sizeof(voice_name),new_voice_name);
	strcpy_s(g_voice_name,sizeof(g_voice_name),new_voice_name);
	pToken->AddRef();
	m_cpToken = pToken;

	master_volume = 100;
	master_rate = 0;
	gVolume = 100;
	gSpeed = -1;
	gPitch = -1;
	gRange = -1;
	gEmphasis = 0;
	gSayas = 0;
	return S_OK;
} /* CTTSEngObj::SetObjectToken */

//
//=== ISpTTSEngine Implementation ============================================
//

#define L(c1,c2)  (c1<<8)+c2          // combine two characters into an integer

static char *phoneme_names_en[] = {
	NULL,NULL,NULL," ",NULL,NULL,NULL,NULL,"'",",",
	"A:","a","V","0","aU","@","aI",
	"b","tS","d","D","E","3:","eI",
	"f","g","h","I","i:","dZ","k",
	"l","m","n","N","oU","OI","p",
	"r","s","S","t","T","U","u:",
	"v","w","j","z","Z",
	NULL
 };



HRESULT CTTSEngObj::WritePhonemes(const SPPHONEID *phons, wchar_t *output,
	size_t output_capacity, size_t *output_length)
{//==============================================================
	static const size_t max_phone_ids = 4096;
	int maxph = 0;
	int skip = 0;
	size_t written = 0;
	espeak_VOICE *voice;

	if(output_length == NULL)
		return E_POINTER;
	*output_length = 0;
	if(phons == NULL)
		return E_INVALIDARG;

	voice = espeak_GetCurrentVoice();
	if((voice == NULL) || (voice->languages == NULL))
		return E_FAIL;
	const int lang = ((unsigned char)voice->languages[1] << 8) +
		(unsigned char)voice->languages[2];
	if(lang == L('e','n'))
		maxph = 49;
	if(maxph == 0)
		return S_OK;

	#define APPEND_PHONE_CHAR(value) \
		do { \
			if((output != NULL) && (written >= output_capacity)) return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER); \
			if(output != NULL) output[written] = (wchar_t)(value); \
			if(written == ~(size_t)0) return E_OUTOFMEMORY; \
			written++; \
		} while(0)

	APPEND_PHONE_CHAR('[');
	APPEND_PHONE_CHAR('[');
	bool terminated = false;
	for(size_t phone_index=0; phone_index<max_phone_ids; phone_index++)
	{
		const int ph = phons[phone_index];
		if(ph == 0)
		{
			terminated = true;
			break;
		}
		if(skip)
		{
			skip = 0;
			continue;
		}
		if((ph < 0) || (ph > maxph))
			continue;

		const int next_ph = (phone_index+1 < max_phone_ids) ? phons[phone_index+1] : 0;
		const char* next_name = ((next_ph >= 0) && (next_ph <= maxph))
			? phoneme_names_en[next_ph] : NULL;
		if(next_name != NULL)
		{
			if(next_name[0] == '\'')
			{
				APPEND_PHONE_CHAR('\'');
				skip = 1;
			}
			else if(next_name[0] == ',')
			{
				APPEND_PHONE_CHAR(',');
				skip = 1;
			}
		}

		const char* name = phoneme_names_en[ph];
		if(name != NULL)
		{
			for(size_t name_index=0; name[name_index] != 0; name_index++)
				APPEND_PHONE_CHAR((unsigned char)name[name_index]);
		}
	}
	if(!terminated)
		return E_INVALIDARG;
	APPEND_PHONE_CHAR(']');
	APPEND_PHONE_CHAR(']');
	#undef APPEND_PHONE_CHAR

	*output_length = written;
	return S_OK;
}


HRESULT CTTSEngObj::ProcessFragList(const SPVTEXTFRAG* pTextFragList,
	wchar_t *output, size_t output_capacity, ISpTTSEngineSite* pOutputSite,
	size_t *output_length, size_t *text_fragment_count)
{//==========================================================================
	static const size_t command_buffer_size = 50;
	static const size_t bookmark_measurement_size = 16;
	const SPVTEXTFRAG* slow = pTextFragList;
	const SPVTEXTFRAG* fast = pTextFragList;
	while((fast != NULL) && (fast->pNext != NULL))
	{
		slow = slow->pNext;
		fast = fast->pNext->pNext;
		if(slow == fast)
			return E_INVALIDARG;
	}

	if((pOutputSite == NULL) || (output_length == NULL) || (text_fragment_count == NULL))
		return E_POINTER;
	if((output != NULL) && (output_capacity == 0))
		return E_INVALIDARG;
	*output_length = 0;
	*text_fragment_count = 0;
	frag_count = 0;
	frag_ix = 0;

	size_t total = 0;
	size_t text_count = 0;
	while(pTextFragList != NULL)
	{
		const int action = pTextFragList->State.eAction;
		if(pOutputSite->GetActions() & SPVES_ABORT)
			break;
		if((pTextFragList->ulTextLen > 0) && (pTextFragList->pTextStart == NULL))
			return E_INVALIDARG;

		CheckActions(pOutputSite);
		const SPVSTATE *state = &pTextFragList->State;
		char cmdbuf[command_buffer_size] = {};
		size_t command_length = 0;
		size_t fragment_length = 0;
		size_t new_total = 0;

		switch(action)
		{
		case SPVA_SpellOut:
		case SPVA_Speak:
		{
			const int sayas = (action == SPVA_SpellOut) ? 0x12 : 0;
			const int volume = (state->Volume * master_volume)/100;
			const int speed = ConvertRate(state->RateAdj);
			const int pitch = ConvertPitch(state->PitchAdj.MiddleAdj);
			const int range = ConvertRange(state->PitchAdj.RangeAdj);
			const int emphasis = (state->EmphAdj != 0) ? 3 : 0;

			if((volume != gVolume) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,volume,'A')) return E_UNEXPECTED;
			if((speed != gSpeed) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,speed,'S')) return E_UNEXPECTED;
			if((pitch != gPitch) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,pitch,'P')) return E_UNEXPECTED;
			if((range != gRange) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,range,'R')) return E_UNEXPECTED;
			if((emphasis != gEmphasis) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,emphasis,'F')) return E_UNEXPECTED;
			if((sayas != gSayas) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,sayas,'Y')) return E_UNEXPECTED;

			gVolume = volume;
			gSpeed = speed;
			gPitch = pitch;
			gRange = range;
			gEmphasis = emphasis;
			gSayas = sayas;

			const size_t measured_commands = (output == NULL)
				? command_buffer_size-1 : command_length;
			if(!CheckedAddSize(measured_commands,(size_t)pTextFragList->ulTextLen,&fragment_length) ||
				((pTextFragList->ulTextLen > 0) && !CheckedAddSize(fragment_length,1u,&fragment_length)) ||
				!CheckedAddSize(total,fragment_length,&new_total))
				return E_OUTOFMEMORY;
			if((output != NULL) && (new_total >= output_capacity))
				return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);

			if(output != NULL)
			{
				if(!EnsureFragOffsetCapacity(text_count+1u))
					return E_OUTOFMEMORY;
				for(size_t index=0; index<command_length; index++)
					output[total+index] = (unsigned char)cmdbuf[index];
				frag_offsets[text_count].textix = (size_t)pTextFragList->ulTextSrcOffset;
				frag_offsets[text_count].bufix = total+command_length;
				frag_offsets[text_count].cmdlen = command_length;
				if(pTextFragList->ulTextLen > 0)
				{
					size_t text_bytes;
					if(!CheckedMultiplySize((size_t)pTextFragList->ulTextLen,sizeof(wchar_t),&text_bytes))
						return E_OUTOFMEMORY;
					memcpy(&output[total+command_length],pTextFragList->pTextStart,text_bytes);
					output[new_total-1] = L' ';
				}
				audio_offset = audio_latest;
			}
			total = new_total;
			text_count++;
			break;
		}

		case SPVA_Bookmark:
			if(output == NULL)
			{
				if(!CheckedAddSize(total,bookmark_measurement_size,&total))
					return E_OUTOFMEMORY;
			}
			else
			{
				size_t mark_characters;
				size_t mark_bytes;
				if(!CheckedAddSize((size_t)pTextFragList->ulTextLen,1u,&mark_characters) ||
					!CheckedMultiplySize(mark_characters,sizeof(wchar_t),&mark_bytes))
					return E_OUTOFMEMORY;
				wchar_t* markbuf = (wchar_t*)malloc(mark_bytes);
				if(markbuf == NULL)
					return E_OUTOFMEMORY;
				if(pTextFragList->ulTextLen > 0)
					memcpy(markbuf,pTextFragList->pTextStart,mark_bytes-sizeof(wchar_t));
				markbuf[pTextFragList->ulTextLen] = 0;
				const int index = AddNameData((const char*)markbuf,1);
				free(markbuf);
				if((index >= 0) && !AppendEmbeddedCommand(cmdbuf,sizeof(cmdbuf),&command_length,index,'M'))
					return E_UNEXPECTED;
				if(!CheckedAddSize(total,command_length,&new_total))
					return E_OUTOFMEMORY;
				if(new_total >= output_capacity)
					return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
				for(size_t command_index=0; command_index<command_length; command_index++)
					output[total+command_index] = (unsigned char)cmdbuf[command_index];
				total = new_total;
			}
			break;

		case SPVA_Pronounce:
		{
			size_t phoneme_length = 0;
			const size_t remaining = ((output != NULL) && (total < output_capacity))
				? output_capacity-total-1u : 0;
			HRESULT result = WritePhonemes(state->pPhoneIds,
				(output != NULL) ? &output[total] : NULL,remaining,&phoneme_length);
			if(FAILED(result))
				return result;
			if(!CheckedAddSize(total,phoneme_length,&new_total))
				return E_OUTOFMEMORY;
			if((output != NULL) && (new_total >= output_capacity))
				return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
			total = new_total;
			break;
		}
		}

		pTextFragList = pTextFragList->pNext;
	}

	if(output != NULL)
		output[total] = 0;
	frag_count = (output != NULL) ? text_count : 0;
	*text_fragment_count = text_count;
	*output_length = total;
	return S_OK;
}   // end of ProcessFragList



/*****************************************************************************
* CTTSEngObj::Speak *
*-------------------*
*   Description:
*       This is the primary method that SAPI calls to render text.
*-----------------------------------------------------------------------------
*   Input Parameters
*
*   pUser
*       Pointer to the current user profile object. This object contains
*       information like what languages are being used and this object
*       also gives access to resources like the SAPI master lexicon object.
*
*   dwSpeakFlags
*       This is a set of flags used to control the behavior of the
*       SAPI voice object and the associated engine.
*
*   VoiceFmtIndex
*       Zero based index specifying the output format that should
*       be used during rendering.
*
*   pTextFragList
*       A linked list of text fragments to be rendered. There is
*       one fragement per XML state change. If the input text does
*       not contain any XML markup, there will only be a single fragment.
*
*   pOutputSite
*       The interface back to SAPI where all output audio samples and events are written.
*
*   Return Values
*       S_OK - This should be returned after successful rendering or if
*              rendering was interrupted because *pfContinue changed to FALSE.
*       E_INVALIDARG 
*       E_OUTOFMEMORY
*
*****************************************************************************/
STDMETHODIMP CTTSEngObj::Speak( DWORD dwSpeakFlags,
                                REFGUID rguidFormatId,
                                const WAVEFORMATEX * pWaveFormatEx,
                                const SPVTEXTFRAG* pTextFragList,
                                ISpTTSEngineSite* pOutputSite )
{
	if((pOutputSite == NULL) || (pTextFragList == NULL))
		return E_INVALIDARG;

	SapiEngineGuard guard;
	if((initialised == 0) || (m_cpToken == NULL))
		return CO_E_NOTINITIALIZED;
	if((pTextFragList->ulTextLen > 0) && (pTextFragList->pTextStart == NULL))
		return E_INVALIDARG;
	if(strcmp(voice_name,g_voice_name) != 0)
	{
		if(espeak_SetVoiceByName(voice_name) != EE_OK)
			return E_INVALIDARG;
		strcpy_s(g_voice_name,sizeof(g_voice_name),voice_name);
	}

	InitNamedata();
	m_pCurrFrag = pTextFragList;
	m_pNextChar = m_pCurrFrag->pTextStart;
	m_pEndChar = (m_pNextChar != NULL) ? m_pNextChar+m_pCurrFrag->ulTextLen : NULL;
	m_ullAudioOff = 0;

	HRESULT result = pOutputSite->GetEventInterest(&event_interest);
	if(FAILED(result))
		return result;
	result = CheckActions(pOutputSite);
	if(FAILED(result))
		return result;
	sonic_speed = GetSonicSpeed();

	const int saved_volume = gVolume;
	const int saved_speed = gSpeed;
	const int saved_pitch = gPitch;
	const int saved_range = gRange;
	const int saved_emphasis = gEmphasis;
	const int saved_sayas = gSayas;
	size_t measured_characters = 0;
	size_t measured_fragments = 0;
	result = ProcessFragList(pTextFragList,NULL,0,pOutputSite,
		&measured_characters,&measured_fragments);
	gVolume = saved_volume;
	gSpeed = saved_speed;
	gPitch = saved_pitch;
	gRange = saved_range;
	gEmphasis = saved_emphasis;
	gSayas = saved_sayas;
	if(FAILED(result))
		return result;

	size_t required_characters;
	size_t required_bytes;
	if(!CheckedAddSize(measured_characters,1u,&required_characters) ||
		!CheckedMultiplySize(required_characters,sizeof(wchar_t),&required_bytes))
		return E_OUTOFMEMORY;
	if(required_characters > gBufCapacity)
	{
		wchar_t* resized = (wchar_t*)realloc(TextBuf,required_bytes);
		if(resized == NULL)
			return E_OUTOFMEMORY;
		TextBuf = resized;
		gBufCapacity = required_characters;
	}

	const int punctuation = (dwSpeakFlags & SPF_NLP_SPEAK_PUNC) ? 1 : 0;
	espeak_SetParameter(espeakPUNCTUATION,punctuation,0);
	audio_offset = 0;
	audio_latest = 0;
	prev_phoneme = 0;
	prev_phoneme_time = 0;
	prev_phoneme_position = 0;

	size_t text_characters = 0;
	size_t text_fragments = 0;
	result = ProcessFragList(pTextFragList,TextBuf,gBufCapacity,pOutputSite,
		&text_characters,&text_fragments);
	if(FAILED(result))
		return result;
	if((text_characters > measured_characters) || (text_fragments > measured_fragments))
		return E_UNEXPECTED;

	if(text_characters > 0)
	{
		SynthesisContext context(this,pOutputSite);
		if(sonic_speed > 1.0f)
		{
			sonic_stream = sonicCreateStream(srate*50,1);
			if(sonic_stream == NULL)
			{
				sonic_speed = 1.0f;
				return E_OUTOFMEMORY;
			}
			sonicSetSpeed(sonic_stream,sonic_speed);
		}

		const espeak_ERROR synth_result = espeak_Synth(TextBuf,0,0,POS_CHARACTER,0,
			espeakCHARS_WCHAR | espeakKEEP_NAMEDATA | espeakPHONEMES,NULL,NULL);
		int sonic_result = 0;
		if(sonic_stream != NULL)
		{
			if((synth_result == EE_OK) && !sonicFlushStream(sonic_stream))
				sonic_result = 1;
			if((sonic_result == 0) && (DrainSonicOutput() != 0))
				sonic_result = 1;
			sonicDestroyStream(sonic_stream);
			sonic_stream = NULL;
		}
		sonic_speed = 1.0f;
		if((synth_result != EE_OK) || (sonic_result != 0))
			return E_FAIL;
	}
	return S_OK;
} /* CTTSEngObj::Speak */





HRESULT CTTSEngObj::CheckActions( ISpTTSEngineSite* pOutputSite )
{//==============================================================
	if(pOutputSite == NULL)
		return E_POINTER;
	int control;
	USHORT volume;
	long rate;

	control = pOutputSite->GetActions();

	if(control & SPVES_VOLUME)
	{
		if(pOutputSite->GetVolume(&volume) == S_OK)
		{
			master_volume = volume;
		}
	}
	if(control & SPVES_RATE)
	{
		if(pOutputSite->GetRate(&rate) == S_OK)
		{
			master_rate = rate;
		}
	}

	return(S_OK);
}  // end of CTTSEngObj::CheckActions



STDMETHODIMP CTTSEngObj::GetOutputFormat( const GUID * pTargetFormatId, const WAVEFORMATEX * pTargetWaveFormatEx,
                                          GUID * pDesiredFormatId, WAVEFORMATEX ** ppCoMemDesiredWaveFormatEx )
{//========================================================================
	if(pDesiredFormatId == NULL || ppCoMemDesiredWaveFormatEx == NULL)
		return E_POINTER;
	SapiEngineGuard guard;
	*ppCoMemDesiredWaveFormatEx = NULL;

	DWORD sample_rate = 22050;

	srate = 441;
	if(espeak_GetParameter(espeakVOICETYPE,1) == 1)
	{
		srate = 320;
		sample_rate = 16000;   // an mbrola voice
	}

	WAVEFORMATEX *format = static_cast<WAVEFORMATEX*>(CoTaskMemAlloc(sizeof(WAVEFORMATEX)));
	if(format == NULL)
		return E_OUTOFMEMORY;
	ZeroMemory(format, sizeof(*format));
	format->wFormatTag = WAVE_FORMAT_PCM;
	format->nChannels = 1;
	format->nSamplesPerSec = sample_rate;
	format->wBitsPerSample = 16;
	format->nBlockAlign = 2;
	format->nAvgBytesPerSec = sample_rate * format->nBlockAlign;

	*pDesiredFormatId = SPDFID_WaveFormatEx;
	*ppCoMemDesiredWaveFormatEx = format;
	return S_OK;
} /* CTTSEngObj::GetVoiceFormat */



extern "C" int CompileDictionary(const char *voice, const char *path_log)
{//===========================================================
	SapiEngineGuard guard;
	if((voice == NULL) || (path_log == NULL) || (path_install == NULL))
		return(1);

	FILE *f_log3 = fopen(path_log,"w");
	if(f_log3 == NULL)
		return(1);
	size_t path_length;
	if(!CheckedAddSize(strlen(path_install),2u,&path_length))
	{
		fclose(f_log3);
		return(1);
	}
	char* fname = (char*)malloc(path_length);
	if(fname == NULL)
	{
		fclose(f_log3);
		return(1);
	}
	const int written = _snprintf_s(fname,path_length,_TRUNCATE,"%s/",path_install);
	int result = 1;
	if((written >= 0) && (espeak_SetVoiceByName(voice) == EE_OK))
	{
		espeak_CompileDictionary(fname,f_log3,0);
		result = 0;
	}
	free(fname);
	fclose(f_log3);
	return(result);
}


