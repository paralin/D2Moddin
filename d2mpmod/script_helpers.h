#pragma once
#define DECLARE_MY_SCRIPTDESC()													ALLOW_SCRIPT_ACCESS(); virtual ScriptClassDesc_t *GetScriptDesc()

#define BEGIN_MY_SCRIPTDESC( className, baseClass, description )					_IMPLEMENT_MY_SCRIPTDESC_ACCESSOR( className ); BEGIN_SCRIPTDESC( className, baseClass, description )
#define BEGIN_MY_SCRIPTDESC_ROOT( className, description )							_IMPLEMENT_MY_SCRIPTDESC_ACCESSOR( className ); BEGIN_SCRIPTDESC_ROOT( className, description )
#define BEGIN_MY_SCRIPTDESC_NAMED( className, baseClass, scriptName, description )	_IMPLEMENT_MY_SCRIPTDESC_ACCESSOR( className ); BEGIN_SCRIPTDESC_NAMED( className, baseClass, scriptName, description )
#define BEGIN_MY_SCRIPTDESC_ROOT_NAMED( className, scriptName, description )		_IMPLEMENT_MY_SCRIPTDESC_ACCESSOR( className ); BEGIN_SCRIPTDESC_ROOT_NAMED( className, scriptName, description )

#define _IMPLEMENT_MY_SCRIPTDESC_ACCESSOR( className )					template <> ScriptClassDesc_t * GetScriptDesc<className>( className * ); ScriptClassDesc_t *className::GetScriptDesc()  { return ::GetScriptDesc( this ); }		