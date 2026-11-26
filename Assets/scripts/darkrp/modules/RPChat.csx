// DarkRP for s&box - chat: local talk range, OOC, whisper/yell/me/PM/group chat/adverts, and the command dispatcher.
// Ported from DarkRP's modules/chat (MIT, Falco Peijnenburg). DarkRP has its own chat box (RPChatPanel) because the
// engine's chat event isn't available to addon scripts.

public static class RPChat
{
	public const string White = "#ffffff";
	public const string Grey = "#b4b4b4";

	public static string NameColor( Connection c )
	{
		var rp = RP.Of( c );
		return rp != null ? RPUtil.Hex( RP.JobColor( rp ) ) : White;
	}

	/// <summary>Players within <paramref name="range"/> of the speaker (everyone if range ≤ 0).</summary>
	public static List<Connection> Near( Connection c, float range )
	{
		var list = new List<Connection>();
		var me = RP.Pawn( c );
		foreach ( var rp in RP.All() )
		{
			var other = RP.ConnOf( rp );
			if ( other == null ) continue;
			if ( range <= 0 || other == c ) { list.Add( other ); continue; }
			var p = RP.Pawn( other );
			if ( me != null && p != null && p.WorldPosition.Distance( me.WorldPosition ) <= range ) list.Add( other );
		}
		if ( !list.Contains( c ) ) list.Add( c );
		return list;
	}

	public static List<Connection> Everyone() => Near( null, 0 );

	public static void HandleSay( Connection c, string text, bool team )
	{
		if ( c == null ) return;
		text = (text ?? "").Trim();
		if ( text.Length == 0 ) return;
		if ( text.Length > 400 ) text = text.Substring( 0, 400 );
		var rp = RP.Of( c );
		if ( RP.CooldownLeft( rp, "chat", 0.35f ) > 0 ) return;

		var prefix = DarkRP.Config().Str( "chatCommandPrefix" );
		if ( string.IsNullOrEmpty( prefix ) ) prefix = "/";
		if ( text.StartsWith( "//" ) ) { Ooc( c, text.Substring( 2 ).Trim() ); return; }
		if ( text.StartsWith( prefix ) || text.StartsWith( "!" ) ) { RunCommand( c, text ); return; }

		var h = DarkRP.RunHook( new RPHook { Name = "PlayerSay", Player = c, Text = text } );
		if ( !h.Allowed ) return;
		text = h.Text;
		if ( string.IsNullOrEmpty( text ) ) return;
		if ( !RP.Alive( c ) && !DarkRP.Config().Bool( "deadtalk" ) ) { DarkRP.Notify( c, 1, 3, "You can't talk while dead." ); return; }
		var range = DarkRP.Config().Bool( "alltalk" ) ? 0 : DarkRP.Config().Int( "talkDistance" );
		var line = RPNet.Join( RPNet.Seg( NameColor( c ), RP.Name( c ) ), RPNet.Seg( White, ": " + text ) );
		if ( !RP.Alive( c ) ) line = RPNet.Join( RPNet.Seg( "#ff3c3c", "*DEAD* " ), line );
		RPNet.ChatLine( Near( c, range ), line );
		RPLog.Write( "[chat] " + RP.Name( c ) + ": " + text );
	}

	public static void Ooc( Connection c, string text )
	{
		if ( string.IsNullOrEmpty( text ) ) return;
		if ( !DarkRP.Config().Bool( "ooc" ) ) { DarkRP.Notify( c, 1, 3, "OOC is disabled." ); return; }
		var line = RPNet.Join( RPNet.Seg( "#ff3c3c", "(OOC) " ), RPNet.Join( RPNet.Seg( NameColor( c ), RP.Name( c ) ), RPNet.Seg( White, ": " + text ) ) );
		RPNet.ChatLine( Everyone(), line );
		RPLog.Write( "[ooc] " + RP.Name( c ) + ": " + text );
	}

