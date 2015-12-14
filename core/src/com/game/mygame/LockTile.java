package com.game.mygame;

public class LockTile extends Tile {

	private TileColor lockColor;
	private boolean locked;

	public LockTile(TileColor lockColor) {
		super();
		this.lockColor = lockColor;
		this.locked = true;
	}

	@Override
	public void interact(Player player) {
		if (player.getKey() == lockColor) {
			locked = false;
			player.removeKey();
			player.fulfillRequirement(lockColor);
		}
	}

	public void reset() {
		locked = true;
	}

	public TileColor getLockColor() {
		return lockColor;
	}

}
