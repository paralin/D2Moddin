#include <stdio.h>
#include "d2mpmod.h"


SH_DECL_HOOK0_void(IServerGameDLL, ServerActivate, SH_NOATTRIB, 0);
SH_DECL_HOOK0(IServerGameDLL, GameInit, SH_NOATTRIB, 0, bool);

D2MPPlugin g_D2MPPlugin;
IServerGameDLL *server = NULL;

PLUGIN_EXPOSE(D2MPPlugin, g_D2MPPlugin);
bool D2MPPlugin::Load(PluginId id, ISmmAPI *ismm, char *error, size_t maxlen, bool late)
{
	PLUGIN_SAVEVARS();

	GET_V_IFACE_ANY(GetServerFactory, server, IServerGameDLL, INTERFACEVERSION_SERVERGAMEDLL);
	SH_ADD_HOOK_STATICFUNC(IServerGameDLL, ServerActivate, server, Hook_ServerActivate, true);

	SH_ADD_HOOK(IServerGameDLL, GameInit, server, SH_MEMBER(this, &D2MPPlugin::Hook_GameInit), false);

	return true;
}

bool D2MPPlugin::Unload(char *error, size_t maxlen)
{
	SH_REMOVE_HOOK_STATICFUNC(IServerGameDLL, ServerActivate, server, Hook_ServerActivate, true);

	return true;
}

void Hook_ServerActivate()
{
	META_LOG(g_PLAPI, "ServerActivate() called");
}

void D2MPPlugin::AllPluginsLoaded()
{
	/* This is where we'd do stuff that relies on the mod or other plugins
	* being initialized (for example, cvars added and events registered).
	*/
}

bool D2MPPlugin::Pause(char *error, size_t maxlen)
{
	return true;
}

bool D2MPPlugin::Unpause(char *error, size_t maxlen)
{
	return true;
}

const char *D2MPPlugin::GetLicense()
{
	return "Private";
}

const char *D2MPPlugin::GetVersion()
{
	return "1.0.0.0";
}

const char *D2MPPlugin::GetDate()
{
	return __DATE__;
}

const char *D2MPPlugin::GetLogTag()
{
	return "D2MP";
}

const char *D2MPPlugin::GetAuthor()
{
	return "Quantum";
}

const char *D2MPPlugin::GetDescription()
{
	return "Plugin to manage a D2Moddin server instance.";
}

const char *D2MPPlugin::GetName()
{
	return "D2Moddin";
}

const char *D2MPPlugin::GetURL()
{
	return "http://d2modd.in/";
}

bool D2MPPlugin::Hook_GameInit()
{
	static ConVarRef rconPassword("rcon_password");
	curlpp::options::Url myUrl(std::string("http://10.0.1.2:3000/servapi/init/") + std::string(rconPassword.GetString()));
	curlpp::Cleanup myCleanup;
	curlpp::Easy myRequest;
	myRequest.setOpt(myUrl);
	myRequest.perform();

	return true;
}