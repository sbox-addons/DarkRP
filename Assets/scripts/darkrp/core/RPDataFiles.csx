// DarkRP for s&box - server-owner config files in the data folder (DarkRP's "darkrpmodification" idea: custom content
// lives outside the addon) and the JSON form of content that the host sends to clients.
using System.Text.Json.Nodes;

public static class RPDataFiles
{
	public const string Dir = "darkrp";

	static string P( string file ) => Dir + "/" + file;

	public static bool Exists( string file ) => FileSystem.Data.FileExists( P( file ) );

	public static string Read( string file )
	{
		try { return FileSystem.Data.FileExists( P( file ) ) ? FileSystem.Data.ReadAllText( P( file ) ) : null; }
		catch ( Exception e ) { Log.Warning( "DarkRP: can't read " + file + ": " + e.Message ); return null; }
	}

	public static void Write( string file, string text )
	{
		try
		{
			var slash = file.LastIndexOf( '/' );
			FileSystem.Data.CreateDirectory( slash > 0 ? P( file.Substring( 0, slash ) ) : Dir );
			FileSystem.Data.WriteAllText( P( file ), text );
		}
		catch ( Exception e ) { Log.Warning( "DarkRP: can't write " + file + ": " + e.Message ); }
	}

	/// <summary>Host: config.json, then each content file. Missing files are created (empty, with a readme).</summary>
	public static void LoadAll( RPRegistry r )
	{
		LoadConfig( r );
		LoadKind( r, "jobs.json", "jobs" );
		LoadKind( r, "shipments.json", "shipments" );
		LoadKind( r, "entities.json", "entities" );
		LoadKind( r, "ammo.json", "ammo" );
		LoadKind( r, "food.json", "food" );
		LoadKind( r, "categories.json", "categories" );
		LoadGroups( r );
		LoadLaws( r );
	}

	// ---- config.json ----

	public static void LoadConfig( RPRegistry r )
	{
		var o = RPJson.Parse( Read( "config.json" ) );
		bool missing = false;
		if ( o != null )
		{
			foreach ( var key in r.Config.Order )
			{
				if ( !RPJson.HasKey( o, key ) ) { missing = true; continue; }
				if ( !r.Config.Set( key, RPJson.Str( o, key, "" ) ) ) r.LoadErrors.Add( "config.json: bad value for " + key );
			}
		}
		if ( o == null || missing ) SaveConfig( r.Config );
	}

	public static void SaveConfig( RPConfig c )
	{
		var w = new RPJsonWriter();
		w.BeginObject();
		w.Str( "_readme", "DarkRP settings. Change a value and restart the map (or use the Admin tab / rp_config). Every setting is explained in darkrp/README.txt." );
		foreach ( var key in c.Order )
		{
			var s = c.Settings[key];
			if ( s.Kind == "bool" ) w.Bool( key, c.Bool( key ) );
			else if ( s.Kind == "int" ) w.Num( key, c.Int( key ) );
			else if ( s.Kind == "float" ) w.Flt( key, c.Float( key ) );
			else w.Str( key, s.Value );
		}
		w.EndObject();
		Write( "config.json", w.ToString() );
	}

	// ---- content files ----

	static void LoadKind( RPRegistry r, string file, string kind )
	{
		var text = Read( file );
		if ( text == null )
		{
			WriteEmpty( file, kind );
			return;
		}
		var o = RPJson.Parse( text );
		if ( o == null ) { r.LoadErrors.Add( file + " is not valid JSON - fix it or delete it to get a fresh one." ); return; }

		var arr = RPJson.Arr( o, kind );
		if ( arr != null )
		{
			foreach ( var node in arr )
			{
				JsonObject item = null;
				try { item = node.AsObject(); } catch { }
				if ( item == null ) continue;
				try { AddFromJson( r, kind, item ); }
				catch ( Exception e ) { r.LoadErrors.Add( file + ": " + e.Message ); }
			}
		}

		var disable = RPJson.StrArray( o, "disable", new string[0] );
		var single = kind == "jobs" ? "job" : kind == "shipments" ? "shipment" : kind == "entities" ? "entity" : kind == "categories" ? "category" : kind;
		foreach ( var id in disable ) r.Disabled.Add( single + ":" + id.ToLowerInvariant() );
	}

