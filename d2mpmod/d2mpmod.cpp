#include <stdio.h>
#include "d2mpmod.h"

D2MPPlugin g_D2MPPlugin;

static CSteamGameServerAPIContext steam;

static IServerGameDLL *gamedll = NULL;
static IScriptManager *scriptmgr = NULL;
static IScriptVM *g_pScriptVM;

BEGIN_MY_SCRIPTDESC_ROOT(D2MPPlugin, "D2Moddin script extensions")
DEFINE_SCRIPTFUNC(SetResultValue, "Set a match result value")
END_SCRIPTDESC();

SH_DECL_HOOK0_void(IServerGameDLL, GameServerSteamAPIActivated, SH_NOATTRIB, 0);
SH_DECL_HOOK0_void(IServerGameDLL, GameServerSteamAPIShutdown, SH_NOATTRIB, 0);
SH_DECL_HOOK1(IScriptManager, CreateVM, SH_NOATTRIB, 0, IScriptVM *, ScriptLanguage_t);
SH_DECL_HOOK1_void(IScriptManager, DestroyVM, SH_NOATTRIB, 0, IScriptVM *);

IServerGameDLL *server = NULL;

PLUGIN_EXPOSE(D2MPPlugin, g_D2MPPlugin);
bool D2MPPlugin::Load(PluginId id, ISmmAPI *ismm, char *error, size_t maxlen, bool late)
{
	PLUGIN_SAVEVARS();

	baseURL = "http://10.0.1.2:3000/resultAPI/";

	GET_V_IFACE_CURRENT(GetServerFactory, gamedll, IServerGameDLL, INTERFACEVERSION_SERVERGAMEDLL);
	GET_V_IFACE_CURRENT(GetEngineFactory, scriptmgr, IScriptManager, VSCRIPT_INTERFACE_VERSION);
	GET_V_IFACE_ANY(GetServerFactory, server, IServerGameDLL, INTERFACEVERSION_SERVERGAMEDLL);

	SH_ADD_HOOK(IScriptManager, CreateVM, scriptmgr, SH_MEMBER(this, &D2MPPlugin::Hook_CreateVMPost), true);
	SH_ADD_HOOK(IScriptManager, DestroyVM, scriptmgr, SH_MEMBER(this, &D2MPPlugin::Hook_DestroyVM), true);
	SH_ADD_HOOK(IServerGameDLL, GameServerSteamAPIActivated, gamedll, SH_MEMBER(this, &D2MPPlugin::Hook_GameServerSteamAPIActivatedPost), true);
	SH_ADD_HOOK(IServerGameDLL, GameServerSteamAPIShutdown, gamedll, SH_MEMBER(this, &D2MPPlugin::Hook_GameServerSteamAPIShutdown), false);

	return true;
}

bool D2MPPlugin::Unload(char *error, size_t maxlen)
{
	SH_REMOVE_HOOK(IServerGameDLL, GameServerSteamAPIActivated, gamedll, SH_MEMBER(this, &D2MPPlugin::Hook_GameServerSteamAPIActivatedPost), true);
	SH_REMOVE_HOOK(IServerGameDLL, GameServerSteamAPIShutdown, gamedll, SH_MEMBER(this, &D2MPPlugin::Hook_GameServerSteamAPIShutdown), false);
	SH_REMOVE_HOOK(IScriptManager, CreateVM, scriptmgr, SH_MEMBER(this, &D2MPPlugin::Hook_CreateVMPost), true);
	SH_REMOVE_HOOK(IScriptManager, DestroyVM, scriptmgr, SH_MEMBER(this, &D2MPPlugin::Hook_DestroyVM), true);

	return true;
}

IScriptVM* D2MPPlugin::Hook_CreateVMPost(ScriptLanguage_t language)
{
	g_pScriptVM = META_RESULT_ORIG_RET(IScriptVM *);
	HSCRIPT scope = g_pScriptVM->RegisterInstance(GetScriptDesc(), this);
	g_pScriptVM->SetValue(NULL, "D2MP", scope);
	m_Scope = scope;

	RETURN_META_VALUE(MRES_IGNORED, NULL);
}

void D2MPPlugin::Hook_DestroyVM(IScriptVM *pVM)
{
	if (g_pScriptVM)
	{
		m_Scope = INVALID_HSCRIPT;
		g_pScriptVM = NULL;
	}
}

void D2MPPlugin::Hook_GameServerSteamAPIActivatedPost()
{
	LOG("GameServer SteamAPI activated.")
	steam.Init();
}

void D2MPPlugin::Hook_GameServerSteamAPIShutdown()
{
	LOG("GameServer SteamAPI shutdown.")
	steam.Clear();
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

bool D2MPPlugin::SendHTTPRequest(EHTTPMethod method, const char *pszURL, const char* json)
{
	auto http = steam.SteamHTTP();
	if (!http)
	{
		Msg("[VScriptHTTP] Error: tried to run http method before ISteamHTTP available.\n");
		return false;
	}

	auto hReq = http->CreateHTTPRequest(method, pszURL);

	http->SetHTTPRequestGetOrPostParameter(hReq, "data", json);

	SteamAPICall_t hCall;
	http->SendHTTPRequest(hReq, &hCall);

	return true;
}


void D2MPPlugin::UploadResultValue(char* json)
{
	static ConVarRef rconPassword("rcon_password");
	std::ostringstream str;
	str << baseURL.c_str();
	str << rconPassword.GetString();
	str << "/";
	str << "set";
	SendHTTPRequest(k_EHTTPMethodPOST, str.str().c_str(), json);
}

void D2MPPlugin::SetResultValue(const char* key, const char* value)
{
	std::ostringstream str;
	str << "{\"";
	str << key;
	str << "\": \""; //{"key": 
	str << value;
	str << "\"}"; // add the ending }
	LOG(str.str().c_str());
	UploadResultValue((char*)str.str().c_str());
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