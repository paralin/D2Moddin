#pragma once
#include <ISmmPlugin.h>
#include <curlpp/cURLpp.hpp>
#include <curlpp/Easy.hpp>
#include <curlpp/Options.hpp>


#if defined WIN32 && !defined snprintf
#define snprintf _snprintf
#endif

#define LOG(msg) META_LOG(&g_D2MPPlugin, msg)

class D2MPPlugin : public ISmmPlugin
{
public:
	bool Load(PluginId id, ISmmAPI *ismm, char *error, size_t maxlen, bool late);
	bool Unload(char *error, size_t maxlen);
	bool Pause(char *error, size_t maxlen);
	bool Unpause(char *error, size_t maxlen);
	void AllPluginsLoaded();

	bool Hook_GameInit();
public:
	const char *GetAuthor();
	const char *GetName();
	const char *GetDescription();
	const char *GetURL();
	const char *GetLicense();
	const char *GetVersion();
	const char *GetDate();
	const char *GetLogTag();
};

void Hook_ServerActivate();

extern D2MPPlugin g_D2MPPlugin;

PLUGIN_GLOBALVARS();