	public static void Ranged( Connection c, string tag, string tagColor, string text, float range )
	{
		if ( string.IsNullOrEmpty( text ) ) return;
		if ( !RP.Alive( c ) && !DarkRP.Config().Bool( "deadtalk" ) ) return;
		var line = RPNet.Join( RPNet.Seg( tagColor, tag ), RPNet.Join( RPNet.Seg( NameColor( c ), RP.Name( c ) ), RPNet.Seg( White, ": " + text ) ) );
		RPNet.ChatLine( Near( c, range ), line );
	}

	/// <summary>Runs "/command args" for a player.</summary>
	public static void RunCommand( Connection c, string text )
	{
		if ( c == null || string.IsNullOrWhiteSpace( text ) ) return;
		text = text.Trim();
		if ( text.StartsWith( "//" ) ) { Ooc( c, text.Substring( 2 ).Trim() ); return; }
		if ( text.StartsWith( "/" ) || text.StartsWith( "!" ) ) text = text.Substring( 1 );
		var space = text.IndexOf( ' ' );
		var name = (space < 0 ? text : text.Substring( 0, space )).ToLowerInvariant();
		var rest = space < 0 ? "" : text.Substring( space + 1 ).Trim();
		var reg = DarkRP.Registry();
		var rp = RP.Of( c );
		if ( rp == null || reg == null ) return;

		var cmd = new RPCmd { Caller = c, Command = name, ArgString = rest, Args = RPUtil.SplitArgs( rest ) };
		var h = DarkRP.RunHook( new RPHook { Name = "onChatCommand", Player = c, Text = name, Reason = rest } );
		if ( !h.Allowed ) { if ( !string.IsNullOrEmpty( h.Reason ) ) DarkRP.Notify( c, 1, 4, h.Reason ); return; }

		if ( reg.Commands.ContainsKey( name ) )
		{
			var def = reg.Commands[name];
			if ( !RP.HasPermission( c, def.Permission ) ) { DarkRP.Notify( c, 1, 4, "You don't have access to /" + name + "." ); return; }
			float left = def.Delay > 0 ? RP.CooldownLeft( rp, "cmd_" + name, def.Delay ) : 0;
			if ( left > 0 ) { DarkRP.Notify( c, 1, 3, "Wait " + (int)Math.Ceiling( left ) + "s before using /" + name + " again." ); return; }
			if ( def.Handler == null ) return;
			try { def.Handler( cmd ); }
			catch ( Exception e ) { Log.Warning( "DarkRP /" + name + " failed: " + e ); DarkRP.Notify( c, 1, 4, "/" + name + " failed." ); }
			return;
		}

		// /<job command>, /vote<job command>
		var job = reg.Job( name );
		if ( job == null && name.StartsWith( "vote" ) ) job = reg.Job( name.Substring( 4 ) );
		if ( job != null ) { RPJobs.Request( c, job ); return; }

		// /buy<entity> (e.g. /buymoneyprinter)
		var ent = reg.EntityDef( name );
		if ( ent != null ) { RPShop.Buy( c, "entity", ent.Cmd, 1 ); return; }

		DarkRP.Notify( c, 1, 4, "Unknown command: /" + name + ". Press M for the menu." );
	}
}

/// <summary>DarkRP's built-in modules: registers the chat commands (and module hooks) in every registry.</summary>
public static class RPModules
{
	public static void Register( RPRegistry r )
	{
		RPCommands.Register( r );
		RPPropProtection.Register( r );
	}
}

public sealed class RPCommands
{
	static void C( RPRegistry r, string name, string desc, Action<RPCmd> fn ) => r.AddCommand( new RPCommandDef { Name = name, Description = desc, Handler = fn } );
	static void C( RPRegistry r, string name, string desc, float delay, Action<RPCmd> fn ) => r.AddCommand( new RPCommandDef { Name = name, Description = desc, Delay = delay, Handler = fn } );
	static void A( RPRegistry r, string name, string desc, Action<RPCmd> fn ) => r.AddCommand( new RPCommandDef { Name = name, Description = desc, Permission = "admin", Handler = fn } );

