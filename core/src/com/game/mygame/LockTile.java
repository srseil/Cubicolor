package com.game.mygame;

public class LockTile extends Tile {

	private TileColor lockColor;

	public LockTile(TileColor lockColor) {
		super();
		this.lockColor = lockColor;
	}

	@Override
	public void interact(Player player) {
		// Unlock the tile if the player has the right key.
		if (player.getKey() == lockColor) {
			player.removeKey();
			player.fulfillRequirement(lockColor);
		}
	}

	public void reset() {}

	public TileColor getLockColor() {
		return lockColor;
	}

}

