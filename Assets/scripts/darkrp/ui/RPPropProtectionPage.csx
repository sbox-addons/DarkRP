// DarkRP for s&box - the "Prop Protection" page in Sandbox's spawn menu (Utilities > Server > Prop Protection).
// Admins change the settings and the blocked models here; every player picks their prop buddies.

/// <summary>The settings as a Sandbox property sheet (reads what the host sent, changes go to the host).</summary>
public sealed class RPPPSheet
{
	static RPPPSettings S() => RPPropProtection.S();
	static void Set( string key, bool value ) => RPPropProtectionPage.Send( "set", key, value ? "true" : "false" );
	static void Set( string key, float value ) => RPPropProtectionPage.Send( "set", key, RPUtil.FloatStr( value ) );
	static void Set( string key, int value ) => RPPropProtectionPage.Send( "set", key, value.ToString() );

	[Property, Title( "Prop protection" ), Group( "Protection" ), Order( 0 ), Description( "Only a prop's owner, their buddies and admins can touch it. Off: Sandbox's own rules." )]
	public bool Enabled { get => S().Enabled; set => Set( "enabled", value ); }

	[Property, Title( "Physgun" ), Group( "Protection" ), Order( 1 ), Description( "Others can't pick up your props with the physgun." )]
	public bool Physgun { get => S().Physgun; set => Set( "physgun", value ); }

	[Property, Title( "Tool gun" ), Group( "Protection" ), Order( 2 ), Description( "Others can't use the tool gun on your props." )]
	public bool Toolgun { get => S().Toolgun; set => Set( "toolgun", value ); }

	[Property, Title( "Pick up (use)" ), Group( "Protection" ), Order( 3 ), Description( "Others can't carry your props with the use key." )]
	public bool Pickup { get => S().Pickup; set => Set( "pickup", value ); }

	[Property, Title( "Prop damage" ), Group( "Protection" ), Order( 4 ), Description( "Others can't break your props (DarkRP entities like printers can still be destroyed)." )]
	public bool PropDamage { get => S().PropDamage; set => Set( "propDamage", value ); }

	[Property, Title( "Anti prop-kill" ), Group( "Protection" ), Order( 5 ), Description( "Props (thrown, pushed or dropped) can't hurt players." )]
	public bool AntiPropKill { get => S().AntiPropKill; set => Set( "antiPropKill", value ); }

	[Property, Title( "Protect map props" ), Group( "Protection" ), Order( 6 ), Description( "Only admins can move, tool or remove the map's own props." )]
	public bool WorldProps { get => S().WorldProps; set => Set( "worldProps", value ); }

	[Property, Title( "Admins ignore entity rules" ), Group( "Protection" ), Order( 7 ), Description( "Admins may physgun, tool and pick up every DarkRP entity, whatever its rules below say." )]
	public bool AdminsTouchEntities { get => S().AdminsTouchEntities; set => Set( "adminsTouchEntities", value ); }

	[Property, Title( "Cloud props for everyone" ), Group( "Spawning" ), Order( 10 ), Description( "Players may spawn props from sbox.game (and dupes that need them). Admins always can." )]
	public bool CloudProps { get => S().CloudProps; set => Set( "cloudProps", value ); }

	[Property, Title( "Cloud entities for everyone" ), Group( "Spawning" ), Order( 11 ), Description( "Players may spawn entities from sbox.game. Admins always can." )]
	public bool CloudEntities { get => S().CloudEntities; set => Set( "cloudEntities", value ); }

	[Property, Title( "Mounted games for everyone" ), Group( "Spawning" ), Order( 12 ), Description( "Players may spawn content from mounted games (others may not own them). Admins always can." )]
	public bool MountedContent { get => S().MountedContent; set => Set( "mountedContent", value ); }

	[Property, Title( "Spawn delay" ), Group( "Spawning" ), Order( 13 ), Description( "Seconds between two spawns of a player (anti spam). Admins have none." )]
	[Range( 0, 5 ), Step( 0.1f )]
	public float SpawnDelay { get => S().SpawnDelay; set => Set( "spawnDelay", value ); }