	static void AddFromJson( RPRegistry r, string kind, JsonObject o )
	{
		if ( kind == "jobs" ) r.AddJob( RPContentJson.JobFrom( o, r.Job( RPJson.Str( o, "command", "" ) ) ) );
		else if ( kind == "shipments" ) r.AddShipment( RPContentJson.ShipmentFrom( o, r.Shipment( RPJson.Str( o, "name", "" ) ) ) );
		else if ( kind == "entities" ) r.AddEntity( RPContentJson.EntityFrom( o, r.EntityDef( RPJson.Str( o, "cmd", RPJson.Str( o, "name", "" ) ) ) ) );
		else if ( kind == "ammo" ) r.AddAmmo( RPContentJson.AmmoFrom( o ) );
		else if ( kind == "food" ) r.AddFood( RPContentJson.FoodFrom( o ) );
		else if ( kind == "categories" ) r.AddCategory( RPContentJson.CategoryFrom( o ) );
	}

	static void WriteEmpty( string file, string kind )
	{
		var w = new RPJsonWriter();
		w.BeginObject();
		w.Str( "_readme", "Add your own " + kind + " to the list below (same fields as darkrp/reference/" + kind + ".json, which lists everything DarkRP has right now). An entry with the same id as an existing one replaces it; only the fields you write change. Put ids in \"disable\" to remove defaults. Restart the map to apply." );
		w.BeginArray( kind );
		w.EndArray();
		w.StrArray( "disable", new string[0] );
		w.EndObject();
		Write( file, w.ToString() );
	}

	static void LoadGroups( RPRegistry r )
	{
		var o = RPJson.Parse( Read( "groups.json" ) );
		if ( o == null )
		{
			var w = new RPJsonWriter();
			w.BeginObject();
			w.Str( "_readme", "Door groups (doors only some jobs can use), agendas and group chats (/g). Entries here are added to DarkRP's defaults; set \"replaceDefaults\": true to use only these." );
			w.Bool( "replaceDefaults", false );
			w.BeginArray( "doorGroups" ).EndArray();
			w.BeginArray( "agendas" ).EndArray();
			w.BeginArray( "groupChats" ).EndArray();
			w.BeginArray( "demoteGroups" ).EndArray();
			w.EndObject();
			Write( "groups.json", w.ToString() );
			return;
		}
		if ( RPJson.Bool( o, "replaceDefaults", false ) )
		{
			r.DoorGroups.Clear(); r.Agendas.Clear(); r.GroupChats.Clear(); r.DemoteGroups.Clear();
		}
		var dg = RPJson.Arr( o, "doorGroups" );
		if ( dg != null ) foreach ( var n in dg ) { var x = n.AsObject(); r.DoorGroups.Add( new RPDoorGroup { Name = RPJson.Str( x, "name", "" ), Jobs = RPJson.StrArray( x, "jobs", new string[0] ) } ); }
		var ag = RPJson.Arr( o, "agendas" );
		if ( ag != null ) foreach ( var n in ag ) { var x = n.AsObject(); r.Agendas.Add( new RPAgendaDef { Title = RPJson.Str( x, "title", "" ), Managers = RPJson.StrArray( x, "managers", new string[0] ), Listeners = RPJson.StrArray( x, "listeners", new string[0] ) } ); }
		var gc = RPJson.Arr( o, "groupChats" );
		if ( gc != null ) foreach ( var n in gc ) { var x = n.AsObject(); r.GroupChats.Add( new RPGroupChat { Jobs = RPJson.StrArray( x, "jobs", new string[0] ) } ); }
		var dm = RPJson.Arr( o, "demoteGroups" );
		if ( dm != null ) foreach ( var n in dm ) { var x = n.AsObject(); r.DemoteGroups.Add( new RPDemoteGroup { Name = RPJson.Str( x, "name", "" ), Jobs = RPJson.StrArray( x, "jobs", new string[0] ) } ); }
	}