	static long Amount( RPCmd cmd, int i )
	{
		var s = cmd.Arg( i ).Replace( ",", "" ).Replace( "$", "" );
		return (long)RPUtil.ParseFloat( s, 0 );
	}

	static RPWorld W() => RPWorld.Current();

	static Connection Target( RPCmd cmd, int i )
	{
		var t = RP.Find( cmd.Arg( i ) );
		if ( t == null ) cmd.Error( "Player not found: " + cmd.Arg( i ) );
		return t;
	}

	public static void Register( RPRegistry r )
	{
		// ---- talking ----
		C( r, "ooc", "Global out-of-character chat (also //text).", cmd => RPChat.Ooc( cmd.Caller, cmd.ArgString ) );
		C( r, "a", "Global out-of-character chat.", cmd => RPChat.Ooc( cmd.Caller, cmd.ArgString ) );
		C( r, "w", "Whisper to players very close to you.", cmd => RPChat.Ranged( cmd.Caller, "(Whisper) ", "#b4b4ff", cmd.ArgString, DarkRP.Config().Int( "whisperDistance" ) ) );
		C( r, "y", "Yell to players further away.", cmd => RPChat.Ranged( cmd.Caller, "(Yell) ", "#ffb43c", cmd.ArgString, DarkRP.Config().Int( "yellDistance" ) ) );
		C( r, "me", "Describe what you do: /me waves.", cmd =>
		{
			if ( string.IsNullOrEmpty( cmd.ArgString ) ) return;
			RPNet.ChatLine( RPChat.Near( cmd.Caller, DarkRP.Config().Int( "meDistance" ) ), RPNet.Seg( RPChat.NameColor( cmd.Caller ), "* " + RP.Name( cmd.Caller ) + " " + cmd.ArgString ) );
		} );
		C( r, "pm", "Private message: /pm <player> <message>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t == null ) return;
			var msg = cmd.Rest( 1 );
			if ( string.IsNullOrEmpty( msg ) ) return;
			var line = RPNet.Join( RPNet.Seg( "#ff7fff", "(PM) " ), RPNet.Join( RPNet.Seg( RPChat.NameColor( cmd.Caller ), RP.Name( cmd.Caller ) + " → " + RP.Name( t ) ), RPNet.Seg( RPChat.White, ": " + msg ) ) );
			var to = new List<Connection>(); to.Add( t ); if ( t != cmd.Caller ) to.Add( cmd.Caller );
			RPNet.ChatLine( to, line );
		} );
		C( r, "g", "Group chat with your job group (police, gangsters...).", cmd =>
		{
			if ( string.IsNullOrEmpty( cmd.ArgString ) ) return;
			var to = new List<Connection>();
			foreach ( var gc in DarkRP.Registry().GroupChats )
			{
				if ( !InGroup( gc, cmd.Caller ) ) continue;
				foreach ( var rp in RP.All() ) { var c = RP.ConnOf( rp ); if ( InGroup( gc, c ) && !to.Contains( c ) ) to.Add( c ); }
			}
			if ( to.Count == 0 ) { cmd.Error( "Your job has no group chat." ); return; }
			RPNet.ChatLine( to, RPNet.Join( RPNet.Seg( "#ff3c3c", "(Group) " ), RPNet.Join( RPNet.Seg( RPChat.NameColor( cmd.Caller ), RP.Name( cmd.Caller ) ), RPNet.Seg( RPChat.White, ": " + cmd.ArgString ) ) ) );
		} );
		C( r, "advert", "Advertise something to everyone.", 2, cmd =>
		{
			if ( string.IsNullOrEmpty( cmd.ArgString ) ) return;
			var cost = DarkRP.Config().Int( "advertcost" );
			if ( cost > 0 && !RP.CanAfford( cmd.Caller, cost ) ) { cmd.Error( "An advert costs " + RPUtil.Money( cost ) + "." ); return; }
			if ( cost > 0 ) RP.AddMoney( cmd.Caller, -cost );
			RPNet.ChatLine( RPChat.Everyone(), RPNet.Join( RPNet.Seg( "#ffff00", "[Advert] " ), RPNet.Join( RPNet.Seg( RPChat.NameColor( cmd.Caller ), RP.Name( cmd.Caller ) ), RPNet.Seg( "#ffff96", ": " + cmd.ArgString ) ) ) );
		} );
		r.AddCommand( new RPCommandDef { Name = "ad", Description = "Same as /advert.", Delay = 2, Handler = cmd => RPChat.RunCommand( cmd.Caller, "/advert " + cmd.ArgString ) } );
		C( r, "broadcast", "Mayor: a message to the whole city.", cmd =>
		{
			if ( !RP.IsMayor( cmd.Caller ) ) { cmd.Error( "Only the mayor can broadcast." ); return; }
			RPNet.ChatLine( RPChat.Everyone(), RPNet.Join( RPNet.Seg( "#963c3c", "[Broadcast] " ), RPNet.Seg( "#ffc8c8", RP.Name( cmd.Caller ) + ": " + cmd.ArgString ) ) );
		} );
		C( r, "cr", "Call the police: /cr <what happened>.", 10, cmd =>
		{
			if ( string.IsNullOrEmpty( cmd.ArgString ) ) return;
			var to = new List<Connection>();
			foreach ( var rp in RP.All() ) { var c = RP.ConnOf( rp ); if ( RP.IsCP( c ) ) to.Add( c ); }
			if ( !to.Contains( cmd.Caller ) ) to.Add( cmd.Caller );
			RPNet.ChatLine( to, RPNet.Join( RPNet.Seg( "#3c78ff", "[Police request] " ), RPNet.Seg( RPChat.White, RP.Name( cmd.Caller ) + ": " + cmd.ArgString ) ) );
		} );

