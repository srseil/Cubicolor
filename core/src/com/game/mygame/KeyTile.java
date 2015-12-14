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

	public void reset() {
		keyAvailable = true;
		// no notify.
	}

	public TileColor getKeyColor() {
		return keyColor;
	}

	public boolean hasKey() {
		return keyAvailable;
	}

}