	static void LoadLaws( RPRegistry r )
	{
		var o = RPJson.Parse( Read( "laws.json" ) );
		if ( o == null )
		{
			var w = new RPJsonWriter();
			w.BeginObject();
			w.Str( "_readme", "The laws the city starts with (the mayor can change them with /addlaw and /removelaw)." );
			w.StrArray( "laws", r.DefaultLaws.ToArray() );
			w.EndObject();
			Write( "laws.json", w.ToString() );
			return;
		}
		var laws = RPJson.StrArray( o, "laws", null );
		if ( laws != null ) { r.DefaultLaws.Clear(); r.DefaultLaws.AddRange( laws ); }
	}

	/// <summary>darkrp/reference/*.json (everything loaded, in the editable format) and darkrp/README.txt.</summary>
	public static void WriteReference( RPRegistry r )
	{
		Write( "reference/jobs.json", RPContentJson.List( "jobs", r, true ) );
		Write( "reference/shipments.json", RPContentJson.List( "shipments", r, true ) );
		Write( "reference/entities.json", RPContentJson.List( "entities", r, true ) );
		Write( "reference/ammo.json", RPContentJson.List( "ammo", r, true ) );
		Write( "reference/food.json", RPContentJson.List( "food", r, true ) );
		Write( "reference/categories.json", RPContentJson.List( "categories", r, true ) );
		Write( "reference/weapons.json", RPContentJson.List( "weapons", r, true ) );
		Write( "README.txt", RPReadme.Build( r ) );
	}

	// ---- per-player and per-map data ----

	public static JsonObject ReadPlayer( long steamId ) => RPJson.Parse( Read( "players/" + steamId + ".json" ) );
	public static void WritePlayer( long steamId, string json ) => Write( "players/" + steamId + ".json", json );
	public static JsonObject ReadMap( string map ) => RPJson.Parse( Read( "maps/" + RPUtil.Safe( map ) + ".json" ) );
	public static void WriteMap( string map, string json ) => Write( "maps/" + RPUtil.Safe( map ) + ".json", json );
}

/// <summary>Content ⇄ JSON (data files, references and the host → client content sync).</summary>
public static class RPContentJson
{
	public static void Job( RPJsonWriter w, RPJob j )
	{
		w.BeginObject();
		w.Str( "name", j.Name ).Str( "command", j.Command ).Str( "color", j.Color ).Str( "description", j.Description );
		w.StrArray( "model", j.Models ).StrArray( "outfit", j.Outfit ).StrArray( "weapons", j.Weapons ).StrArray( "ammo", j.Ammo );
		w.Num( "salary", j.Salary ).Flt( "max", j.Max ).Str( "category", j.Category ).Num( "sortOrder", j.SortOrder );
		w.Bool( "vote", j.Vote ).Bool( "hasLicense", j.HasLicense ).Bool( "candemote", j.CanDemote ).Num( "admin", j.Admin );
		w.StrArray( "NeedToChangeFrom", j.NeedToChangeFrom ).Flt( "health", j.StartHealth ).Flt( "armor", j.StartArmor );
		w.Bool( "police", j.Police ).Bool( "chief", j.Chief ).Bool( "mayor", j.Mayor ).Bool( "medic", j.Medic ).Bool( "hobo", j.Hobo ).Bool( "cook", j.Cook ).Bool( "hitman", j.Hitman );
		w.Bool( "hidden", j.Hidden ).Num( "index", j.Index ).Str( "source", j.Source );
		w.EndObject();
	}

