// DarkRP for s&box - the mayor (laws, lockdown, lottery, broadcasts), agendas and votes.
// Ported from DarkRP's modules/police (laws, lockdown), voting and base/sv_gamemode_functions (MIT, Falco Peijnenburg).

public static class RPMayor
{
	public static bool CanEditLaws( Connection c ) => RP.IsMayor( c ) || RP.IsAdmin( c );

	public static void AddLaw( RPWorld w, Connection c, string law )
	{
		if ( !CanEditLaws( c ) ) { DarkRP.Notify( c, 1, 4, "Only the mayor can change the laws." ); return; }
		law = (law ?? "").Trim();
		if ( law.Length < 3 ) { DarkRP.Notify( c, 1, 4, "The law is too short." ); return; }
		if ( law.Length > 160 ) law = law.Substring( 0, 160 );
		if ( w.Laws.Count >= DarkRP.Config().Int( "maxlaws" ) ) { DarkRP.Notify( c, 1, 4, "There are too many laws." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canEditLaws", Player = c, Text = law, Data = "add" } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't add laws." : h.Reason ); return; }
		w.Laws.Add( law );
		DarkRP.NotifyAll( 0, 4, "A new law was added: " + law );
		DarkRP.RunHook( new RPHook { Name = "addLaw", Player = c, Text = law, Amount = w.Laws.Count } );
	}