		// ---- identity ----
		C( r, "rpname", "Set your roleplay name.", 5, cmd => SetName( cmd.Caller, cmd.Caller, cmd.ArgString ) );
		r.AddCommand( new RPCommandDef { Name = "name", Description = "Same as /rpname.", Delay = 5, Handler = cmd => SetName( cmd.Caller, cmd.Caller, cmd.ArgString ) } );
		r.AddCommand( new RPCommandDef { Name = "nick", Description = "Same as /rpname.", Delay = 5, Handler = cmd => SetName( cmd.Caller, cmd.Caller, cmd.ArgString ) } );
		C( r, "job", "Set a custom job title: /job Taxi driver.", 3, cmd =>
		{
			var rp = RP.Of( cmd.Caller );
			var title = cmd.ArgString.Trim();
			if ( title.Length > 25 ) title = title.Substring( 0, 25 );
			var h = DarkRP.RunHook( new RPHook { Name = "canChangeJobTitle", Player = cmd.Caller, Text = title } );
			if ( !h.Allowed ) { cmd.Error( string.IsNullOrEmpty( h.Reason ) ? "You can't change your job title." : h.Reason ); return; }
			rp.JobTitle = title;
			DarkRP.NotifyAll( 0, 4, RP.Name( cmd.Caller ) + " changed their job title to " + (title == "" ? RP.JobOf( rp ).Name : title) + "." );
		} );

		// ---- money ----
		C( r, "give", "Give money to the player you look at: /give <amount>.", 1, cmd => RPMoney.Give( cmd.Caller, Amount( cmd, 0 ) ) );
		C( r, "dropmoney", "Drop money: /dropmoney <amount>.", 1, cmd => RPMoney.Drop( cmd.Caller, Amount( cmd, 0 ) ) );
		r.AddCommand( new RPCommandDef { Name = "moneydrop", Description = "Same as /dropmoney.", Delay = 1, Handler = cmd => RPMoney.Drop( cmd.Caller, Amount( cmd, 0 ) ) } );
		C( r, "cheque", "Write a cheque: /cheque <player> <amount>.", 2, cmd => RPMoney.Cheque( cmd.Caller, Target( cmd, 0 ), Amount( cmd, 1 ) ) );
		r.AddCommand( new RPCommandDef { Name = "check", Description = "Same as /cheque.", Delay = 2, Handler = cmd => RPMoney.Cheque( cmd.Caller, Target( cmd, 0 ), Amount( cmd, 1 ) ) } );

