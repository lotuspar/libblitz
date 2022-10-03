/*
 * part of the BonitoBlitz (w.i.p name) gamemode
 * library used across the board gamemode & minigames
 * - lotuspar, 2022 (github.com/lotuspar)
 * this is inspired by / based on:
 * - https://github.com/Facepunch/sbox-spire/blob/main/code/Gamemodes/BaseGamemode.cs
 *     * (actually, the whole Activity system is based on the above!)
 */
namespace libblitz;

using System;
using System.Collections.Generic;
using Sandbox;

public interface IActivity
{
	/// <summary>
	/// Subset of the global Player list
	/// </summary>
	public IList<Player> Players { get; }

	public Type PawnType { get; }
	public Type HudPanelType { get; }

	/// <summary>
	/// Called when all players are ready.
	/// </summary>
	public void Initialize();

	/// <summary>
	/// Called on every Game.Simulate
	/// "Called when simulating as part of a player's tick. Like if it's a pawn."
	/// </summary>
	/// <param name="cl">Client</param>
	public void Simulate( Client cl );

	/// <summary>
	/// Called on every Game.FrameSimulate
	/// "Called each frame clientside only on Pawn (and anything the pawn decides to call it on)"
	/// </summary>
	/// <param name="cl">Client</param>
	public void FrameSimulate( Client cl );

	/// <summary>
	/// Called when a player (libblitz.Player) has lost / gained a client
	/// </summary>
	public void PlayerChange();

	/// <summary>
	/// Called when the activity becomes the current global activity
	/// </summary>
	public void ActivityActive( ActivityResult previousActivityResult );

	/// <summary>
	/// Called when the activity is no longer the current global activity
	/// </summary>
	public ActivityResult ActivityDormant();
}

public abstract partial class Activity : Entity, IActivity
{
	public static Activity Current => Game.Current.Activity;

	[Net]
	public IList<Player> Players { get; private set; }

	public abstract Type PawnType { get; }
	public abstract Type HudPanelType { get; }

	public bool PreparedForActivityActive = true;
	public bool PreparedForInitialize = true;

	public Activity( IList<Player> players )
	{
		Transmit = TransmitType.Always;

		if ( players == null && Host.IsServer )
			Log.Info( "Using player list from game!" );

		Players = players ?? Game.Current.Players;
	}

	[ClientRpc]
	private void InternalClientActivityActive( ActivityResult previousActivityResult )
	{
		ActivityActive( previousActivityResult );
	}
	public virtual void ActivityActive( ActivityResult previousActivityResult )
	{
		if ( Host.IsServer )
		{
			if ( PawnType != null )
				foreach ( var player in Players )
					player.SetPawnByType( PawnType );
		}
	}

	// This 3 layered ActivityDormant is certainly annoying...
	public void CallClientActivityDormant()
	{
		foreach ( var player in Players )
		{
			if ( player.Client != null )
				InternalClientActivityDormant( To.Single( player.Client ) );
		}
	}
	[ClientRpc]
	private void InternalClientActivityDormant()
	{
		ActivityDormant();
	}
	public virtual ActivityResult ActivityDormant() { return null; }

	[ClientRpc]
	private void InternalClientInitialize() { Initialize(); }
	public virtual void Initialize() { }

	public virtual void PlayerChange()
	{
		AttemptStateUpdate();
	}

	public virtual void AttemptStateUpdate()
	{
		if ( Players.Count == 0 )
			return;

		bool ready = true;
		foreach ( var player in Players )
		{
			if ( player.Client == null )
				ready = false;
		}

		if ( ready )
		{
			if ( PreparedForInitialize )
			{
				PreparedForInitialize = false;
				Initialize();
				foreach ( var player in Players )
					InternalClientInitialize( To.Single( player.Client ) );
			}
			if ( PreparedForActivityActive )
			{
				PreparedForActivityActive = false;
				ActivityActive( Game.Current.PreviousActivityResult );
				foreach ( var player in Players )
					InternalClientActivityActive( To.Single( player.Client ), Game.Current.PreviousActivityResult );
			}
		}
	}
}