	public static void RemoveLaw( RPWorld w, Connection c, int number )
	{
		if ( !CanEditLaws( c ) ) { DarkRP.Notify( c, 1, 4, "Only the mayor can change the laws." ); return; }
		int index = number - 1;
		if ( index < 0 || index >= w.Laws.Count ) { DarkRP.Notify( c, 1, 4, "There is no law " + number + "." ); return; }
		var law = w.Laws[index];
		var h = DarkRP.RunHook( new RPHook { Name = "canEditLaws", Player = c, Text = law, Data = "remove" } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't remove laws." : h.Reason ); return; }
		w.Laws.RemoveAt( index );
		DarkRP.NotifyAll( 0, 4, "Law " + number + " was removed: " + law );
		DarkRP.RunHook( new RPHook { Name = "removeLaw", Player = c, Text = law, Amount = number } );
	}

	public static void ResetLaws( RPWorld w )
	{
		w.Laws.Clear();
		foreach ( var law in DarkRP.Registry().DefaultLaws ) w.Laws.Add( law );
		DarkRP.RunHook( new RPHook { Name = "resetLaws" } );
	}

	public static void StartLockdown( RPWorld w, Connection c )
	{
		var cfg = DarkRP.Config();
		if ( c != null && !RP.IsMayor( c ) && !RP.IsAdmin( c ) ) { DarkRP.Notify( c, 1, 4, "Only the mayor can start a lockdown." ); return; }
		if ( !cfg.Bool( "lockdown" ) ) { DarkRP.Notify( c, 1, 4, "Lockdowns are disabled." ); return; }
		if ( w.Lockdown ) { DarkRP.Notify( c, 1, 4, "There already is a lockdown." ); return; }
		if ( Time.Now < w.LockdownCooldownUntil && !RP.IsAdmin( c ) ) { DarkRP.Notify( c, 1, 4, "Wait " + (int)(w.LockdownCooldownUntil - Time.Now) + "s before the next lockdown." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canStartLockdown", Player = c } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't start a lockdown." : h.Reason ); return; }
		w.Lockdown = true;
		DarkRP.NotifyAll( 1, 8, "The mayor has initiated a lockdown! Please return to your homes." );
		RPNet.Sound( "sounds/ui/ui.notify.sound", Vector3.Zero );
		DarkRP.RunHook( new RPHook { Name = "lockdownStarted", Player = c } );
	}

	public static void EndLockdown( RPWorld w, Connection c )
	{
		if ( c != null && !RP.IsMayor( c ) && !RP.IsAdmin( c ) ) { DarkRP.Notify( c, 1, 4, "Only the mayor can end a lockdown." ); return; }
		if ( !w.Lockdown ) { if ( c != null ) DarkRP.Notify( c, 1, 4, "There is no lockdown." ); return; }
		w.Lockdown = false;
		w.LockdownCooldownUntil = Time.Now + DarkRP.Config().Int( "lockdowndelay" );
		DarkRP.NotifyAll( 0, 5, "The lockdown has ended." );
		DarkRP.RunHook( new RPHook { Name = "lockdownEnded", Player = c } );
	}

	// ---- lottery ----

	public static void StartLottery( RPWorld w, Connection c, int price )
	{
		var cfg = DarkRP.Config();
		if ( !RP.IsMayor( c ) ) { DarkRP.Notify( c, 1, 4, "Only the mayor can start a lottery." ); return; }
		if ( !cfg.Bool( "lottery" ) ) { DarkRP.Notify( c, 1, 4, "Lotteries are disabled." ); return; }
		if ( !string.IsNullOrEmpty( w.Lottery ) ) { DarkRP.Notify( c, 1, 4, "There already is a lottery." ); return; }
		price = Math.Clamp( price, cfg.Int( "minlotterycost" ), cfg.Int( "maxlotterycost" ) );
		var participants = new List<Connection>();
		RPVotes.Ask( null, RP.Name( c ) + " started a lottery! Ticket: " + RPUtil.Money( price ) + ". Do you want to join?", "lottery", c, cast =>
		{
			var voter = cast.Player;
			if ( !cast.Allowed ) return;
			if ( !RP.CanAfford( voter, price ) ) { DarkRP.Notify( voter, 1, 4, "You can't afford a ticket." ); return; }
			RP.AddMoney( voter, -price );
			participants.Add( voter );
			UpdateLotteryText( w, price, participants.Count );
		}, passed =>
		{
			w.Lottery = "";
			if ( participants.Count == 0 ) { DarkRP.NotifyAll( 0, 5, "Nobody entered the lottery." ); return; }
			var winner = participants[Game.Random.Int( 0, participants.Count - 1 )];
			var pot = (long)price * participants.Count;
			RP.AddMoney( winner, pot );
			DarkRP.NotifyAll( 0, 8, RP.Name( winner ) + " won the lottery: " + RPUtil.Money( pot ) + "!" );
		} );
		UpdateLotteryText( w, price, 0 );
	}

	static void UpdateLotteryText( RPWorld w, int price, int players ) => w.Lottery = price + "|" + ((long)price * players) + "|" + players;

	public static void Tick( RPWorld w ) { }

	// ---- agendas ----

	public static RPAgendaDef AgendaFor( Connection c, bool manager )
	{
		var rp = RP.Of( c );
		if ( rp == null ) return null;
		foreach ( var a in DarkRP.Registry().Agendas )
		{
			if ( RPUtil.ContainsIgnoreCase( a.Managers, rp.JobCommand ) ) return a;
			if ( !manager && RPUtil.ContainsIgnoreCase( a.Listeners, rp.JobCommand ) ) return a;
		}
		return null;
	}

	public static void SetAgenda( RPWorld w, Connection c, string text )
	{
		var a = AgendaFor( c, true );
		if ( a == null ) { DarkRP.Notify( c, 1, 4, "You can't set an agenda." ); return; }
		text = (text ?? "").Trim();
		if ( text.Length > 400 ) text = text.Substring( 0, 400 );
		w.AgendaTexts[a.Title] = text;
		foreach ( var rp in RP.All() )
		{
			var j = rp.JobCommand;
			if ( RPUtil.ContainsIgnoreCase( a.Managers, j ) || RPUtil.ContainsIgnoreCase( a.Listeners, j ) )
				DarkRP.Notify( RP.ConnOf( rp ), 0, 5, RP.Name( c ) + " updated the agenda." );
		}
	}
}

/// <summary>A running vote or question.</summary>
public sealed class RPVote
{
	public int Id;
	public string Question = "";
	public string Kind = "";
	/// <summary>Only these may vote (null: everyone but the target/starter).</summary>
	public List<Connection> Voters;
	public Connection Starter;
	public Connection Target;
	public readonly List<Connection> Voted = new List<Connection>();
	public int Yes;
	public int No;
	public float EndsAt;
	/// <summary>Called once per vote cast (lottery tickets).</summary>
	public Action<RPHook> OnCast;
	/// <summary>Called with the result (yes > no).</summary>
	public Action<bool> OnEnd;
}

public sealed class RPVotesHolder
{
	public readonly List<RPVote> Votes = new List<RPVote>();
	public int NextId = 1;
}

public static class RPVotes
{
	static readonly RPVotesHolder H = new RPVotesHolder();