		// ---- items ----
		C( r, "drop", "Drop the weapon you're holding.", 1, cmd => RPEntities.DropActiveWeapon( cmd.Caller ) );
		r.AddCommand( new RPCommandDef { Name = "dropweapon", Description = "Same as /drop.", Delay = 1, Handler = cmd => RPEntities.DropActiveWeapon( cmd.Caller ) } );
		C( r, "buy", "Buy a single weapon: /buy <name>.", cmd => RPShop.Buy( cmd.Caller, "weapon", cmd.ArgString, 1 ) );
		C( r, "buyshipment", "Buy a shipment: /buyshipment <name>.", cmd => RPShop.Buy( cmd.Caller, "shipment", cmd.ArgString, 1 ) );
		C( r, "buyammo", "Buy ammo: /buyammo <pistol|smg1|buckshot|sniper>.", cmd => RPShop.Buy( cmd.Caller, "ammo", cmd.ArgString, 1 ) );
		C( r, "buyfood", "Buy food (hunger module): /buyfood <name>.", cmd => RPShop.Buy( cmd.Caller, "food", cmd.ArgString, 1 ) );
		C( r, "price", "Set the price of the gun lab, drug lab or microwave you look at.", cmd => RPEntities.SetPrice( cmd.Caller, Amount( cmd, 0 ) ) );
		r.AddCommand( new RPCommandDef { Name = "setprice", Description = "Same as /price.", Handler = cmd => RPEntities.SetPrice( cmd.Caller, Amount( cmd, 0 ) ) } );
		C( r, "write", "Write a letter.", 2, cmd => RPEntities.Letter( cmd.Caller, cmd.ArgString, false ) );
		C( r, "type", "Type a letter.", 2, cmd => RPEntities.Letter( cmd.Caller, cmd.ArgString, true ) );
		C( r, "removeletters", "Remove your letters.", cmd => RPEntities.RemoveOwned( cmd.Caller, "letter" ) );

		// ---- doors ----
		C( r, "sellalldoors", "Sell all your doors.", 2, cmd => RPDoors.SellAll( cmd.Caller ) );
		r.AddCommand( new RPCommandDef { Name = "unownalldoors", Description = "Same as /sellalldoors.", Delay = 2, Handler = cmd => RPDoors.SellAll( cmd.Caller ) } );
		C( r, "title", "Set the title of the door you look at.", cmd => DoorCmd( cmd, "title", cmd.ArgString ) );
		C( r, "addowner", "Add a co-owner to the door you look at.", cmd => DoorCmd( cmd, "addowner", cmd.ArgString ) );
		C( r, "removeowner", "Remove a co-owner from the door you look at.", cmd => DoorCmd( cmd, "removeowner", cmd.ArgString ) );