	/// <summary>A job from JSON; fields missing in <paramref name="o"/> keep the values of <paramref name="baseJob"/>.</summary>
	public static RPJob JobFrom( JsonObject o, RPJob baseJob )
	{
		var b = baseJob ?? new RPJob();
		var j = new RPJob
		{
			Name = RPJson.Str( o, "name", b.Name ),
			Command = RPJson.Str( o, "command", b.Command ),
			Color = RPJson.Str( o, "color", b.Color ),
			Description = RPJson.Str( o, "description", b.Description ),
			Models = RPJson.StrArray( o, "model", b.Models ),
			Outfit = RPJson.HasKey( o, "outfit" ) ? RPJson.StrArray( o, "outfit", null ) : (o.ContainsKey( "outfit" ) ? null : b.Outfit),
			Weapons = RPJson.StrArray( o, "weapons", b.Weapons ),
			Ammo = RPJson.StrArray( o, "ammo", b.Ammo ),
			Salary = RPJson.Int( o, "salary", b.Salary ),
			Max = RPJson.Flt( o, "max", b.Max ),
			Category = RPJson.Str( o, "category", b.Category ),
			SortOrder = RPJson.Int( o, "sortOrder", b.SortOrder ),
			Vote = RPJson.Bool( o, "vote", b.Vote ),
			HasLicense = RPJson.Bool( o, "hasLicense", b.HasLicense ),
			CanDemote = RPJson.Bool( o, "candemote", b.CanDemote ),
			Admin = RPJson.Int( o, "admin", b.Admin ),
			NeedToChangeFrom = RPJson.StrArray( o, "NeedToChangeFrom", b.NeedToChangeFrom ),
			StartHealth = RPJson.Flt( o, "health", b.StartHealth ),
			StartArmor = RPJson.Flt( o, "armor", b.StartArmor ),
			Police = RPJson.Bool( o, "police", b.Police ),
			Chief = RPJson.Bool( o, "chief", b.Chief ),
			Mayor = RPJson.Bool( o, "mayor", b.Mayor ),
			Medic = RPJson.Bool( o, "medic", b.Medic ),
			Hobo = RPJson.Bool( o, "hobo", b.Hobo ),
			Cook = RPJson.Bool( o, "cook", b.Cook ),
			Hitman = RPJson.Bool( o, "hitman", b.Hitman ),
			Hidden = RPJson.Bool( o, "hidden", b.Hidden ),
			Index = RPJson.Int( o, "index", b.Index ),
			Source = RPJson.Str( o, "source", "" ),
			CustomCheck = b.CustomCheck,
			CustomCheckFailMsg = b.CustomCheckFailMsg,
			OnSpawn = b.OnSpawn,
			OnDeath = b.OnDeath,
		};
		return j;
	}

	public static void Shipment( RPJsonWriter w, RPShipment s )
	{
		w.BeginObject();
		w.Str( "name", s.Name ).Str( "entity", s.Entity ).Str( "model", s.Model ).Num( "price", s.Price ).Num( "amount", s.Amount );
		w.Bool( "separate", s.Separate ).Num( "pricesep", s.PriceSep ).Bool( "noship", s.NoShip ).StrArray( "allowed", s.Allowed );
		w.Str( "category", s.Category ).Num( "sortOrder", s.SortOrder ).Bool( "hidden", s.Hidden ).Num( "index", s.Index ).Str( "source", s.Source );
		w.EndObject();
	}

	public static RPShipment ShipmentFrom( JsonObject o, RPShipment baseS )
	{
		var b = baseS ?? new RPShipment();
		return new RPShipment
		{
			Name = RPJson.Str( o, "name", b.Name ), Entity = RPJson.Str( o, "entity", b.Entity ), Model = RPJson.Str( o, "model", b.Model ),
			Price = RPJson.Int( o, "price", b.Price ), Amount = RPJson.Int( o, "amount", b.Amount ), Separate = RPJson.Bool( o, "separate", b.Separate ),
			PriceSep = RPJson.Int( o, "pricesep", b.PriceSep ), NoShip = RPJson.Bool( o, "noship", b.NoShip ), Allowed = RPJson.StrArray( o, "allowed", b.Allowed ),
			Category = RPJson.Str( o, "category", b.Category ), SortOrder = RPJson.Int( o, "sortOrder", b.SortOrder ), Hidden = RPJson.Bool( o, "hidden", b.Hidden ),
			Index = RPJson.Int( o, "index", b.Index ), Source = RPJson.Str( o, "source", "" ), CustomCheck = b.CustomCheck, CustomCheckFailMsg = b.CustomCheckFailMsg,
		};
	}

	public static void Entity( RPJsonWriter w, RPEntityDef e )
	{
		w.BeginObject();
		w.Str( "name", e.Name ).Str( "ent", e.Ent ).Str( "model", e.Model ).Num( "price", e.Price ).Num( "max", e.Max ).Str( "cmd", e.Cmd );
		w.StrArray( "allowed", e.Allowed ).Str( "category", e.Category ).Num( "sortOrder", e.SortOrder ).Bool( "hidden", e.Hidden ).Num( "index", e.Index ).Str( "source", e.Source );
		w.EndObject();
	}

