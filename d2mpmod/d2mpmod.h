#pragma once
#define VERSION_SAFE_STEAM_API_INTERFACES
#include <ISmmPlugin.h>
#include "script_helpers.h"
#include <jansson/jansson.h>
#include <enginecallback.h>
#include <vscript/ivscript.h>
#include <http.h>
#include <steam/isteamclient.h>
#include <steam/steam_gameserver.h>
#include <enginecallback.h>
#include <string>
#include <sstream>

#if defined WIN32 && !defined snprintf
#define snprintf _snprintf
#endif

#define LOG(msg) { META_LOG(&g_D2MPPlugin, msg); META_CONPRINT((std::string(msg)+"\n").c_str()); }

class D2MPPlugin : public ISmmPlugin
{
public:
	bool Load(PluginId id, ISmmAPI *ismm, char *error, size_t maxlen, bool late);
	bool Unload(char *error, size_t maxlen);
	bool Pause(char *error, size_t maxlen);
	bool Unpause(char *error, size_t maxlen);
	void AllPluginsLoaded();
public:
	const char *GetAuthor();
	const char *GetName();
	const char *GetDescription();
	const char *GetURL();
	const char *GetLicense();
	const char *GetVersion();
	const char *GetDate();
	const char *GetLogTag();

private:
	IScriptVM* Hook_CreateVMPost(ScriptLanguage_t language);
	void Hook_DestroyVM(IScriptVM *pVM);
	void Hook_GameServerSteamAPIActivatedPost();
	void Hook_GameServerSteamAPIShutdown();
	HSCRIPT m_Scope;
	bool SendHTTPRequest(EHTTPMethod method, const char *pszURL, const char* json);

	std::string baseURL;

public:
	void UploadResultValue(char* json);
	void SetResultValue(const char* key, const char* value);

	DECLARE_MY_SCRIPTDESC();
};

extern D2MPPlugin g_D2MPPlugin;

PLUGIN_GLOBALVARS();