		// ---- police ----
		C( r, "wanted", "Police: make a player wanted: /wanted <player> <reason>.", cmd =>
		{
			if ( !RP.IsCP( cmd.Caller ) && !RP.IsAdmin( cmd.Caller ) ) { cmd.Error( "Only the police can do that." ); return; }
			var t = Target( cmd, 0 );
			if ( t == null ) return;
			if ( RP.Of( t ).Arrested ) { cmd.Error( RP.Name( t ) + " is arrested." ); return; }
			RPPolice.SetWanted( t, cmd.Caller, cmd.Rest( 1 ) );
		} );
		C( r, "unwanted", "Police: remove a wanted status.", cmd =>
		{
			if ( !RP.IsCP( cmd.Caller ) && !RP.IsAdmin( cmd.Caller ) ) { cmd.Error( "Only the police can do that." ); return; }
			var t = Target( cmd, 0 );
			if ( t != null ) RPPolice.SetUnwanted( t, cmd.Caller, false );
		} );
		C( r, "warrant", "Police: request a search warrant: /warrant <player> <reason>.", cmd =>
		{
			if ( !RP.IsCP( cmd.Caller ) && !RP.IsAdmin( cmd.Caller ) ) { cmd.Error( "Only the police can do that." ); return; }
			var t = Target( cmd, 0 );
			if ( t != null ) RPPolice.RequestWarrant( cmd.Caller, t, cmd.Rest( 1 ) );
		} );
		C( r, "unwarrant", "Police: remove a warrant.", cmd =>
		{
			if ( !RP.IsCP( cmd.Caller ) && !RP.IsAdmin( cmd.Caller ) ) { cmd.Error( "Only the police can do that." ); return; }
			var t = Target( cmd, 0 );
			if ( t != null ) RPPolice.RemoveWarrant( t, cmd.Caller );
		} );
		C( r, "jailpos", "Chief/mayor/admin: reset the jail positions to where you stand.", cmd => JailPos( cmd, true ) );
		C( r, "addjailpos", "Chief/mayor/admin: add a jail position where you stand.", cmd => JailPos( cmd, false ) );
		C( r, "givelicense", "Mayor/chief: give the player you look at a gun license.", cmd =>
		{
			var j = RP.Job( cmd.Caller );
			if ( !(j != null && (j.Mayor || j.Chief)) && !RP.IsAdmin( cmd.Caller ) ) { cmd.Error( "Only the mayor or chief can give licenses." ); return; }
			var t = RP.AimTarget( cmd.Caller, 200 );
			if ( t == null ) { cmd.Error( "Look at a player." ); return; }
			RP.Of( t ).License = true;
			DarkRP.Notify( t, 0, 4, RP.Name( cmd.Caller ) + " gave you a gun license." );
			cmd.Reply( "You gave " + RP.Name( t ) + " a gun license." );
		} );
		C( r, "requestlicense", "Ask the mayor or police for a gun license.", 30, cmd =>
		{
			foreach ( var rp in RP.All() )
			{
				var j = RP.JobOf( rp );
				if ( j != null && (j.Mayor || j.Chief || j.Police) ) DarkRP.Notify( RP.ConnOf( rp ), 3, 6, RP.Name( cmd.Caller ) + " is requesting a gun license. Look at them and type /givelicense." );
			}
		} );

		// ---- mayor ----
		C( r, "addlaw", "Mayor: add a law.", cmd => RPMayor.AddLaw( W(), cmd.Caller, cmd.ArgString ) );
		C( r, "removelaw", "Mayor: remove a law by number.", cmd => RPMayor.RemoveLaw( W(), cmd.Caller, (int)Amount( cmd, 0 ) ) );
		C( r, "resetlaws", "Mayor: reset the laws.", cmd => { if ( RPMayor.CanEditLaws( cmd.Caller ) ) RPMayor.ResetLaws( W() ); } );
		C( r, "placelaws", "Mayor: place a board showing the laws.", cmd => RPEntities.PlaceLawBoard( cmd.Caller ) );
		C( r, "lockdown", "Mayor: start a lockdown.", cmd => RPMayor.StartLockdown( W(), cmd.Caller ) );
		C( r, "unlockdown", "Mayor: end the lockdown.", cmd => RPMayor.EndLockdown( W(), cmd.Caller ) );
		C( r, "lottery", "Mayor: start a lottery: /lottery <ticket price>.", cmd => RPMayor.StartLottery( W(), cmd.Caller, (int)Amount( cmd, 0 ) ) );
		C( r, "agenda", "Set your group's agenda.", cmd => RPMayor.SetAgenda( W(), cmd.Caller, cmd.ArgString ) );
		r.AddCommand( new RPCommandDef { Name = "addagenda", Description = "Same as /agenda.", Handler = cmd => RPMayor.SetAgenda( W(), cmd.Caller, cmd.ArgString ) } );

		// ---- hits ----
		C( r, "hit", "Look at a hitman and order a hit: /hit <target> <price>.", 5, cmd => RPHits.Request( cmd.Caller, Target( cmd, 0 ), Amount( cmd, 1 ) ) );