	public static RPEntityDef EntityFrom( JsonObject o, RPEntityDef baseE )
	{
		var b = baseE ?? new RPEntityDef();
		return new RPEntityDef
		{
			Name = RPJson.Str( o, "name", b.Name ), Ent = RPJson.Str( o, "ent", b.Ent ), Model = RPJson.Str( o, "model", b.Model ), Price = RPJson.Int( o, "price", b.Price ),
			Max = RPJson.Int( o, "max", b.Max ), Cmd = RPJson.Str( o, "cmd", b.Cmd ), Allowed = RPJson.StrArray( o, "allowed", b.Allowed ),
			Category = RPJson.Str( o, "category", b.Category ), SortOrder = RPJson.Int( o, "sortOrder", b.SortOrder ), Hidden = RPJson.Bool( o, "hidden", b.Hidden ),
			Index = RPJson.Int( o, "index", b.Index ), Source = RPJson.Str( o, "source", "" ), CustomCheck = b.CustomCheck, CustomCheckFailMsg = b.CustomCheckFailMsg,
		};
	}

	public static void Ammo( RPJsonWriter w, RPAmmoDef a )
	{
		w.BeginObject();
		w.Str( "name", a.Name ).Str( "ammoType", a.AmmoType ).Str( "model", a.Model ).Num( "price", a.Price ).Num( "amountGiven", a.Amount );
		w.StrArray( "allowed", a.Allowed ).Str( "category", a.Category ).Num( "sortOrder", a.SortOrder ).Num( "index", a.Index ).Str( "source", a.Source );
		w.EndObject();
	}

	public static RPAmmoDef AmmoFrom( JsonObject o ) => new RPAmmoDef
	{
		Name = RPJson.Str( o, "name", "" ), AmmoType = RPJson.Str( o, "ammoType", "pistol" ), Model = RPJson.Str( o, "model", "" ), Price = RPJson.Int( o, "price", 30 ),
		Amount = RPJson.Int( o, "amountGiven", 24 ), Allowed = RPJson.StrArray( o, "allowed", new string[0] ), Category = RPJson.Str( o, "category", "Other" ),
		SortOrder = RPJson.Int( o, "sortOrder", 100 ), Index = RPJson.Int( o, "index", 0 ), Source = RPJson.Str( o, "source", "" ),
	};

	public static void Food( RPJsonWriter w, RPFoodDef f )
	{
		w.BeginObject();
		w.Str( "name", f.Name ).Str( "model", f.Model ).Num( "energy", f.Energy ).Num( "price", f.Price ).Bool( "requiresCook", f.RequiresCook ).Num( "index", f.Index ).Str( "source", f.Source );
		w.EndObject();
	}

	public static RPFoodDef FoodFrom( JsonObject o ) => new RPFoodDef
	{
		Name = RPJson.Str( o, "name", "" ), Model = RPJson.Str( o, "model", "" ), Energy = RPJson.Int( o, "energy", 20 ), Price = RPJson.Int( o, "price", 10 ),
		RequiresCook = RPJson.Bool( o, "requiresCook", true ), Index = RPJson.Int( o, "index", 0 ), Source = RPJson.Str( o, "source", "" ),
	};

	public static void Category( RPJsonWriter w, RPCategory c )
	{
		w.BeginObject();
		w.Str( "name", c.Name ).Str( "categorises", c.Categorises ).Str( "color", c.Color ).Bool( "startExpanded", c.StartExpanded ).Num( "sortOrder", c.SortOrder );
		w.EndObject();
	}

	public static RPCategory CategoryFrom( JsonObject o ) => new RPCategory
	{
		Name = RPJson.Str( o, "name", "" ), Categorises = RPJson.Str( o, "categorises", "jobs" ), Color = RPJson.Str( o, "color", "#3c3c3c" ),
		StartExpanded = RPJson.Bool( o, "startExpanded", true ), SortOrder = RPJson.Int( o, "sortOrder", 100 ),
	};

	public static void Weapon( RPJsonWriter w, RPWeaponDef d )
	{
		w.BeginObject();
		w.Str( "id", d.Id ).Str( "name", d.Name ).Str( "prefab", d.Prefab ).Str( "model", d.Model ).Bool( "license", d.NeedsLicense );
		w.EndObject();
	}

