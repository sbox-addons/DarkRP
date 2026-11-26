// DarkRP for s&box - the DarkRP menu (DarkRP's F4 menu; s&box reserves F1-F12, so it opens with M) and dialogs
// (door menu, pocket, prompts, letters, tip jar, weapon checker). Ported in spirit from DarkRP's f4menu (MIT, Falco Peijnenburg).
using Sandbox.UI;

[Title( "DarkRP Menu" ), Hide]
public sealed class RPMenuPanel : PanelComponent
{
	const string Css = @"
.overlay { position: absolute; left: 0; top: 0; width: 100%; height: 100%; justify-content: center; align-items: center; background-color: rgba( 0, 0, 0, 0.45 ); pointer-events: all; }
.window { flex-direction: row; width: 1080px; height: 680px; max-width: 94%; max-height: 90%; overflow: hidden; }
.side { flex-direction: column; width: 220px; flex-shrink: 0; padding: 14px 10px; gap: 4px; background-color: rgba( 0, 0, 0, 0.25 ); border-right: 1px solid rgba( 255, 255, 255, 0.06 ); }
.brand { flex-direction: column; padding: 4px 10px 14px; }
.brand .logo { font-size: 26px; font-weight: 900; letter-spacing: 1px; }
.brand .logo-sub { font-size: 11px; color: #9aa4ae; }
.me { flex-direction: column; padding: 10px; margin: 0 0 10px; border-radius: 8px; background-color: rgba( 255, 255, 255, 0.04 ); }
.me .n { font-size: 14px; font-weight: 700; }
.me .m { font-size: 16px; font-weight: 800; color: #6fe08a; }
.tab { flex-direction: row; align-items: center; gap: 10px; padding: 8px 12px; border-radius: 7px; cursor: pointer; }
.tab:hover { background-color: rgba( 255, 255, 255, 0.07 ); }
.tab.active { background-color: #2f9e57; }
.tab .icon { font-size: 18px; }
.tab .t { font-size: 14px; font-weight: 600; }
.main { flex-direction: column; flex-grow: 1; min-width: 0; }
.head { flex-direction: row; align-items: center; height: 56px; flex-shrink: 0; padding: 0 20px; border-bottom: 1px solid rgba( 255, 255, 255, 0.06 ); }
.head .h { flex-grow: 1; font-size: 20px; font-weight: 800; }
.content { flex-direction: row; flex-grow: 1; min-height: 0; }
.list { flex-direction: column; flex-grow: 1; overflow-y: scroll; padding: 12px 16px; gap: 6px; }
.list > * { flex-shrink: 0; }
.cat { flex-direction: row; align-items: center; gap: 8px; margin: 8px 0 2px; }
.cat .bar { width: 4px; height: 16px; border-radius: 2px; }
.cat .t { font-size: 12px; font-weight: 800; letter-spacing: 1px; text-transform: uppercase; color: #c5ccd3; }
.item { flex-direction: row; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 8px; background-color: rgba( 255, 255, 255, 0.04 ); cursor: pointer; }
.item:hover { background-color: rgba( 255, 255, 255, 0.08 ); }
.item.selected { background-color: rgba( 47, 158, 87, 0.25 ); }
.item.locked { opacity: 0.5; }
.item .swatch { width: 6px; height: 34px; border-radius: 3px; flex-shrink: 0; }
.item .body { flex-direction: column; flex-grow: 1; min-width: 0; }
.item .t { font-size: 15px; font-weight: 700; }
.item .s { font-size: 12px; color: #9aa4ae; }
.item .price { font-size: 15px; font-weight: 800; color: #6fe08a; }
.detail { flex-direction: column; width: 340px; flex-shrink: 0; padding: 18px; gap: 10px; border-left: 1px solid rgba( 255, 255, 255, 0.06 ); overflow-y: scroll; }
.detail .dt { font-size: 22px; font-weight: 800; }
.detail .dd { font-size: 13px; color: #c5ccd3; }
.detail .lbl { font-size: 11px; font-weight: 800; letter-spacing: 1px; color: #9aa4ae; text-transform: uppercase; margin-top: 6px; }
.detail .v { font-size: 13px; }
.grid { flex-direction: row; flex-wrap: wrap; gap: 8px; }
.empty { padding: 30px; color: #9aa4ae; font-size: 14px; }
.row2 { flex-direction: row; align-items: center; gap: 8px; }
.setting { flex-direction: row; align-items: center; gap: 10px; padding: 7px 10px; border-radius: 7px; background-color: rgba( 255, 255, 255, 0.04 ); cursor: pointer; }
.setting:hover { background-color: rgba( 255, 255, 255, 0.08 ); }
.setting .k { font-size: 13px; font-weight: 700; width: 210px; flex-shrink: 0; }
.setting .val { font-size: 13px; font-weight: 800; color: #6fb7ff; width: 90px; flex-shrink: 0; }
.setting .help { font-size: 12px; color: #9aa4ae; flex-grow: 1; }
";

	RPClickRoot _root;
	string _key;

	protected override void OnUpdate()
	{
		var root = Panel;
		if ( root == null ) return;
		if ( _root == null || !_root.IsValid() )
		{
			root.StyleSheet.Parse( RPStyle.Base );
			root.StyleSheet.Parse( Css );
			_root = root.AddChild<RPClickRoot>();
			_root.AddClass( "rp" );
			_key = null;
		}
		var h = RPUi.H;
		var rp = RP.Local();
		var reg = DarkRP.Registry();
		if ( !h.MenuOpen || rp == null || reg == null )
		{
			if ( _key != "" ) { _root.Reset(); _key = ""; }
			return;
		}
		var w = RPWorld.Current();
		var key = h.MenuTab + "|" + h.MenuSelected + "|" + reg.Version + "|" + rp.JobCommand + "|" + rp.Money + "|" + RP.All().Count + "|" + (w != null ? w.Laws.Count : 0) + "|" + string.Join( ",", RP.All().Select( p => p.JobCommand ) );
		if ( key == _key ) return;
		_key = key;
		Build( rp, reg );
	}

	void Build( RPPlayer rp, RPRegistry reg )
	{
		_root.Reset();
		var h = RPUi.H;
		var overlay = RPUi.Div( _root, "overlay" );
		var win = RPUi.Div( overlay, "window glass" );
		var side = RPUi.Div( win, "side" );
		var brand = RPUi.Div( side, "brand" );
		RPUi.Text( brand, "DarkRP", "logo" );
		RPUi.Text( brand, "for s&box · " + DarkRP.Version, "logo-sub" );
		var me = RPUi.Div( side, "me" );
		RPUi.Text( me, RP.NameOf( rp ) + " · " + RP.JobName( rp ), "n" );
		RPUi.Text( me, RPUtil.Money( rp.Money ), "m" );

		Tab( side, "jobs", "work", "Jobs" );
		Tab( side, "weapons", "gps_fixed", "Weapons" );
		Tab( side, "shipments", "inventory_2", "Shipments" );
		Tab( side, "entities", "memory", "Entities" );
		Tab( side, "ammo", "bolt", "Ammo" );
		if ( reg.Config.Bool( "hungermod" ) ) Tab( side, "food", "restaurant", "Food" );
		Tab( side, "actions", "touch_app", "Actions" );
		Tab( side, "commands", "help_outline", "Commands" );
		if ( RP.IsAdmin( Connection.Local ) ) Tab( side, "admin", "admin_panel_settings", "Admin" );

		var main = RPUi.Div( win, "main" );
		var head = RPUi.Div( main, "head" );
		RPUi.Text( head, TabTitle( h.MenuTab ), "h" );
		RPUi.Button( _root, head, "Close (M)", "", "close:" );
		var content = RPUi.Div( main, "content" );
		var list = RPUi.Div( content, "list" );

		switch ( h.MenuTab )
		{
			case "jobs": Jobs( list, content, rp, reg ); break;
			case "weapons": Shop( list, rp, reg, "weapons" ); break;
			case "shipments": Shop( list, rp, reg, "shipments" ); break;
			case "entities": Shop( list, rp, reg, "entities" ); break;
			case "ammo": Shop( list, rp, reg, "ammo" ); break;
			case "food": Shop( list, rp, reg, "food" ); break;
			case "actions": Actions( list, rp ); break;
			case "commands": Commands( list, reg ); break;
			case "admin": Admin( list, reg ); break;
		}
	}

	static string TabTitle( string t ) => t == "jobs" ? "Jobs" : t == "weapons" ? "Weapons" : t == "shipments" ? "Shipments" : t == "entities" ? "Entities" : t == "ammo" ? "Ammo" : t == "food" ? "Food" : t == "actions" ? "Actions" : t == "commands" ? "Chat commands" : "Admin";

	void Tab( Panel side, string id, string icon, string title )
	{
		var t = RPUi.Div( side, "tab" );
		if ( RPUi.H.MenuTab == id ) t.AddClass( "active" );
		RPUi.Icon( t, icon, "" );
		RPUi.Text( t, title, "t" );
		_root.Clicks[t] = "tab:" + id;
	}

	void Category( Panel list, string name, string color )
	{
		var c = RPUi.Div( list, "cat" );
		var bar = RPUi.Div( c, "bar" );
		bar.Style.BackgroundColor = RPUtil.ParseColor( color );
		RPUi.Text( c, name, "t" );
	}

	// ---- jobs ----

	void Jobs( Panel list, Panel content, RPPlayer rp, RPRegistry reg )
	{
		var h = RPUi.H;
		var cats = reg.Categories.Where( c => c.Categorises == "jobs" ).ToList();
		RPJob selected = null;
		foreach ( var cat in cats )
		{
			var jobs = reg.Jobs.Where( j => !j.Hidden && (j.Category ?? "Other") == cat.Name && !(j.Cook && !reg.Config.Bool( "hungermod" )) ).ToList();
			if ( jobs.Count == 0 ) continue;
			Category( list, cat.Name, cat.Color );
			foreach ( var j in jobs )
			{
				int count = RP.All().Count( p => p.JobCommand == j.Command );
				var item = RPUi.Div( list, "item" );
				if ( h.MenuSelected == j.Command ) { item.AddClass( "selected" ); selected = j; }
				if ( rp.JobCommand == j.Command ) item.AddClass( "selected" );
				var sw = RPUi.Div( item, "swatch" );
				sw.Style.BackgroundColor = RPUtil.ParseColor( j.Color );
				var body = RPUi.Div( item, "body" );
				RPUi.Text( body, j.Name, "t" );
				var max = j.Max <= 0 ? "∞" : j.Max < 1 ? (int)(j.Max * 100) + "%" : ((int)j.Max).ToString();
				RPUi.Text( body, "Salary " + RPUtil.Money( j.Salary ) + "  ·  " + count + "/" + max + (j.Vote ? "  ·  vote" : "") + (j.Admin > 0 ? "  ·  admin" : ""), "s" );
				_root.Clicks[item] = "select:" + j.Command;
			}
		}
		if ( selected == null ) selected = reg.Job( rp.JobCommand );
		if ( selected == null ) return;

		var d = RPUi.Div( content, "detail" );
		var title = RPUi.Text( d, selected.Name, "dt" );
		title.Style.FontColor = RPUtil.ParseColor( selected.Color );
		RPUi.Text( d, selected.Description, "dd" );
		RPUi.Text( d, "Salary", "lbl" );
		RPUi.Text( d, RPUtil.Money( selected.Salary ) + " every " + RPUtil.Duration( reg.Config.Int( "paydelay" ) ), "v" );
		if ( selected.Weapons != null && selected.Weapons.Length > 0 )
		{
			RPUi.Text( d, "Weapons", "lbl" );
			RPUi.Text( d, string.Join( ", ", selected.Weapons.Select( x => reg.WeaponName( x ) ) ), "v" );
		}
		RPUi.Text( d, "Appearance", "lbl" );
		RPUi.Text( d, selected.Models != null && selected.Models.Length > 0 ? "Custom model" : selected.Outfit == null ? "Your own avatar" : "Job outfit (" + selected.Outfit.Length + " items)", "v" );
		var flags = new List<string>();
		if ( selected.Police ) flags.Add( "Police" );
		if ( selected.Mayor ) flags.Add( "Mayor" );
		if ( selected.HasLicense ) flags.Add( "Gun license" );
		if ( selected.Vote ) flags.Add( "Needs a vote" );
		if ( selected.NeedToChangeFrom != null && selected.NeedToChangeFrom.Length > 0 ) flags.Add( "From: " + string.Join( ", ", selected.NeedToChangeFrom ) );
		if ( flags.Count > 0 ) { RPUi.Text( d, "Notes", "lbl" ); RPUi.Text( d, string.Join( " · ", flags ), "v" ); }
		if ( rp.JobCommand == selected.Command ) RPUi.Text( d, "This is your job.", "muted" );
		else RPUi.Button( _root, d, selected.Vote ? "Start vote for " + selected.Name : "Become " + selected.Name, "primary", "job:" + selected.Command );
	}

	// ---- shop ----

	static bool Allowed( string[] allowed, RPPlayer rp ) => allowed == null || allowed.Length == 0 || RPUtil.ContainsIgnoreCase( allowed, rp.JobCommand );

	static string AllowedText( string[] allowed, RPRegistry reg ) => "For: " + string.Join( ", ", allowed.Select( x => reg.Job( x ) != null ? reg.Job( x ).Name : x ) );

	void Shop( Panel list, RPPlayer rp, RPRegistry reg, string kind )
	{
		int shown = 0;
		var catKind = kind == "food" ? "food" : kind;
		var cats = reg.Categories.Where( c => c.Categorises == catKind ).Select( c => c.Name ).ToList();
		if ( kind == "food" ) cats = new List<string>( new[] { "Food" } );
		foreach ( var catName in cats )
		{
			bool header = false;
			void Head()
			{
				if ( header ) return;
				header = true;
				var cat = reg.Category( catName, catKind );
				Category( list, catName, cat != null ? cat.Color : "#3c3c3c" );
			}

			if ( kind == "weapons" || kind == "shipments" )
			{
				foreach ( var s in reg.Shipments )
				{
					if ( s.Hidden ) continue;
					bool single = kind == "weapons";
					if ( single && !(s.Separate || s.NoShip) ) continue;
					if ( !single && s.NoShip ) continue;
					if ( (s.Category ?? "Other") != catName ) continue;
					Head();
					var ok = Allowed( s.Allowed, rp );
					var price = single ? s.PriceSep : s.Price;
					Item( list, s.Name, single ? reg.WeaponName( s.Entity ) : s.Amount + "x " + reg.WeaponName( s.Entity ), price, ok, ok ? null : AllowedText( s.Allowed, reg ), "buy:" + (single ? "weapon" : "shipment") + "|" + s.Name, rp );
					shown++;
				}
			}
			else if ( kind == "entities" )
			{
				foreach ( var e in reg.Entities )
				{
					if ( e.Hidden || (e.Category ?? "Other") != catName ) continue;
					if ( e.Ent == "microwave" && !reg.Config.Bool( "hungermod" ) ) continue;
					Head();
					var ok = Allowed( e.Allowed, rp );
					Item( list, e.Name, "Max " + (e.Max <= 0 ? "∞" : e.Max.ToString()) + "  ·  /" + e.Cmd, e.Price, ok, ok ? null : AllowedText( e.Allowed, reg ), "buy:entity|" + e.Cmd, rp );
					shown++;
				}
			}
			else if ( kind == "ammo" )
			{
				foreach ( var a in reg.Ammo )
				{
					if ( (a.Category ?? "Other") != catName ) continue;
					Head();
					var ok = Allowed( a.Allowed, rp );
					Item( list, a.Name, a.Amount + " rounds", a.Price, ok, ok ? null : AllowedText( a.Allowed, reg ), "buy:ammo|" + a.Name, rp );
					shown++;
				}
			}
			else if ( kind == "food" )
			{
				foreach ( var f in reg.Food )
				{
					Head();
					Item( list, f.Name, "+" + f.Energy + " energy", f.Price, true, null, "buy:food|" + f.Name, rp );
					shown++;
				}
			}
		}
		if ( shown == 0 ) RPUi.Text( list, "Nothing to buy here.", "empty" );
	}

	void Item( Panel list, string title, string sub, int price, bool allowed, string note, string action, RPPlayer rp )
	{
		var item = RPUi.Div( list, "item" );
		if ( !allowed || rp.Money < price ) item.AddClass( "locked" );
		var body = RPUi.Div( item, "body" );
		RPUi.Text( body, title, "t" );
		RPUi.Text( body, note ?? sub, "s" );
		RPUi.Text( item, price > 0 ? RPUtil.Money( price ) : "Free", "price" );
		if ( allowed ) RPUi.Button( _root, item, "Buy", "primary", action );
	}

	// ---- actions ----

	void Actions( Panel list, RPPlayer rp )
	{
		var job = RP.JobOf( rp );
		Category( list, "Money", "#2f9e57" );
		var g = RPUi.Div( list, "grid" );
		RPUi.Button( _root, g, "Drop money", "", "prompt:Drop how much money?|/dropmoney|Amount" );
		RPUi.Button( _root, g, "Give money to who you look at", "", "prompt:Give how much money?|/give|Look at the player first" );
		RPUi.Button( _root, g, "Write a cheque", "", "prompt:Cheque: <player> <amount>|/cheque|" );

		Category( list, "You", "#4aa3ff" );
		g = RPUi.Div( list, "grid" );
		RPUi.Button( _root, g, "Change RP name", "", "prompt:Your new roleplay name|/rpname|" );
		RPUi.Button( _root, g, "Set job title", "", "prompt:Your job title (empty: the job's name)|/job|" );
		RPUi.Button( _root, g, "Drop weapon", "", "cmd:/drop" );
		RPUi.Button( _root, g, "Sell all doors", "", "cmd:/sellalldoors" );
		RPUi.Button( _root, g, "Request gun license", "", "cmd:/requestlicense" );
		RPUi.Button( _root, g, "Advert", "", "prompt:Advert text|/advert|" );
		RPUi.Button( _root, g, "Call the police", "", "prompt:What happened?|/cr|" );
		RPUi.Button( _root, g, "Write a letter", "", "prompt:Letter text|/write|" );
		RPUi.Button( _root, g, "Demote a player", "", "prompt:Demote: <player> <reason>|/demote|" );

		if ( job != null && job.Police )
		{
			Category( list, "Police", "#1919aa" );
			g = RPUi.Div( list, "grid" );
			RPUi.Button( _root, g, "Make wanted", "", "prompt:Wanted: <player> <reason>|/wanted|" );
			RPUi.Button( _root, g, "Remove wanted", "", "prompt:Unwanted: <player>|/unwanted|" );
			RPUi.Button( _root, g, "Request warrant", "", "prompt:Warrant: <player> <reason>|/warrant|" );
			RPUi.Button( _root, g, "Remove warrant", "", "prompt:Unwarrant: <player>|/unwarrant|" );
			if ( job.Chief || job.Mayor ) RPUi.Button( _root, g, "Set jail position here", "", "cmd:/jailpos" );
			if ( job.Chief || job.Mayor ) RPUi.Button( _root, g, "Add jail position here", "", "cmd:/addjailpos" );
		}
		if ( job != null && job.Mayor )
		{
			Category( list, "Mayor", "#961414" );
			g = RPUi.Div( list, "grid" );
			RPUi.Button( _root, g, "Add law", "", "prompt:New law|/addlaw|" );
			RPUi.Button( _root, g, "Remove law", "", "prompt:Law number to remove|/removelaw|" );
			RPUi.Button( _root, g, "Reset laws", "", "cmd:/resetlaws" );
			RPUi.Button( _root, g, "Place law board", "", "cmd:/placelaws" );
			RPUi.Button( _root, g, "Start lockdown", "danger", "cmd:/lockdown" );
			RPUi.Button( _root, g, "End lockdown", "", "cmd:/unlockdown" );
			RPUi.Button( _root, g, "Start lottery", "", "prompt:Ticket price|/lottery|" );
			RPUi.Button( _root, g, "Broadcast", "", "prompt:Message to the city|/broadcast|" );
		}
		if ( RPMayor.AgendaFor( Connection.Local, true ) != null )
		{
			Category( list, "Agenda", "#4b4b4b" );
			g = RPUi.Div( list, "grid" );
			RPUi.Button( _root, g, "Set agenda", "", "prompt:Agenda for your group|/agenda|" );
		}
	}

	void Commands( Panel list, RPRegistry reg )
	{
		foreach ( var k in reg.Commands.Keys.OrderBy( x => x ) )
		{
			var c = reg.Commands[k];
			if ( c.Permission == "admin" && !RP.IsAdmin( Connection.Local ) ) continue;
			var item = RPUi.Div( list, "item" );
			var body = RPUi.Div( item, "body" );
			RPUi.Text( body, "/" + k + (c.Permission == "admin" ? "  (admin)" : ""), "t" );
			RPUi.Text( body, c.Description, "s" );
		}
	}

	void Admin( Panel list, RPRegistry reg )
	{
		Category( list, "Players", "#b33a3a" );
		foreach ( var p in RP.All() )
		{
			var item = RPUi.Div( list, "item" );
			var body = RPUi.Div( item, "body" );
			RPUi.Text( body, RP.NameOf( p ) + "  ·  " + RP.JobName( p ), "t" );
			RPUi.Text( body, RPUtil.Money( p.Money ) + (p.Arrested ? "  ·  arrested" : "") + (p.Wanted ? "  ·  wanted" : ""), "s" );
			var n = "\"" + RP.NameOf( p ) + "\"";
			RPUi.Button( _root, item, "Money", "", "prompt:Set money of " + RP.NameOf( p ) + "|/setmoney " + n + "|" );
			RPUi.Button( _root, item, "Job", "", "prompt:Job command for " + RP.NameOf( p ) + "|/setjob " + n + "|" );
			RPUi.Button( _root, item, p.Arrested ? "Unarrest" : "Arrest", "", p.Arrested ? "cmd:/unarrest " + n : "cmd:/arrest " + n );
		}
		Category( list, "World", "#b33a3a" );
		var g = RPUi.Div( list, "grid" );
		RPUi.Button( _root, g, "Spawn ownable door", "", "cmd:/spawndoor" );
		RPUi.Button( _root, g, "Set job spawn here", "", "prompt:Job command|/setspawn|" );
		RPUi.Button( _root, g, "Save now", "", "cmd:/rpsave" );
		string group = "";
		foreach ( var key in reg.Config.Order )
		{
			var s = reg.Config.Settings[key];
			if ( s.Group != group ) { group = s.Group; Category( list, "Settings: " + group, "#6fb7ff" ); }
			var row = RPUi.Div( list, "setting" );
			RPUi.Text( row, key, "k" );
			RPUi.Text( row, s.Value, "val" );
			RPUi.Text( row, s.Help, "help" );
			_root.Clicks[row] = "config:" + key;
		}
	}
}

[Title( "DarkRP Dialogs" ), Hide]
public sealed class RPDialogPanel : PanelComponent
{
	const string Css = @"
.overlay { position: absolute; left: 0; top: 0; width: 100%; height: 100%; justify-content: center; align-items: center; background-color: rgba( 0, 0, 0, 0.35 ); pointer-events: all; }
.dlg { flex-direction: column; width: 460px; max-height: 80%; padding: 18px 20px; gap: 10px; }
.dlg .title { font-size: 19px; font-weight: 800; }
.dlg .sub { font-size: 13px; color: #9aa4ae; }
.dlg .text { font-size: 14px; white-space: pre-wrap; }
.dlg .grid { flex-direction: row; flex-wrap: wrap; gap: 8px; }
.dlg .entry { font-size: 16px; padding: 8px 10px; border-radius: 6px; background-color: rgba( 255, 255, 255, 0.06 ); border: 1px solid rgba( 111, 224, 138, 0.5 ); color: white; }
.dlg .row { flex-direction: row; align-items: center; gap: 10px; padding: 6px 8px; border-radius: 6px; background-color: rgba( 255, 255, 255, 0.04 ); }
.dlg .row .t { flex-grow: 1; font-size: 14px; }
.dlg .scroll { flex-direction: column; gap: 4px; overflow-y: scroll; max-height: 360px; }
";

	RPClickRoot _root;
	string _key;
	RPTextEntry _entry;

	protected override void OnUpdate()
	{
		var root = Panel;
		if ( root == null ) return;
		if ( _root == null || !_root.IsValid() )
		{
			root.StyleSheet.Parse( RPStyle.Base );
			root.StyleSheet.Parse( Css );
			_root = root.AddChild<RPClickRoot>();
			_root.AddClass( "rp" );
			_key = null;
		}
		var h = RPUi.H;
		var w = RPWorld.Current();
		var rp = RP.Local();
		string key = h.Dialog + "|" + h.DialogData + "|" + h.PromptTitle;
		if ( h.Dialog == "door" && w != null ) key += "|" + (w.Doors.ContainsKey( h.DoorId ) ? w.Doors[h.DoorId] : "") + "|" + (rp != null ? rp.Money : 0);
		if ( h.Dialog == "pocket" && rp != null ) key += "|" + rp.PocketList;

		if ( h.Dialog == "prompt" && _entry != null && _entry.IsValid() )
		{
			if ( !_entry.HasFocus ) _entry.Focus();
			if ( _entry.TakeSubmit() || Input.Keyboard.Pressed( "enter" ) || Input.Keyboard.Pressed( "return" ) || Input.Keyboard.Pressed( "kp_enter" ) ) { RPUi.Submit( _entry.Text ?? "" ); return; }
			if ( _entry.TakeCancel() || Input.EscapePressed || Input.Keyboard.Pressed( "escape" ) ) { Input.EscapePressed = false; RPUi.CloseDialog(); return; }
		}

		if ( key == _key ) return;
		_key = key;
		_root.Reset();
		_entry = null;
		if ( h.Dialog == "" || rp == null ) return;

		var overlay = RPUi.Div( _root, "overlay" );
		var dlg = RPUi.Div( overlay, "dlg glass" );
		switch ( h.Dialog )
		{
			case "door": Door( dlg, w, rp ); break;
			case "pocket": Pocket( dlg, rp ); break;
			case "prompt":
				RPUi.Text( dlg, h.PromptTitle, "title" );
				if ( !string.IsNullOrEmpty( h.PromptHint ) ) RPUi.Text( dlg, h.PromptHint, "sub" );
				_entry = dlg.AddChild<RPTextEntry>();
				_entry.AddClass( "entry" );
				_entry.Focus();
				var g = RPUi.Div( dlg, "grid" );
				RPUi.Text( g, "Enter to confirm, Escape to cancel", "sub" );
				break;
			case "letter":
				{
					var p = h.DialogData.Split( '\u0001' );
					RPUi.Text( dlg, "Letter from " + (p.Length > 0 ? p[0] : "?"), "title" );
					RPUi.Text( dlg, p.Length > 1 ? p[1] : "", "text" );
					RPUi.Button( _root, dlg, "Close", "", "close:" );
					break;
				}
			case "tipjar":
				{
					var p = h.DialogData.Split( '\u0001' );
					h.PromptTitle = "Tip " + (p.Length > 1 ? p[1] : "") + " - how much?";
					h.PromptCommand = "tip:" + (p.Length > 0 ? p[0] : "");
					h.PromptHint = p.Length > 2 ? "Tipped so far: " + RPUtil.Money( (long)RPUtil.ParseFloat( p[2], 0 ) ) : "";
					h.Dialog = "prompt";
					_key = null;
					break;
				}
			case "weaponcheck":
				{
					var p = h.DialogData.Split( '\u0001' );
					RPUi.Text( dlg, "Weapons of " + (p.Length > 0 ? p[0] : "?"), "title" );
					RPUi.Text( dlg, p.Length > 1 && p[1] == "1" ? "Has a gun license" : "No gun license", "sub" );
					var list = p.Length > 2 ? p[2].Split( '\u0002', StringSplitOptions.RemoveEmptyEntries ) : new string[0];
					RPUi.Text( dlg, list.Length == 0 ? "Nothing besides their job's equipment." : string.Join( "\n", list ), "text" );
					RPUi.Text( dlg, "Right click confiscates, reload gives them back.", "sub" );
					RPUi.Button( _root, dlg, "Close", "", "close:" );
					break;
				}
			case "keypadcheck":
				{
					var p = h.DialogData.Split( '\u0001' );
					RPUi.Text( dlg, p.Length > 0 ? p[0] : "Keypad", "title" );
					var list = p.Length > 1 ? p[1].Split( '\u0002', StringSplitOptions.RemoveEmptyEntries ) : new string[0];
					RPUi.Text( dlg, list.Length == 0 ? "This isn't linked to anything." : string.Join( "\n", list ), "text" );
					RPUi.Button( _root, dlg, "Close", "", "close:" );
					break;
				}
			default:
				RPUi.Text( dlg, h.Dialog, "title" );
				RPUi.Text( dlg, h.DialogData, "text" );
				RPUi.Button( _root, dlg, "Close", "", "close:" );
				break;
		}
	}

	void Door( Panel dlg, RPWorld w, RPPlayer rp )
	{
		if ( w == null ) return;
		var h = RPUi.H;
		var d = RPDoors.Get( w, h.DoorId );
		var cfg = DarkRP.Config();
		var admin = RP.IsAdmin( Connection.Local );
		RPUi.Text( dlg, string.IsNullOrEmpty( d.Title ) ? "Door" : d.Title, "title" );
		bool owner = RPDoors.IsOwner( d, rp );
		bool access = RPDoors.HasAccess( d, rp );
		if ( d.IsOwned ) RPUi.Text( dlg, owner ? "You own this door." : RPDoors.IsCoOwner( d, rp ) ? "You co-own this door." : "Owned by someone else.", "sub" );
		else if ( d.IsJobDoor ) RPUi.Text( dlg, string.IsNullOrEmpty( d.Group ) ? "Jobs: " + string.Join( ", ", d.Teams ) : d.Group, "sub" );
		else if ( d.NonOwnable ) RPUi.Text( dlg, "This door can't be owned.", "sub" );
		else RPUi.Text( dlg, "For sale: " + RPUtil.Money( cfg.Int( "doorcost" ) ), "sub" );
		var g = RPUi.Div( dlg, "grid" );
		if ( !d.IsOwned && !d.NonOwnable && !d.IsJobDoor ) RPUi.Button( _root, g, "Buy door (" + RPUtil.Money( cfg.Int( "doorcost" ) ) + ")", "primary", "door:buy" );
		if ( owner )
		{
			RPUi.Button( _root, g, "Sell door", "danger", "door:sell" );
			RPUi.Button( _root, g, "Set title", "", "door:title" );
			RPUi.Button( _root, g, "Add co-owner", "", "door:addowner" );
			if ( d.CoOwners.Count > 0 ) RPUi.Button( _root, g, "Remove co-owner", "", "door:removeowner" );
		}
		if ( access ) RPUi.Button( _root, g, d.Locked ? "Unlock" : "Lock", "", d.Locked ? "door:unlock" : "door:lock" );
		if ( admin )
		{
			RPUi.Text( dlg, "Admin", "sub" );
			var a = RPUi.Div( dlg, "grid" );
			RPUi.Button( _root, a, d.NonOwnable ? "Make ownable" : "Make unownable", "", "door:ownable" );
			RPUi.Button( _root, a, "Door group", "", "door:group" );
			RPUi.Button( _root, a, "Job doors", "", "door:teams" );
			if ( !d.IsOwned ) RPUi.Button( _root, a, "Set title", "", "door:title" );
			if ( d.IsOwned && !owner ) RPUi.Button( _root, a, "Force sell", "danger", "door:sell" );
		}
		RPUi.Button( _root, dlg, "Close", "", "close:" );
	}

	void Pocket( Panel dlg, RPPlayer rp )
	{
		RPUi.Text( dlg, "Pocket", "title" );
		var items = string.IsNullOrEmpty( rp.PocketList ) ? new string[0] : rp.PocketList.Split( '\u0001' );
		RPUi.Text( dlg, items.Length + " / " + DarkRP.Config().Int( "pocketitems" ) + " items · left click picks up, right click drops the last", "sub" );
		var list = RPUi.Div( dlg, "scroll" );
		for ( int i = 0; i < items.Length; i++ )
		{
			var row = RPUi.Div( list, "row" );
			RPUi.Text( row, items[i], "t" );
			RPUi.Button( _root, row, "Drop", "", "pocket:" + i );
		}
		RPUi.Button( _root, dlg, "Close", "", "close:" );
	}
}
