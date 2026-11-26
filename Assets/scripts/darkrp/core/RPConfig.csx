// DarkRP for s&box - settings. Keys and defaults follow DarkRP's config/config.lua (MIT, Falco Peijnenburg).
// Server owners change them in darkrp/config.json (data folder), the admin tab of the menu, or /rpconfig.

/// <summary>All DarkRP settings. Read them with <c>DarkRP.Config().Int( "paydelay" )</c> etc.</summary>
public sealed class RPConfig
{
	public readonly Dictionary<string, RPSetting> Settings = new Dictionary<string, RPSetting>();
	public readonly List<string> Order = new List<string>();

	public RPConfig()
	{
		RPConfigDefaults.Fill( this );
	}

	public void Add( string key, string kind, string def, string group, string help, bool shared )
	{
		var s = new RPSetting { Key = key, Kind = kind, Default = def, Value = def, Group = group, Help = help, Shared = shared };
		if ( !Settings.ContainsKey( key ) ) Order.Add( key );
		Settings[key] = s;
	}

	public bool Has( string key ) => Settings.ContainsKey( key );

	public string Str( string key ) => Settings.ContainsKey( key ) ? Settings[key].Value : "";

	public bool Bool( string key )
	{
		var v = Str( key ).Trim().ToLowerInvariant();
		return v == "true" || v == "1" || v == "yes" || v == "on";
	}

	public int Int( string key )
	{
		var v = Str( key );
		return (int)RPUtil.ParseFloat( v, 0f );
	}

	public float Float( string key )
	{
		return RPUtil.ParseFloat( Str( key ), 0f );
	}

	/// <summary>Sets a value (normalised to the setting's kind). Returns false for unknown keys or bad values.</summary>
	public bool Set( string key, string value )
	{
		if ( !Settings.ContainsKey( key ) ) return false;
		var s = Settings[key];
		value = (value ?? "").Trim();
		if ( s.Kind == "bool" )
		{
			var v = value.ToLowerInvariant();
			if ( v == "1" || v == "yes" || v == "on" ) v = "true";
			if ( v == "0" || v == "no" || v == "off" ) v = "false";
			if ( v != "true" && v != "false" ) return false;
			s.Value = v;
			return true;
		}
		if ( s.Kind == "int" )
		{
			if ( !RPUtil.IsNumber( value ) ) return false;
			s.Value = ((long)RPUtil.ParseFloat( value, 0 )).ToString();
			return true;
		}
		if ( s.Kind == "float" )
		{
			if ( !RPUtil.IsNumber( value ) ) return false;
			s.Value = RPUtil.FloatStr( RPUtil.ParseFloat( value, 0 ) );
			return true;
		}
		s.Value = value;
		return true;
	}
}

