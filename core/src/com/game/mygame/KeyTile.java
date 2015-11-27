package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

public class KeyTile extends Tile {

	private TileAttributes.TColor color;
	private boolean key;
	//private Circle paint;

	public KeyTile(TileAttributes.TColor color) {
		super();
		this.color = color;
		key = true;
	}

	public void takeKey() {
		key = false;
		// Change appearance.
	}

	public void reset() {
		key = true;
	}

	@Override
	public void draw(float x, float y, ShapeRenderer renderer) {
		renderer.setColor(1.0f, 1.0f, 1.0f, 1.0f);
		renderer.rect(x, y, 50, 50);

		switch (color) {
			case RED:
				renderer.setColor(1.0f, 0.1f, 0.1f, 1);
				break;
			case GREEN:
				renderer.setColor(0.1f, 1.0f, 0.1f, 1);
				break;
			case BLUE:
				renderer.setColor(0.1f, 0.1f, 1.0f, 1);
				break;
			case YELLOW:
				renderer.setColor(1.0f, 1.0f, 0.1f, 1);
				break;
			default:
				renderer.setColor(0.0f, 0.0f, 0.0f, 1);
		}
		renderer.circle(x + 25.0f, y + 25.0f, 12.5f);
	}

	public TileAttributes.TColor getColor() {
		return color;
	}

	public boolean hasKey() {
		return key;
	}

}
