// DarkRP for s&box - the pocket, admin tools (spawn menu restrictions, config changes) and the generated README.
// Ported from DarkRP's pocket SWEP and FAdmin/config handling (MIT, Falco Peijnenburg).

public static class RPPocket
{
	static void Sync( RPPlayer rp )
	{
		rp.PocketList = string.Join( "\u0001", rp.Pocket.Select( p => p.Name ) );
	}

	/// <summary>Host: puts a DarkRP entity in the player's pocket.</summary>
	public static void Pickup( Connection c, GameObject target )
	{
		var rp = RP.Of( c );
		var e = target != null ? target.Components.Get<RPEntity>( FindMode.EverythingInSelfAndAncestors ) : null;
		if ( rp == null || e == null ) { DarkRP.Notify( c, 1, 3, "You can't put that in your pocket." ); return; }
		var t = DarkRP.Registry().EntityType( e.TypeId );
		if ( t == null || !t.Pocketable ) { DarkRP.Notify( c, 1, 3, "That doesn't fit in your pocket." ); return; }
		if ( rp.Pocket.Count >= DarkRP.Config().Int( "pocketitems" ) ) { DarkRP.Notify( c, 1, 3, "Your pocket is full." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canPocket", Player = c, Entity = e.GameObject, Text = e.TypeId } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 3, string.IsNullOrEmpty( h.Reason ) ? "You can't pocket that." : h.Reason ); return; }
		var mr = e.Components.Get<ModelRenderer>();
		var item = new RPPocketItem
		{
			Name = string.IsNullOrEmpty( e.Label ) ? t.Name : e.Label.Split( '\n' )[0],
			TypeId = e.TypeId,
			Model = mr != null && mr.Model != null ? mr.Model.ResourcePath : "",
			State = e.Amount + "\u0001" + e.Price + "\u0001" + e.Label + "\u0001" + e.Data + "\u0001" + e.DefCmd + "\u0001" + e.OwnerId + "\u0001" + e.OwnerName,
		};
		rp.Pocket.Add( item );
		foreach ( var other in RP.All() ) other.OwnedEntities.Remove( e.GameObject );
		e.GameObject.Destroy();
		Sync( rp );
		DarkRP.RunHook( new RPHook { Name = "onPocketItemAdded", Player = c, Text = item.TypeId } );
	}

	public static void Drop( Connection c, int index, Vector3 pos )
	{
		var rp = RP.Of( c );
		if ( rp == null || index < 0 || index >= rp.Pocket.Count ) return;
		var item = rp.Pocket[index];
		rp.Pocket.RemoveAt( index );
		Sync( rp );
		var p = item.State.Split( '\u0001' );
		var owner = c;
		foreach ( var other in RP.All() ) if ( p.Length > 5 && other.SteamIdText == p[5] ) owner = RP.ConnOf( other );
		var go = RPEntities.Spawn( item.TypeId, pos, Rotation.Identity, owner );
		if ( go == null ) return;
		var e = go.Components.Get<RPEntity>();
		if ( p.Length > 0 ) e.Amount = (long)RPUtil.ParseFloat( p[0], 0 );
		if ( p.Length > 1 ) e.Price = (int)RPUtil.ParseFloat( p[1], 0 );
		if ( p.Length > 2 ) e.Label = p[2];
		if ( p.Length > 3 ) e.Data = p[3];
		if ( p.Length > 4 ) e.DefCmd = p[4];
		if ( p.Length > 5 ) e.OwnerId = p[5];
		if ( p.Length > 6 ) e.OwnerName = p[6];
		if ( !string.IsNullOrEmpty( item.Model ) )
		{
			var mr = go.Components.Get<ModelRenderer>();
			var m = Model.Load( item.Model );
			if ( mr != null && m != null && !m.IsError && mr.Model != m ) mr.Model = m;
		}
		var orp = RP.Of( owner );
		if ( orp != null ) orp.OwnedEntities.Add( go );
	}

	public static void DropAll( Connection c, Vector3 pos )
	{
		var rp = RP.Of( c );
		if ( rp == null ) return;
		for ( int i = rp.Pocket.Count - 1; i >= 0; i-- ) Drop( c, i, pos + Vector3.Up * (10 + i * 8) );
	}

	public static void Action( Connection c, string action, int index )
	{
		if ( action == "drop" ) Drop( c, index, RPEntities.InFront( c, 40 ) );
	}
}

public static class RPAdmin
{
	/// <summary>DarkRP permission for Sandbox's permission list.</summary>
	[Permission, Title( "DarkRP Admin" ), Description( "DarkRP admin commands (/setmoney, /setjob, /arrest...), the admin tab, spawning restricted things." )]
	public const string Permission = "darkrp.admin";

	/// <summary>Spawn menu: only admins spawn weapons, entities and NPCs (buy them from the M menu instead).</summary>
	public static void OnSpawnMenu( Global.ISpawnEvents.SpawnData e )
	{
		if ( e.Cancelled || e.Player == null ) return;
		var cfg = DarkRP.Config();
		if ( cfg == null || RP.IsAdmin( e.Player ) ) return;
		var prefab = e.Spawner != null ? e.Spawner.Prefab : null;
		if ( cfg.Bool( "restrictspawnmenu" ) )
		{
			if ( prefab != null && prefab.GetComponent<BaseCombatWeapon>( true ) != null ) { e.Cancelled = true; DarkRP.Notify( e.Player, 1, 4, "Buy weapons from the menu (M)." ); return; }
			if ( e.Spawner is EntitySpawner ) { e.Cancelled = true; DarkRP.Notify( e.Player, 1, 4, "Only admins can spawn entities." ); return; }
		}
		if ( !cfg.Bool( "propspawning" ) ) { e.Cancelled = true; DarkRP.Notify( e.Player, 1, 4, "Prop spawning is disabled." ); return; }
		var rp = RP.Of( e.Player );
		if ( rp != null && rp.Arrested ) { e.Cancelled = true; DarkRP.Notify( e.Player, 1, 4, "You can't spawn things in jail." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "PlayerSpawnProp", Player = e.Player, Text = e.Spawner != null ? e.Spawner.DisplayName : "" } );
		if ( !h.Allowed ) { e.Cancelled = true; if ( !string.IsNullOrEmpty( h.Reason ) ) DarkRP.Notify( e.Player, 1, 4, h.Reason ); }
	}

	public static void SetConfig( Connection c, string key, string value )
	{
		var cfg = DarkRP.Config();
		if ( string.IsNullOrEmpty( key ) ) { DarkRP.Notify( c, 1, 4, "Usage: /rpconfig <key> <value>. Keys are listed in data/darkrp/README.txt and the admin tab." ); return; }
		RPSetting found = null;
		foreach ( var k in cfg.Order ) if ( string.Equals( k, key, StringComparison.OrdinalIgnoreCase ) ) found = cfg.Settings[k];
		if ( found == null ) { DarkRP.Notify( c, 1, 4, "Unknown setting: " + key ); return; }
		if ( string.IsNullOrEmpty( value ) ) { DarkRP.Notify( c, 0, 6, found.Key + " = " + found.Value + " (" + found.Help + ")" ); return; }
		if ( !cfg.Set( found.Key, value ) ) { DarkRP.Notify( c, 1, 4, "Bad value for " + found.Key + " (" + found.Kind + ")." ); return; }
		RPDataFiles.SaveConfig( cfg );
		DarkRP.Registry().Version++;
		DarkRP.Notify( c, 0, 4, found.Key + " is now " + found.Value + ". Saved to darkrp/config.json." );
		RPLog.Write( RP.Name( c ) + " set " + found.Key + " to " + found.Value );
		DarkRP.RunHook( new RPHook { Name = "configChanged", Player = c, Text = found.Key, Reason = found.Value } );
	}

	public static void Action( Connection c, string action, string a, string b )
	{
		if ( !RP.IsAdmin( c ) ) return;
		if ( action == "config" ) SetConfig( c, a, b );
	}
}

/// <summary>Builds data/darkrp/README.txt: what each file does and every setting with its current value.</summary>
public static class RPReadme
{
	public static string Build( RPRegistry r )
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine( "DarkRP for s&box " + DarkRP.Version + " - server configuration" );
		sb.AppendLine( "=================================================" );
		sb.AppendLine( "This folder is DarkRP's \"darkrpmodification\": everything here survives addon updates." );
		sb.AppendLine( "Restart the map after editing (settings can also be changed live with /rpconfig or the Admin tab)." );
		sb.AppendLine();
		sb.AppendLine( "config.json      all settings (listed below)" );
		sb.AppendLine( "jobs.json        your jobs (add new ones, replace or tweak defaults by command, \"disable\" to remove)" );
		sb.AppendLine( "shipments.json   shipments and single weapons (by name)" );
		sb.AppendLine( "entities.json    buyable entities (by cmd)" );
		sb.AppendLine( "ammo.json, food.json, categories.json, groups.json (door groups, agendas, group chats), laws.json" );
		sb.AppendLine( "disabled_defaults.json   DarkRP's disabled_defaults: every default job, shipment, entity, ammo, food, door" );
		sb.AppendLine( "                 group, agenda, group chat, demote group, hitman and law, plus the modules - set one to true" );
		sb.AppendLine( "                 to remove that default (your own content with the same id is still added)" );
		sb.AppendLine( "propprotection.json  prop protection settings and blocked models (also in the spawn menu: Utilities >" );
		sb.AppendLine( "                 Server > Prop Protection); pp_buddies.json holds the players' buddy lists" );
		sb.AppendLine( "reference/       everything DarkRP currently has, in the same format - copy entries from here" );
		sb.AppendLine( "players/         saved wallets and RP names (one file per steam id)" );
		sb.AppendLine( "maps/            jail positions, job spawns and door settings per map" );
		sb.AppendLine( "logs/            the DarkRP log" );
		sb.AppendLine();
		sb.AppendLine( "Jobs: \"outfit\" is a list of s&box clothing (.clothing paths, optional \"@0.5\" tint); leave it out (or null)" );
		sb.AppendLine( "for the player's own avatar. \"model\" is a list of .vmdl player models instead of clothing." );
		sb.AppendLine( "Weapons are ids from reference/weapons.json (DarkRP's and Sandbox's) or a weapon prefab path." );
		sb.AppendLine( "Full guide: the addon's Docs/SERVER_OWNERS.md." );
		sb.AppendLine();
		sb.AppendLine( "SETTINGS" );
		string group = "";
		foreach ( var key in r.Config.Order )
		{
			var s = r.Config.Settings[key];
			if ( s.Group != group ) { group = s.Group; sb.AppendLine(); sb.AppendLine( "[" + group + "]" ); }
			sb.AppendLine( "  " + key.PadRight( 28 ) + " = " + s.Value.PadRight( 10 ) + " (" + s.Kind + ", default " + s.Default + ")  " + s.Help );
		}
		sb.AppendLine();
		sb.AppendLine( "MODULES (disabled_defaults.json \"modules\")" );
		foreach ( var m in RPDisabledDefaults.Modules() ) sb.AppendLine( "  " + m.PadRight( 16 ) + (r.ModuleDisabled( m ) ? "OFF  " : "on   ") + "switching it off removes " + RPDisabledDefaults.ModuleHelp( m ) );
		sb.AppendLine();
		sb.AppendLine( "PROP PROTECTION (propprotection.json; /ppset <setting> <value>)" );
		sb.AppendLine( "  enabled                 only owners, their buddies (/buddy) and admins touch a prop; off = Sandbox's own rules" );
		sb.AppendLine( "  physgun, toolgun, pickup, propDamage   which actions are protected" );
		sb.AppendLine( "  antiPropKill            props can't hurt players" );
		sb.AppendLine( "  worldProps              only admins can move/tool/remove the map's props" );
		sb.AppendLine( "  cloudProps, cloudEntities, mountedContent   players (not only admins) may spawn props/entities from sbox.game" );
		sb.AppendLine( "                          or content from mounted games (all false by default)" );
		sb.AppendLine( "  blockedModels           model paths (or sbox.game idents) nobody but admins can spawn; * is a wildcard" );
		sb.AppendLine( "  blockedModelsForAdmins  admins can't spawn blocked models either" );
		sb.AppendLine( "  maxPropSize             longest side in units a player's prop may have (0 = no limit)" );
		sb.AppendLine( "  spawnDelay              seconds between two spawns of a player" );
		sb.AppendLine( "  cleanupDelay            seconds after leaving before a player's props are removed (-1 = keep)" );
		sb.AppendLine( "  logSpawns               write spawns to the DarkRP log" );
		sb.AppendLine( "  entityPermissions       per DarkRP entity type what owners may do: \"physgun,toolgun,pickup\" (FPP's blocked" );
		sb.AppendLine( "                          entities: by default only pickup; the tip jar may be physgunned and tooled)" );
		sb.AppendLine( "  adminsTouchEntities     admins ignore entityPermissions" );
		sb.AppendLine( "  Prop counts per player are Sandbox's: spawn menu > Utilities > Server > Limits." );
		sb.AppendLine();
		sb.AppendLine( "CHAT COMMANDS" );
		foreach ( var k in r.Commands.Keys.OrderBy( x => x ) )
		{
			var c = r.Commands[k];
			sb.AppendLine( "  /" + k.PadRight( 16 ) + (c.Permission == "admin" ? "[admin] " : "") + c.Description );
		}
		sb.AppendLine( "  /<job command>   become that job (e.g. /cp), /vote<job> for vote jobs" );
		sb.AppendLine( "  /<entity cmd>    buy an entity (e.g. /buymoneyprinter)" );
		return sb.ToString();
	}
}