	/// <summary>One kind as a JSON document { "kind": [...] }.</summary>
	public static string List( string kind, RPRegistry r, bool pretty )
	{
		var w = new RPJsonWriter { Pretty = pretty };
		w.BeginObject();
		WriteKind( w, kind, r );
		w.EndObject();
		return w.ToString();
	}

	static void WriteKind( RPJsonWriter w, string kind, RPRegistry r )
	{
		w.BeginArray( kind );
		if ( kind == "jobs" ) foreach ( var j in r.Jobs ) Job( w, j );
		if ( kind == "shipments" ) foreach ( var s in r.Shipments ) Shipment( w, s );
		if ( kind == "entities" ) foreach ( var e in r.Entities ) Entity( w, e );
		if ( kind == "ammo" ) foreach ( var a in r.Ammo ) Ammo( w, a );
		if ( kind == "food" ) foreach ( var f in r.Food ) Food( w, f );
		if ( kind == "categories" ) foreach ( var c in r.Categories ) Category( w, c );
		if ( kind == "weapons" ) foreach ( var k in r.Weapons.Keys ) Weapon( w, r.Weapons[k] );
		w.EndArray();
	}

	/// <summary>Everything clients need, compact (synced through RPWorld.ContentJson).</summary>
	public static string ForClients( RPRegistry r )
	{
		var w = new RPJsonWriter { Pretty = false };
		w.BeginObject();
		w.BeginObject( "config" );
		foreach ( var key in r.Config.Order ) w.Str( key, r.Config.Settings[key].Value );
		w.EndObject();
		w.StrArray( "modulesOff", r.DisabledModules.ToArray() );
		WriteKind( w, "jobs", r );
		WriteKind( w, "shipments", r );
		WriteKind( w, "entities", r );
		WriteKind( w, "ammo", r );
		WriteKind( w, "food", r );
		WriteKind( w, "categories", r );
		WriteKind( w, "weapons", r );
		w.BeginArray( "doorGroups" );
		foreach ( var g in r.DoorGroups ) { w.BeginObject(); w.Str( "name", g.Name ).StrArray( "jobs", g.Jobs ); w.EndObject(); }
		w.EndArray();
		w.BeginArray( "agendas" );
		foreach ( var a in r.Agendas ) { w.BeginObject(); w.Str( "title", a.Title ).StrArray( "managers", a.Managers ).StrArray( "listeners", a.Listeners ); w.EndObject(); }
		w.EndArray();
		w.BeginArray( "commands" );
		foreach ( var k in r.Commands.Keys.OrderBy( x => x ) ) { var c = r.Commands[k]; w.BeginObject(); w.Str( "name", c.Name ).Str( "description", c.Description ).Str( "permission", c.Permission ); w.EndObject(); }
		w.EndArray();
		w.EndObject();
		return w.ToString();
	}

