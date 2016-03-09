package com.game.mygame;

public class KeyTile extends Tile {

	private TileColor keyColor;

	public KeyTile(TileColor color) {
		super();
		this.keyColor = color;
	}

	@Override
	public void interact(Player player) {
		player.takeKey(keyColor);
		notifyObserver();
	}

	/*
	 * Reset the tile to its default state.
	 */
	public void reset() {
	}

	public TileColor getKeyColor() {
		return keyColor;
	}

}

