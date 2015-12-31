package com.game.mygame;

public class KeyTile extends Tile {

	private TileColor keyColor;
	private boolean keyAvailable;

	public KeyTile(TileColor color) {
		super();
		this.keyColor = color;
		keyAvailable = true;
	}

	@Override
	public void interact(Player player) {
		player.takeKey(keyColor);
		keyAvailable = false;
		notifyObserver();
	}

	/*
	 * Reset the tile to its default state.
	 */
	public void reset() {
		keyAvailable = true;
	}

	public TileColor getKeyColor() {
		return keyColor;
	}

}