	/// <summary>Client: replaces the local content with what the host sent.</summary>
	public static void ApplyFromHost( RPRegistry r, string json )
	{
		var o = RPJson.Parse( json );
		if ( o == null ) return;
		var cfg = RPJson.Obj( o, "config" );
		if ( cfg != null ) foreach ( var key in r.Config.Order ) if ( RPJson.HasKey( cfg, key ) ) r.Config.Set( key, RPJson.Str( cfg, key, "" ) );
		r.DisabledModules.Clear();
		foreach ( var m in RPJson.StrArray( o, "modulesOff", new string[0] ) ) r.DisabledModules.Add( m );

		var jobs = new List<RPJob>();
		var a = RPJson.Arr( o, "jobs" );
		if ( a != null ) foreach ( var n in a ) { var x = n.AsObject(); jobs.Add( JobFrom( x, r.Job( RPJson.Str( x, "command", "" ) ) ) ); }
		r.Jobs.Clear(); r.Jobs.AddRange( jobs );

		var ships = new List<RPShipment>();
		a = RPJson.Arr( o, "shipments" );
		if ( a != null ) foreach ( var n in a ) { var x = n.AsObject(); ships.Add( ShipmentFrom( x, r.Shipment( RPJson.Str( x, "name", "" ) ) ) ); }
		r.Shipments.Clear(); r.Shipments.AddRange( ships );

		var ents = new List<RPEntityDef>();
		a = RPJson.Arr( o, "entities" );
		if ( a != null ) foreach ( var n in a ) { var x = n.AsObject(); ents.Add( EntityFrom( x, r.EntityDef( RPJson.Str( x, "cmd", "" ) ) ) ); }
		r.Entities.Clear(); r.Entities.AddRange( ents );

		a = RPJson.Arr( o, "ammo" );
		r.Ammo.Clear();
		if ( a != null ) foreach ( var n in a ) r.Ammo.Add( AmmoFrom( n.AsObject() ) );
		a = RPJson.Arr( o, "food" );
		r.Food.Clear();
		if ( a != null ) foreach ( var n in a ) r.Food.Add( FoodFrom( n.AsObject() ) );
		a = RPJson.Arr( o, "categories" );
		r.Categories.Clear();
		if ( a != null ) foreach ( var n in a ) r.Categories.Add( CategoryFrom( n.AsObject() ) );
		a = RPJson.Arr( o, "weapons" );
		if ( a != null )
		{
			foreach ( var n in a )
			{
				var x = n.AsObject();
				r.AddWeapon( new RPWeaponDef { Id = RPJson.Str( x, "id", "" ), Name = RPJson.Str( x, "name", "" ), Prefab = RPJson.Str( x, "prefab", "" ), Model = RPJson.Str( x, "model", "" ), NeedsLicense = RPJson.Bool( x, "license", true ) } );
			}
		}
		a = RPJson.Arr( o, "doorGroups" );
		r.DoorGroups.Clear();
		if ( a != null ) foreach ( var n in a ) { var x = n.AsObject(); r.DoorGroups.Add( new RPDoorGroup { Name = RPJson.Str( x, "name", "" ), Jobs = RPJson.StrArray( x, "jobs", new string[0] ) } ); }
		a = RPJson.Arr( o, "agendas" );
		r.Agendas.Clear();
		if ( a != null ) foreach ( var n in a ) { var x = n.AsObject(); r.Agendas.Add( new RPAgendaDef { Title = RPJson.Str( x, "title", "" ), Managers = RPJson.StrArray( x, "managers", new string[0] ), Listeners = RPJson.StrArray( x, "listeners", new string[0] ) } ); }
		a = RPJson.Arr( o, "commands" );
		if ( a != null )
		{
			foreach ( var n in a )
			{
				var x = n.AsObject();
				var name = RPJson.Str( x, "name", "" );
				if ( !r.Commands.ContainsKey( name ) ) r.Commands[name] = new RPCommandDef { Name = name, Description = RPJson.Str( x, "description", "" ), Permission = RPJson.Str( x, "permission", "" ) };
			}
		}
		r.Version++;
	}
}

/// <summary>darkrp/disabled_defaults.json, DarkRP's darkrpmodification/disabled_defaults.lua: every default job, shipment,
/// entity... and module, each set to false. Set one to true to remove that default (custom content with the same id,
/// from the data files or an addon, is still added). New defaults are added to the file with false; your values stay.</summary>
public static class RPDisabledDefaults
{
	public const string File = "disabled_defaults.json";

	/// <summary>The modules that can be switched off, with what switching them off does.</summary>
	public static string[] Modules() => new string[] { "hungermod", "hitmenu", "hud", "propprotection" };

	public static string ModuleHelp( string m )
	{
		if ( m == "hungermod" ) return "hunger, food, the cook job and the microwave (the hunger itself is switched on with \"hungermod\" in config.json)";
		if ( m == "hitmenu" ) return "hits (/hit) and hitman jobs";
		if ( m == "hud" ) return "the DarkRP HUD (Sandbox's health and ammo come back)";
		if ( m == "propprotection" ) return "DarkRP's prop protection (Sandbox's own rules apply)";
		return "";
	}

	static string GroupChatId( RPGroupChat g ) => !string.IsNullOrEmpty( g.Name ) ? g.Name : string.Join( ",", g.Jobs );