	public static void Reset() => H.Votes.Clear();

	static bool CanVote( RPVote v, Connection c )
	{
		if ( c == null || v.Voted.Contains( c ) ) return false;
		if ( v.Voters != null ) return v.Voters.Contains( c );
		return c != v.Target;
	}

	/// <summary>Starts a vote everyone (but the target) can answer. Returns its id.</summary>
	public static int Ask( List<Connection> voters, string question, string kind, Connection target, Action<RPHook> onCast, Action<bool> onEnd )
	{
		var v = new RPVote
		{
			Id = H.NextId++, Question = question, Kind = kind, Voters = voters, Target = target, OnCast = onCast, OnEnd = onEnd,
			EndsAt = Time.Now + Math.Max( 5, DarkRP.Config().Int( "votetime" ) ),
		};
		H.Votes.Add( v );
		Sync();
		DarkRP.RunHook( new RPHook { Name = "onVoteStarted", Player = target, Text = question, Reason = kind } );
		return v.Id;
	}

	/// <summary>A yes/no question for one player (warrants for the mayor).</summary>
	public static void Question( Connection who, string question, string kind, Action<bool> onAnswer )
	{
		var list = new List<Connection>();
		list.Add( who );
		Ask( list, question, kind, null, null, onAnswer );
	}