		// ---- votes ----
		C( r, "demote", "Start a vote to demote a player: /demote <player> <reason>.", cmd => RPVotes.StartDemote( cmd.Caller, Target( cmd, 0 ), cmd.Rest( 1 ) ) );
		C( r, "vote", "Vote on the newest vote: /vote yes|no.", cmd => RPVotes.CastLatest( cmd.Caller, cmd.Arg( 0 ).StartsWith( "y" ) ) );

		// ---- admin ----
		A( r, "setmoney", "Admin: /setmoney <player> <amount>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t == null ) return;
			var rp = RP.Of( t );
			RP.AddMoney( t, Amount( cmd, 1 ) - rp.Money );
			cmd.Reply( RP.Name( t ) + " now has " + RPUtil.Money( rp.Money ) + "." );
			RPLog.Write( RP.Name( cmd.Caller ) + " set " + RP.Name( t ) + "'s money to " + RPUtil.Money( rp.Money ) );
		} );
		A( r, "addmoney", "Admin: /addmoney <player> <amount>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t == null ) return;
			RP.AddMoney( t, Amount( cmd, 1 ) );
			cmd.Reply( RP.Name( t ) + " now has " + RPUtil.Money( RP.Money( t ) ) + "." );
			RPLog.Write( RP.Name( cmd.Caller ) + " gave " + RP.Name( t ) + " " + RPUtil.Money( Amount( cmd, 1 ) ) );
		} );
		A( r, "setjob", "Admin: /setjob <player> <job command>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t == null ) return;
			var job = DarkRP.Registry().Job( cmd.Arg( 1 ) );
			if ( job == null ) { cmd.Error( "Unknown job: " + cmd.Arg( 1 ) ); return; }
			RPJobs.ChangeTeam( t, job, true, false );
		} );
		A( r, "setname", "Admin: /setname <player> <rp name>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t != null ) SetName( cmd.Caller, t, cmd.Rest( 1 ) );
		} );
		A( r, "arrest", "Admin: /arrest <player> [seconds].", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t != null ) RPPolice.Arrest( t, Amount( cmd, 1 ), cmd.Caller );
		} );
		A( r, "unarrest", "Admin: /unarrest <player>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t != null ) RPPolice.Unarrest( t, cmd.Caller );
		} );
		A( r, "teamban", "Admin: ban a player from a job: /teamban <player> <job> [seconds].", cmd =>
		{
			var t = Target( cmd, 0 );
			var job = DarkRP.Registry().Job( cmd.Arg( 1 ) );
			if ( t == null || job == null ) { if ( job == null ) cmd.Error( "Unknown job." ); return; }
			var secs = Amount( cmd, 2 );
			RP.Of( t ).DemotedUntil[job.Command] = Time.Now + (secs > 0 ? secs : 999999);
			if ( RP.Of( t ).JobCommand == job.Command ) RPJobs.ChangeTeam( t, DarkRP.Registry().DefaultJob(), true, false );
			cmd.Reply( RP.Name( t ) + " is banned from " + job.Name + "." );
		} );
		A( r, "teamunban", "Admin: /teamunban <player> <job>.", cmd =>
		{
			var t = Target( cmd, 0 );
			if ( t == null ) return;
			RP.Of( t ).DemotedUntil.Remove( cmd.Arg( 1 ).ToLowerInvariant() );
			cmd.Reply( "Done." );
		} );
		A( r, "setspawn", "Admin: reset a job's spawn points to here: /setspawn <job>.", cmd => SpawnPos( cmd, true ) );
		A( r, "addspawn", "Admin: add a spawn point for a job: /addspawn <job>.", cmd => SpawnPos( cmd, false ) );
		A( r, "removespawn", "Admin: remove a job's spawn points: /removespawn <job>.", cmd =>
		{
			RPMap.Data().Spawns.Remove( cmd.Arg( 0 ).ToLowerInvariant() );
			RPMap.Save( W() );
			cmd.Reply( "Spawn points removed." );
		} );
		A( r, "rpconfig", "Admin: change a setting: /rpconfig <key> <value> (no value: show it).", cmd => RPAdmin.SetConfig( cmd.Caller, cmd.Arg( 0 ), cmd.Rest( 1 ) ) );
		A( r, "spawndoor", "Admin: spawn an ownable door where you look.", cmd => RPEntities.SpawnDoor( cmd.Caller ) );
		A( r, "rpsave", "Admin: save all players and map data now.", cmd => { RPPlayers.SaveAll(); RPMap.Save( W() ); cmd.Reply( "Saved." ); } );
	}

	static bool InGroup( RPGroupChat gc, Connection c )
	{
		if ( c == null ) return false;
		if ( gc.Check != null ) { try { return gc.Check( c ); } catch { return false; } }
		var rp = RP.Of( c );
		return rp != null && RPUtil.ContainsIgnoreCase( gc.Jobs, rp.JobCommand );
	}

	public static void SetName( Connection actor, Connection target, string name )
	{
		name = (name ?? "").Trim();
		var admin = RP.IsAdmin( actor );
		if ( !DarkRP.Config().Bool( "allowrpnames" ) && !admin ) { DarkRP.Notify( actor, 1, 4, "RP names are disabled." ); return; }
		if ( name.Length < 2 || name.Length > 30 ) { DarkRP.Notify( actor, 1, 4, "The name must be 2-30 characters long." ); return; }
		foreach ( var ch in name ) if ( char.IsControl( ch ) ) { DarkRP.Notify( actor, 1, 4, "Invalid name." ); return; }
		foreach ( var rp in RP.All() )
			if ( RP.ConnOf( rp ) != target && string.Equals( rp.RPName, name, StringComparison.OrdinalIgnoreCase ) ) { DarkRP.Notify( actor, 1, 4, "That name is taken." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "CanChangeRPName", Player = target, Text = name } );
		if ( !h.Allowed && !admin ) { DarkRP.Notify( actor, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't use that name." : h.Reason ); return; }
		var old = RP.Name( target );
		var trp = RP.Of( target );
		trp.RPName = name;
		trp.Dirty = true;
		DarkRP.NotifyAll( 0, 4, old + " changed their RP name to " + name + "." );
		DarkRP.RunHook( new RPHook { Name = "onPlayerChangedName", Player = target, Text = name, Reason = old } );
	}

	static void DoorCmd( RPCmd cmd, string action, string arg )
	{
		var ply = RP.Pawn( cmd.Caller );
		var door = RPDoors.DoorOf( RP.AimObject( ply, 200 ) );
		if ( door == null ) { cmd.Error( "Look at a door." ); return; }
		RPDoors.Action( cmd.Caller, RPDoors.IdOf( door ), action, arg );
	}

	static void JailPos( RPCmd cmd, bool reset )
	{
		var j = RP.Job( cmd.Caller );
		if ( !(j != null && (j.Chief || j.Mayor)) && !RP.IsAdmin( cmd.Caller ) ) { cmd.Error( "Only the chief, the mayor or admins can set jail positions." ); return; }
		var ply = RP.Pawn( cmd.Caller );
		if ( ply == null ) return;
		if ( reset ) RPMap.Data().JailPositions.Clear();
		RPMap.Data().JailPositions.Add( ply.WorldPosition );
		RPMap.Save( W() );
		cmd.Reply( reset ? "Jail position set." : "Jail position added (" + RPMap.Data().JailPositions.Count + ")." );
	}

	static void SpawnPos( RPCmd cmd, bool reset )
	{
		var job = DarkRP.Registry().Job( cmd.Arg( 0 ) );
		if ( job == null ) { cmd.Error( "Unknown job: " + cmd.Arg( 0 ) ); return; }
		var ply = RP.Pawn( cmd.Caller );
		if ( ply == null ) return;
		if ( reset || !RPMap.Data().Spawns.ContainsKey( job.Command ) ) RPMap.Data().Spawns[job.Command] = new List<Vector3>();
		RPMap.Data().Spawns[job.Command].Add( ply.WorldPosition );
		RPMap.Save( W() );
		cmd.Reply( "Spawn point for " + job.Name + " " + (reset ? "set." : "added.") );
	}
}