	[Property, Title( "Max prop size" ), Group( "Spawning" ), Order( 14 ), Description( "Longest side of a prop in units, 0 = no limit. Admins have none. Prop counts are in Server > Limits." )]
	[Range( 0, 2000 ), Step( 50 )]
	public int MaxPropSize { get => S().MaxPropSize; set => Set( "maxPropSize", value ); }

	[Property, Title( "Blocked models for admins too" ), Group( "Spawning" ), Order( 15 ), Description( "Admins can't spawn blocked models either." )]
	public bool BlockedModelsForAdmins { get => S().BlockedModelsForAdmins; set => Set( "blockedModelsForAdmins", value ); }

	[Property, Title( "Remove props after leaving" ), Group( "Cleanup" ), Order( 20 ), Description( "Seconds after a player leaves before their props are removed (they get them back if they return). -1 = keep them." )]
	[Range( -1, 1800 ), Step( 30 )]
	public int CleanupDelay { get => S().CleanupDelay; set => Set( "cleanupDelay", value ); }

	[Property, Title( "Log spawns" ), Group( "Cleanup" ), Order( 21 ), Description( "Write every spawn to the DarkRP log." )]
	public bool LogSpawns { get => S().LogSpawns; set => Set( "logSpawns", value ); }

	[Button( "Remove props of players who left", "delete_sweep" ), Group( "Cleanup" ), Order( 22 )]
	public void CleanupDisconnected() => RPPropProtectionPage.Send( "cleanup", "disconnected", "" );
}