public static class RPConfigDefaults
{
	public static void Fill( RPConfig c )
	{
		// Money
		c.Add( "startingmoney", "int", "500", "Money", "Money new players start with.", false );
		c.Add( "normalsalary", "int", "45", "Money", "Base salary (default jobs scale from it).", false );
		c.Add( "paydelay", "int", "160", "Money", "Seconds between paydays.", true );
		c.Add( "currency", "string", "$", "Money", "Currency symbol.", true );
		c.Add( "currencyLeft", "bool", "true", "Money", "Put the currency symbol before the amount.", true );
		c.Add( "currencyThousandSeparator", "string", ",", "Money", "Thousands separator.", true );
		c.Add( "maxMoneyPiles", "int", "20", "Money", "Money piles a player may have dropped at once.", false );
		c.Add( "moneyRemoveTime", "int", "600", "Money", "Seconds before dropped money disappears (0 = never).", false );
		c.Add( "deathfee", "int", "30", "Money", "Money lost when you die (0 = none).", false );
		c.Add( "dropmoneyondeath", "bool", "false", "Money", "Drop the death fee as a money pile.", false );
		c.Add( "maxCheques", "int", "5", "Money", "Cheques a player may have written at once.", false );
		c.Add( "wallettax", "bool", "false", "Money", "Tax wallets periodically.", false );
		c.Add( "wallettaxmin", "float", "1", "Money", "Minimum wallet tax in percent.", false );
		c.Add( "wallettaxmax", "float", "5", "Money", "Maximum wallet tax in percent.", false );
		c.Add( "wallettaxtime", "int", "600", "Money", "Seconds between wallet taxes.", false );
		c.Add( "pricemin", "int", "50", "Money", "Minimum price players can set for things they sell.", false );
		c.Add( "pricecap", "int", "500", "Money", "Maximum price players can set for things they sell.", false );
		c.Add( "npckillpay", "int", "10", "Money", "Money for killing an NPC.", false );

		// Jobs
		c.Add( "allowjobswitch", "bool", "true", "Jobs", "Players may change jobs.", false );
		c.Add( "changejobtime", "int", "10", "Jobs", "Seconds between job changes.", false );
		c.Add( "norespawn", "bool", "true", "Jobs", "Changing job keeps you alive (false: you respawn in the new job).", false );
		c.Add( "restrictallteams", "bool", "false", "Jobs", "Only admins can change jobs.", false );
		c.Add( "adminBypassJobRestrictions", "bool", "true", "Jobs", "Admins ignore job limits and checks.", false );
		c.Add( "demotetime", "int", "120", "Jobs", "Seconds a demoted player can't take the job again.", false );
		c.Add( "defaultjob", "string", "citizen", "Jobs", "Command of the job new players start in.", true );
		c.Add( "enforceplayermodel", "bool", "true", "Jobs", "Players always wear their job's model/outfit.", false );
		c.Add( "revokeLicenseOnJobChange", "bool", "true", "Jobs", "Lose a granted gun license when you change jobs.", false );
		c.Add( "removeclassitems", "bool", "true", "Jobs", "Remove job entities (printers...) the player can't own in the new job.", false );

		// Players
		c.Add( "allowrpnames", "bool", "true", "Players", "Players may set an RP name with /rpname.", true );
		c.Add( "startinghealth", "int", "100", "Players", "Health on spawn.", false );
		c.Add( "respawntime", "int", "1", "Players", "Seconds before a dead player may respawn.", false );
		c.Add( "babygod", "bool", "true", "Players", "Spawn protection.", false );
		c.Add( "babygodtime", "int", "5", "Players", "Seconds of spawn protection.", false );
		c.Add( "showjob", "bool", "true", "Players", "Show job above heads.", true );
		c.Add( "showname", "bool", "true", "Players", "Show names above heads.", true );
		c.Add( "showhealth", "bool", "true", "Players", "Show health above heads.", true );
		c.Add( "dropweapondeath", "bool", "false", "Players", "Drop the active weapon on death.", false );
		c.Add( "restrictdrop", "bool", "false", "Players", "Only weapons bought from the menu can be dropped.", false );
		c.Add( "propspawning", "bool", "true", "Players", "Players may spawn props from the spawn menu.", false );
		c.Add( "restrictspawnmenu", "bool", "true", "Players", "Only admins may spawn weapons, entities and NPCs from the spawn menu.", false );

		// Chat
		c.Add( "chatCommandPrefix", "string", "/", "Chat", "Prefix of chat commands.", true );
		c.Add( "ooc", "bool", "true", "Chat", "Allow // and /ooc global chat.", false );
		c.Add( "alltalk", "bool", "false", "Chat", "Normal chat is global instead of local.", false );
		c.Add( "talkDistance", "int", "250", "Chat", "Range of normal chat.", false );
		c.Add( "whisperDistance", "int", "90", "Chat", "Range of /w.", false );
		c.Add( "yellDistance", "int", "550", "Chat", "Range of /y.", false );
		c.Add( "meDistance", "int", "250", "Chat", "Range of /me.", false );
		c.Add( "deadtalk", "bool", "true", "Chat", "Dead players can talk.", false );
		c.Add( "advertcost", "int", "0", "Chat", "Price of /advert (0 = free).", false );

		// Police
		c.Add( "jailtimer", "int", "120", "Police", "Seconds an arrest lasts.", true );
		c.Add( "wantedtime", "int", "120", "Police", "Seconds a wanted status lasts.", false );
		c.Add( "searchtime", "int", "30", "Police", "Seconds a warrant lasts.", false );
		c.Add( "needwantedforarrest", "bool", "false", "Police", "Players must be wanted to be arrested.", false );
		c.Add( "cpcanarrestcp", "bool", "true", "Police", "Police may arrest other police.", false );
		c.Add( "teletojail", "bool", "true", "Police", "Teleport arrested players to a jail position.", false );
		c.Add( "telefromjail", "bool", "true", "Police", "Teleport released players to a spawn point.", false );
		c.Add( "respawninjail", "bool", "true", "Police", "Arrested players respawn in jail.", false );
		c.Add( "doorwarrants", "bool", "true", "Police", "The battering ram needs a warrant against the door owner.", false );
		c.Add( "canforcedooropen", "bool", "true", "Police", "The battering ram can open unowned doors.", false );
		c.Add( "copscanunfreeze", "bool", "true", "Police", "The battering ram unfreezes props.", false );
		c.Add( "lockdown", "bool", "true", "Police", "The mayor can start lockdowns.", false );
		c.Add( "lockdowndelay", "int", "120", "Police", "Seconds between lockdowns.", false );
		c.Add( "license", "bool", "false", "Police", "Players need a gun license to carry guns.", true );
		c.Add( "stunstickdamage", "int", "1000", "Police", "Damage the stunstick does to props/entities.", false );

		// Mayor
		c.Add( "lottery", "bool", "true", "Mayor", "The mayor can start lotteries.", false );
		c.Add( "minlotterycost", "int", "30", "Mayor", "Minimum lottery ticket price.", false );
		c.Add( "maxlotterycost", "int", "250", "Mayor", "Maximum lottery ticket price.", false );
		c.Add( "shouldResetLaws", "bool", "false", "Mayor", "Reset the laws when the mayor dies or quits.", false );
		c.Add( "maxlaws", "int", "12", "Mayor", "Maximum number of laws.", false );

		// Doors
		c.Add( "doorcost", "int", "30", "Doors", "Price of a door.", true );
		c.Add( "maxdoors", "int", "20", "Doors", "Doors a player may own.", false );
		c.Add( "unlockdoorsonstart", "bool", "false", "Doors", "Unlock all doors when the map starts.", false );
		c.Add( "propertytax", "bool", "false", "Doors", "Pay tax per owned door each payday.", false );

		// Entities
		c.Add( "mprintamount", "int", "250", "Entities", "Money printed each time.", false );
		c.Add( "printerdelay", "int", "100", "Entities", "Seconds between printer prints (randomised ±40%).", false );
		c.Add( "printeroverheat", "bool", "true", "Entities", "Printers can overheat and explode.", false );
		c.Add( "printeroverheatchance", "int", "22", "Entities", "1 in N chance per print to overheat.", false );
		c.Add( "printerreward", "int", "950", "Entities", "Reward police get for destroying a printer.", false );
		c.Add( "shipmentspawntime", "int", "10", "Entities", "Seconds between weapons taken from a shipment.", false );
		c.Add( "entremovedelay", "int", "0", "Entities", "Seconds before a leaving player's entities are removed.", false );
		c.Add( "removeondisconnect", "bool", "true", "Entities", "Remove a player's entities when they leave.", false );
		c.Add( "pocketitems", "int", "10", "Entities", "Items that fit in the pocket.", false );
		c.Add( "maxletters", "int", "10", "Entities", "Letters a player may have at once.", false );
		c.Add( "letters", "bool", "true", "Entities", "Allow /write and /type letters.", false );
		c.Add( "gunlabguncost", "int", "200", "Entities", "Price of a gun from a gun lab.", false );
		c.Add( "gunlabweapon", "string", "weapon_p2282", "Entities", "Weapon a gun lab makes.", false );
		c.Add( "druglabdrugcost", "int", "100", "Entities", "Price of drugs from a drug lab.", false );
		c.Add( "microwavefoodcost", "int", "30", "Entities", "Price of food from a microwave.", false );
		c.Add( "maxlawboards", "int", "2", "Entities", "Law boards the mayor may have.", false );

		// Hunger
		c.Add( "hungermod", "bool", "false", "Hunger", "Enable the hunger module (energy, food, cooks).", true );
		c.Add( "hungerspeed", "float", "2", "Hunger", "Energy lost per 10 seconds.", false );
		c.Add( "starverate", "int", "3", "Hunger", "Damage per tick while starving.", false );

		// Voting
		c.Add( "votetime", "int", "20", "Voting", "Seconds a vote lasts.", false );
		c.Add( "voteminplayers", "int", "2", "Voting", "With fewer players online, votes pass automatically.", false );

		// Hits
		c.Add( "minHitPrice", "int", "200", "Hits", "Lowest price for a hit.", false );
		c.Add( "maxHitPrice", "int", "50000", "Hits", "Highest price for a hit.", false );

		// Admin
		c.Add( "adminsCanBypassMax", "bool", "true", "Admin", "Admins ignore entity limits.", false );
		c.Add( "logging", "bool", "true", "Admin", "Log DarkRP actions to the console and darkrp/logs.", false );
	}
}