	public static void StartJobVote( Connection c, RPJob job )
	{
		foreach ( var v in H.Votes ) if ( v.Kind == "job" && v.Target == c ) { DarkRP.Notify( c, 1, 4, "You already have a vote running." ); return; }
		var rp = RP.Of( c );
		var left = RP.CooldownLeft( rp, "jobvote", 20 );
		if ( left > 0 ) { DarkRP.Notify( c, 1, 4, "Wait " + (int)left + "s before starting another vote." ); return; }
		var others = RP.All().Count( p => RP.ConnOf( p ) != c );
		if ( others < Math.Max( 1, DarkRP.Config().Int( "voteminplayers" ) - 1 ) )
		{
			RPJobs.ChangeTeam( c, job, false, true );
			return;
		}
		var h = DarkRP.RunHook( new RPHook { Name = "canStartVote", Player = c, Job = job } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't start a vote." : h.Reason ); return; }
		DarkRP.NotifyAll( 0, 4, RP.Name( c ) + " wants to be " + job.Name + "." );
		Ask( null, RP.Name( c ) + " wants to be " + job.Name + ".", "job", c, null, yes =>
		{
			if ( yes && RP.Of( c ) != null )
			{
				var reason = RPJobs.ChangeTeamBlocker( c, job, false );
				if ( reason == null ) RPJobs.ChangeTeam( c, job, false, true );
				else DarkRP.Notify( c, 1, 4, reason );
			}
			else DarkRP.NotifyAll( 1, 4, RP.Name( c ) + " has not been made " + job.Name + "." );
		} );
	}

	public static void StartDemote( Connection c, Connection target, string reason )
	{
		if ( target == null ) { DarkRP.Notify( c, 1, 4, "Player not found." ); return; }
		if ( target == c ) { DarkRP.Notify( c, 1, 4, "You can't demote yourself." ); return; }
		var job = RP.Job( target );
		if ( job == null || !job.CanDemote || job == DarkRP.Registry().DefaultJob() ) { DarkRP.Notify( c, 1, 4, RP.Name( target ) + " can't be demoted." ); return; }
		if ( string.IsNullOrWhiteSpace( reason ) || reason.Length < 3 ) { DarkRP.Notify( c, 1, 4, "Give a reason for the demotion." ); return; }
		var rp = RP.Of( c );
		var left = RP.CooldownLeft( rp, "demote", 60 );
		if ( left > 0 ) { DarkRP.Notify( c, 1, 4, "Wait " + (int)left + "s before demoting again." ); return; }
		var h = DarkRP.RunHook( new RPHook { Name = "canDemote", Player = c, Target = target, Reason = reason } );
		if ( !h.Allowed ) { DarkRP.Notify( c, 1, 4, string.IsNullOrEmpty( h.Reason ) ? "You can't demote this player." : h.Reason ); return; }
		DarkRP.NotifyAll( 0, 5, RP.Name( c ) + " started a vote to demote " + RP.Name( target ) + ". Reason: " + reason );
		Ask( null, "Demote " + RP.Name( target ) + "? Reason: " + reason, "demote", target, null, yes =>
		{
			if ( yes ) RPJobs.Demote( target, reason );
			else DarkRP.NotifyAll( 1, 4, RP.Name( target ) + " has not been demoted." );
		} );
	}

	public static void Cast( Connection c, int id, bool yes )
	{
		foreach ( var v in H.Votes )
		{
			if ( v.Id != id ) continue;
			if ( !CanVote( v, c ) ) return;
			v.Voted.Add( c );
			if ( yes ) v.Yes++; else v.No++;
			if ( v.OnCast != null )
			{
				try { v.OnCast( new RPHook { Name = "voteCast", Player = c, Allowed = yes } ); } catch ( Exception e ) { Log.Warning( "DarkRP vote: " + e.Message ); }
			}
			if ( v.Voters != null && v.Voted.Count >= v.Voters.Count ) v.EndsAt = 0;
			Sync();
			return;
		}
	}

	/// <summary>/vote yes|no: answers the newest vote this player can still vote on.</summary>
	public static void CastLatest( Connection c, bool yes )
	{
		for ( int i = H.Votes.Count - 1; i >= 0; i-- )
		{
			if ( !CanVote( H.Votes[i], c ) ) continue;
			Cast( c, H.Votes[i].Id, yes );
			return;
		}
		DarkRP.Notify( c, 1, 3, "There is nothing to vote on." );
	}

	public static void PlayerLeft( Connection c )
	{
		foreach ( var v in H.Votes ) if ( v.Target == c ) v.EndsAt = 0;
	}

	public static void Tick( RPWorld w )
	{
		bool changed = false;
		for ( int i = H.Votes.Count - 1; i >= 0; i-- )
		{
			var v = H.Votes[i];
			if ( Time.Now < v.EndsAt ) continue;
			H.Votes.RemoveAt( i );
			changed = true;
			bool passed = v.Yes > v.No;
			if ( v.Kind == "lottery" ) passed = true;
			DarkRP.RunHook( new RPHook { Name = "onVoteEnded", Player = v.Target, Text = v.Question, Reason = v.Kind, Allowed = passed, Amount = v.Yes } );
			if ( v.OnEnd != null )
			{
				try { v.OnEnd( passed ); } catch ( Exception e ) { Log.Warning( "DarkRP vote: " + e.Message ); }
			}
		}
		if ( changed ) Sync();
		else if ( H.Votes.Count > 0 ) Sync();
	}

	/// <summary>Writes the running votes for clients: id, question, yes, no, seconds left, who may vote.</summary>
	static void Sync()
	{
		var w = RPWorld.Current();
		if ( w == null ) return;
		var wr = new RPJsonWriter { Pretty = false };
		wr.BeginArray();
		foreach ( var v in H.Votes )
		{
			wr.BeginObject();
			wr.Num( "id", v.Id ).Str( "q", v.Question ).Str( "kind", v.Kind ).Num( "yes", v.Yes ).Num( "no", v.No ).Num( "left", (long)Math.Max( 0, Math.Ceiling( v.EndsAt - Time.Now ) ) );
			var allowed = new List<string>();
			if ( v.Voters != null ) foreach ( var c in v.Voters ) allowed.Add( c.Id.ToString() );
			var voted = new List<string>();
			foreach ( var c in v.Voted ) voted.Add( c.Id.ToString() );
			wr.StrArray( "voters", v.Voters != null ? allowed.ToArray() : null );
			wr.StrArray( "voted", voted.ToArray() );
			wr.Str( "target", v.Target != null ? v.Target.Id.ToString() : "" );
			wr.EndObject();
		}
		wr.EndArray();
		w.VotesJson = wr.ToString();
	}
}
