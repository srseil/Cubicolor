package com.game.mygame;

public class Player {

	private Level level;
	private TileColor key;
	private int x, y;

	public Player(Level level) {
		this.level = level;
		x = level.getStartColumn();
		y = level.getStartRow();
		key = TileColor.NONE;
	}

	/*
	 * Interact with the tile the player is standing on.
	 */
	private void interact() {
		level.getMatrix()[y][x].interact(this);
	}

	/*
	 * Move the player one unit along the directional axis specified.
	 */
	public void move(int x, int y) {
		int newX = this.x + x;
		int newY = this.y + y;

		if (newX >= level.getColumns() || newX < 0
				|| newY >= level.getRows() || newY < 0) {
			// Target location is out of bounds.
			return;
		} else if (level.getMatrix()[this.y][this.x] instanceof ExitTile) {
			// Player is standing on exit tile.
			return;
		}

		Tile toTile = level.getMatrix()[newY][newX];
		if (!toTile.isDead()
				&& !(toTile instanceof ExitTile && !level.requirementsMet())) {
			// Tile is neither dead nor exit tile with remaining requirements;
			// Move to tile.
			level.getMatrix()[this.y][this.x].setDead(true);
			this.x = newX;
			this.y = newY;
			interact();
		}
	}

	/*
	 * Reset the player to the start of the level.
	 */
	public void reset() {
		x = level.getStartColumn();
		y = level.getStartRow();
		key = TileColor.NONE;
	}

	/*
	 * Take the from a key tile.
	 */
	public void takeKey(TileColor color) {
		key = color;
	}

	/*
	 * Remove the from the player.
	 */
	public void removeKey() {
		key = TileColor.NONE;
	}

	/*
	 * Fulfill a requirement of the level with the specified color.
	 */
	public void fulfillRequirement(TileColor color) {
		level.fulfillRequirement(color);
	}

	public TileColor getKey() {
		return key;
	}

	public int getX() {
		return x;
	}

	public int getY() {
		return y;
	}

}

