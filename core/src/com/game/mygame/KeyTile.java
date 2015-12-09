package com.game.mygame;

public class KeyTile extends Tile {

	private TileAttributes.TColor keyColor;
	private boolean keyAvailable;

	public KeyTile(TileAttributes.TColor color) {
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

	public TileAttributes.TColor getKeyColor() {
		return keyColor;
	}

	public boolean hasKey() {
		return keyAvailable;
	}

}