	/// <summary>Host, right after the defaults are registered.</summary>
	public static void Apply( RPRegistry r )
	{
		var o = RPJson.Parse( RPDataFiles.Read( File ) );
		if ( o == null && RPDataFiles.Exists( File ) ) { r.LoadErrors.Add( File + " is not valid JSON - fix it or delete it to get a fresh one." ); return; }

		var w = new RPJsonWriter();
		w.BeginObject();
		w.Str( "_readme", "DarkRP's disabled_defaults: everything DarkRP adds by default. Set an entry to true to remove that default (modules: switch the module off). Your own jobs/entities with the same id (data files or addons) are still added. Restart the map to apply. Module help: " + string.Join( "; ", Modules().Select( m => m + " = " + ModuleHelp( m ) ) ) + "." );

		var mods = Section( w, o, "modules", Modules().ToList() );
		foreach ( var m in mods ) if ( !r.DisabledModules.Contains( m ) ) r.DisabledModules.Add( m );

		var jobs = Section( w, o, "jobs", r.Jobs.Select( j => j.Command ).ToList() );
		var hitmen = Section( w, o, "hitmen", r.Jobs.Where( j => j.Hitman ).Select( j => j.Command ).ToList() );
		var ships = Section( w, o, "shipments", r.Shipments.Select( x => x.Name ).ToList() );
		var ents = Section( w, o, "entities", r.Entities.Select( x => x.Cmd ).ToList() );
		var ammo = Section( w, o, "ammo", r.Ammo.Select( x => x.Name ).ToList() );
		var food = Section( w, o, "food", r.Food.Select( x => x.Name ).ToList() );
		var doors = Section( w, o, "doorgroups", r.DoorGroups.Select( x => x.Name ).ToList() );
		var agendas = Section( w, o, "agendas", r.Agendas.Select( x => x.Title ).ToList() );
		var chats = Section( w, o, "groupchats", r.GroupChats.Select( x => GroupChatId( x ) ).ToList() );
		var demote = Section( w, o, "demotegroups", r.DemoteGroups.Select( x => x.Name ).ToList() );
		var laws = Section( w, o, "laws", r.DefaultLaws.ToList() );
		w.EndObject();

		var text = w.ToString();
		if ( RPDataFiles.Read( File ) != text ) RPDataFiles.Write( File, text );

		r.Jobs.RemoveAll( x => jobs.Contains( x.Command ) );
		foreach ( var j in r.Jobs ) if ( hitmen.Contains( j.Command ) ) j.Hitman = false;
		r.Shipments.RemoveAll( x => ships.Contains( x.Name ) );
		r.Entities.RemoveAll( x => ents.Contains( x.Cmd ) );
		r.Ammo.RemoveAll( x => ammo.Contains( x.Name ) );
		r.Food.RemoveAll( x => food.Contains( x.Name ) );
		r.DoorGroups.RemoveAll( x => doors.Contains( x.Name ) );
		r.Agendas.RemoveAll( x => agendas.Contains( x.Title ) );
		r.GroupChats.RemoveAll( x => chats.Contains( GroupChatId( x ) ) );
		r.DemoteGroups.RemoveAll( x => demote.Contains( x.Name ) );
		r.DefaultLaws.RemoveAll( x => laws.Contains( x ) );
		var removed = jobs.Count + ships.Count + ents.Count + ammo.Count + food.Count + doors.Count + agendas.Count + chats.Count + demote.Count + laws.Count;
		if ( removed > 0 || mods.Count > 0 ) Log.Info( "[DarkRP] disabled_defaults.json: " + removed + " defaults removed" + (mods.Count > 0 ? ", modules off: " + string.Join( ", ", mods ) : "") );
	}

	/// <summary>Writes one section (every id, keeping the file's values) and returns the ids set to true.</summary>
	static List<string> Section( RPJsonWriter w, JsonObject file, string key, List<string> ids )
	{
		var existing = RPJson.Obj( file, key );
		var disabled = new List<string>();
		w.BeginObject( key );
		foreach ( var id in ids.Distinct() )
		{
			if ( string.IsNullOrEmpty( id ) ) continue;
			var off = RPJson.Bool( existing, id, false );
			if ( off ) disabled.Add( id );
			w.Bool( id, off );
		}
		w.EndObject();
		return disabled;
	}
}
