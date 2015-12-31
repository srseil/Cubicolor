package com.game.mygame;

public class Player implements Observable {

	private Level level;
	private Observer observer;
	private TileColor key;
	private int x, y;
	private int steps;

	public Player(Level level) {
		this.level = level;
		x = level.getStartColumn();
		y = level.getStartRow();
		key = TileColor.NONE;
		steps = 0;
	}

	/*
	 * Interact with the tile the player is standing on.
	 */
	private void interact() {
		level.getMatrix()[y][x].interact(this);
		notifyObserver();
	}

	@Override
	public void addObserver(Observer observer) {
		this.observer = observer;
	}

	@Override
	public void notifyObserver(Object... args) {
		observer.updateState();
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
			// Model is notified to play indication animation.
			notifyObserver();
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
			notifyObserver();
			steps++;
			interact();
			return;
		} else {
			// Player cannot move to tile.
			notifyObserver();
			return;
		}
	}

	/*
	 * Reset the player to the start of the level.
	 */
	public void reset() {
		x = level.getStartColumn();
		y = level.getStartRow();
		steps = 0;
		key = TileColor.NONE;
	}

	/*
	 * Take the from a key tile.
	 */
	public void takeKey(TileColor color) {
		key = color;
		notifyObserver();
	}

	/*
	 * Remove the from the player.
	 */
	public void removeKey() {
		key = TileColor.NONE;
		notifyObserver();
	}

	/*
	 * Fulfill a requirement of the level with the specified color.
	 */
	public void fulfillRequirement(TileColor color) {
		level.fulfillRequirement(color);
	}

	public int getSteps() {
		return steps;
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

