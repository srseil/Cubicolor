package com.game.mygame;

import com.badlogic.gdx.graphics.Color;

public enum TileColor {

	RED,
	GREEN,
	BLUE,
	YELLOW,
	NONE;

	public static Color getGdxColor(TileColor color) {
		switch (color) {
			case RED:
				return new Color(0.949f, 0.467f, 0.478f, 1.0f);
			case GREEN:
				return new Color(0.6f, 0.8f, 0.6f, 1.0f);
			case BLUE:
				return new Color(0.4f, 0.6f, 0.8f, 1.0f);
			case YELLOW:
				return new Color(1.0f, 0.8f, 0.4f, 1.0f);
			default:
				return new Color(0.0f, 0.0f, 0.0f, 1.0f);
		}
	}
}