[Icon( "shield" ), Title( "Prop Protection" ), Group( "#spawnmenu.utility.group.server" ), Order( 5 )]
public sealed class RPPropProtectionPage : UtilityPage
{
	const string Css = @"
.pp-row { flex-direction: row; align-items: center; gap: 6px; margin: 4px 0 8px; }
.pp-row .pp-entry { flex-grow: 1; flex-shrink: 1; width: 220px; }
.pp-list { flex-direction: column; }
.pp-list .icon { font-family: Material Icons; font-size: 18px; }
.pp-perm { opacity: 0.28; }
.pp-perm.on { opacity: 1; color: #74e08f; }
.pp-rules { flex-direction: column; gap: 2px; margin-bottom: 6px; }
";

	readonly Dictionary<Panel, string> _clicks = new Dictionary<Panel, string>();
	bool _built;
	Panel _blocked, _buddies, _rules, _ents;
	RPTextEntry _entry;
	string _blockedKey, _buddyKey, _rulesKey, _entsKey;

	public override bool IsPageVisible() => !DarkRP.ModuleDisabled( "propprotection" );

	public static void Send( string action, string a, string b )
	{
		var w = RPWorld.Current();
		if ( w != null ) w.PPAction( action, a ?? "", b ?? "" );
	}

	static bool Admin() => RP.IsAdmin( Connection.Local );

	public override void Tick()
	{
		base.Tick();
		if ( !_built ) Build();
		if ( _entry != null && _entry.TakeSubmit() ) Run( "block:entry" );
		Refresh();
	}

	void Build()
	{
		_built = true;
		AddClass( "page" );
		StyleSheet.Parse( Css );
		if ( Admin() )
		{
			var sheet = new UISheet();
			AddChild( sheet );
			sheet.PropertyFilter = p => UISheet.PropertiesAndButtons( p );
			sheet.Target = new RPPPSheet();
			sheet.Rebuild();

			RPUi.Text( this, "Blocked models", "section-header" );
			RPUi.Text( this, "Nobody but admins can spawn these (dupes with them are removed too). Use * as a wildcard, e.g. models/sbox/warehouse/*.", "note" );
			var row = RPUi.Div( this, "pp-row" );
			_entry = new RPTextEntry();
			_entry.AddClass( "menu-control" );
			_entry.AddClass( "pp-entry" );
			_entry.Placeholder = "models/... .vmdl";
			row.AddChild( _entry );
			Btn( row, "Block", "block", "block:entry" );
			Btn( row, "Block what I look at", "ads_click", "block:aim" );
			_blocked = RPUi.Div( this, "pp-list" );

			RPUi.Text( this, "DarkRP entities", "section-header" );
			RPUi.Text( this, "What owners (and their buddies) may do to DarkRP's entities, like FPP's blocked entities in DarkRP: physgun, tool gun, pick up. Green is allowed; click to change. Addons can allow more with the CanTouchEntity hook.", "note" );
			_ents = RPUi.Div( this, "pp-list" );
		}
		else
		{
			RPUi.Text( this, "Prop protection", "section-header" );
			_rules = RPUi.Div( this, "pp-rules" );
		}
		RPUi.Text( this, "Your prop buddies", "section-header" );
		RPUi.Text( this, "Buddies can move, tool, pick up and damage your props. Admins always can.", "note" );
		_buddies = RPUi.Div( this, "pp-list" );
	}

	void Btn( Panel parent, string text, string icon, string action )
	{
		var b = new UIButton();
		b.Text = text;
		b.Icon = icon;
		parent.AddChild( b );
		_clicks[b] = action;
	}

	Panel Entry( Panel parent, string name, string detail, string icon, string action, long avatar )
	{
		var e = RPUi.Div( parent, "list-entry" );
		if ( avatar != 0 )
		{
			var img = new Image();
			img.AddClass( "list-entry-avatar" );
			img.SetTexture( "avatar:" + avatar );
			e.AddChild( img );
		}
		var info = RPUi.Div( e, "list-entry-info" );
		RPUi.Text( info, name, "list-entry-name" );
		if ( !string.IsNullOrEmpty( detail ) ) RPUi.Text( info, detail, "list-entry-detail" );
		if ( !string.IsNullOrEmpty( action ) )
		{
			var act = RPUi.Div( e, "list-entry-action pressable" );
			RPUi.Icon( act, icon, "" );
			_clicks[act] = action;
		}
		return e;
	}

	void Refresh()
	{
		var s = RPPropProtection.S();
		var me = RPPropProtection.SteamOf( Connection.Local );

		if ( _blocked != null )
		{
			var key = string.Join( "|", s.BlockedModels );
			if ( key != _blockedKey )
			{
				_blockedKey = key;
				ClearClicks( "unblock:" );
				_blocked.DeleteChildren( true );
				if ( s.BlockedModels.Count == 0 ) RPUi.Text( _blocked, "No models are blocked.", "note" );
				foreach ( var m in s.BlockedModels )
				{
					var slash = m.LastIndexOf( '/' );
					var name = slash >= 0 ? m.Substring( slash + 1 ) : m;
					var folder = slash > 0 ? m.Substring( 0, slash ) : "";
					Entry( _blocked, name == "*" || name == "" ? m : name, (m.Contains( "*" ) ? "wildcard · " : "") + folder, "close", "unblock:" + m, 0 );
				}
			}
		}

		if ( _ents != null )
		{
			var reg = DarkRP.Registry();
			var types = reg != null ? reg.EntityTypes.Values.OrderBy( t => t.Name ).ToList() : new List<RPEntityType>();
			var key = string.Join( "|", types.Select( t => t.Id + "=" + s.EntityPerms( t.Id ) ) );
			if ( key != _entsKey )
			{
				_entsKey = key;
				ClearClicks( "perm:" );
				_ents.DeleteChildren( true );
				foreach ( var t in types )
				{
					var perms = s.EntityPerms( t.Id );
					var e = RPUi.Div( _ents, "list-entry" );
					var info = RPUi.Div( e, "list-entry-info" );
					RPUi.Text( info, t.Name, "list-entry-name" );
					RPUi.Text( info, t.Id, "list-entry-detail" );
					PermButton( e, t.Id, perms, "physgun", "open_with", "Physgun" );
					PermButton( e, t.Id, perms, "toolgun", "build", "Tool gun" );
					PermButton( e, t.Id, perms, "pickup", "pan_tool", "Pick up" );
				}
			}
		}

		if ( _rules != null )
		{
			var key = s.ToJson( false );
			if ( key != _rulesKey )
			{
				_rulesKey = key;
				_rules.DeleteChildren( true );
				if ( !s.Enabled ) RPUi.Text( _rules, "Prop protection is off on this server.", "note" );
				else
				{
					RPUi.Text( _rules, "Only you, your buddies and admins can touch your props" + (s.WorldProps ? "; the map's props are for admins." : "."), "note" );
					if ( s.AntiPropKill ) RPUi.Text( _rules, "Props can't hurt players.", "note" );
					RPUi.Text( _rules, "Props from sbox.game: " + (s.CloudProps ? "allowed" : "admins only") + ". Entities from sbox.game: " + (s.CloudEntities ? "allowed" : "admins only") + ". Mounted games: " + (s.MountedContent ? "allowed" : "admins only") + ".", "note" );
					if ( s.BlockedModels.Count > 0 ) RPUi.Text( _rules, s.BlockedModels.Count + " models are blocked.", "note" );
					if ( s.CleanupDelay >= 0 ) RPUi.Text( _rules, "Your props are removed " + RPUtil.Duration( s.CleanupDelay ) + " after you leave (you get them back if you return).", "note" );
				}
			}
		}

		if ( _buddies != null )
		{
			var others = Connection.All.Where( c => c != Connection.Local ).ToList();
			var key = string.Join( "|", others.Select( c => RPPropProtection.SteamOf( c ) + (RPPropProtection.IsBuddy( me, RPPropProtection.SteamOf( c ) ) ? "+" : "-") + RP.Name( c ) ) );
			if ( key != _buddyKey )
			{
				_buddyKey = key;
				ClearClicks( "buddy:" );
				_buddies.DeleteChildren( true );
				if ( others.Count == 0 ) RPUi.Text( _buddies, "Nobody else is here.", "note" );
				foreach ( var c in others )
				{
					var id = RPPropProtection.SteamOf( c );
					var buddy = RPPropProtection.IsBuddy( me, id );
					Entry( _buddies, RP.Name( c ), buddy ? "Can touch your props" : "Can't touch your props", buddy ? "person_remove" : "person_add", "buddy:" + id, id );
				}
			}
		}
	}

	void PermButton( Panel entry, string typeId, string perms, string what, string icon, string tip )
	{
		var on = RPPPSettings.HasPerm( perms, what );
		var b = RPUi.Div( entry, "list-entry-action pressable pp-perm" );
		b.SetClass( "on", on );
		b.Tooltip = tip + (on ? ": allowed" : ": not allowed");
		RPUi.Icon( b, icon, "" );
		var list = ("," + perms + ",").Replace( "," + what + ",", "," ).Trim( ',' );
		var next = on ? list : (list.Length > 0 ? list + "," + what : what);
		_clicks[b] = "perm:" + typeId + "=" + next;
	}

	void ClearClicks( string prefix )
	{
		foreach ( var p in _clicks.Keys.ToList() ) if ( _clicks[p].StartsWith( prefix ) ) _clicks.Remove( p );
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );
		var p = e.Target;
		while ( p != null && p != this )
		{
			if ( _clicks.ContainsKey( p ) )
			{
				e.StopPropagation();
				Run( _clicks[p] );
				return;
			}
			p = p.Parent;
		}
	}

	void Run( string action )
	{
		var i = action.IndexOf( ':' );
		var kind = i > 0 ? action.Substring( 0, i ) : action;
		var arg = i > 0 ? action.Substring( i + 1 ) : "";
		if ( kind == "buddy" ) Send( "buddy", arg, "" );
		else if ( kind == "perm" )
		{
			var eq = arg.IndexOf( '=' );
			if ( eq > 0 ) Send( "entity", arg.Substring( 0, eq ), arg.Substring( eq + 1 ) );
		}
		else if ( kind == "unblock" ) Send( "unblock", arg, "" );
		else if ( kind == "block" && arg == "aim" ) Send( "block", "", "" );
		else if ( kind == "block" && _entry != null )
		{
			var text = (_entry.Text ?? "").Trim();
			if ( text.Length == 0 ) return;
			Send( "block", text, "" );
			_entry.Text = "";
		}
	}
}
