package com.game.mygame;

import com.badlogic.gdx.graphics.Color;

public class TileAttributes {

	public enum TColor {
		RED,
		GREEN,
		BLUE,
		YELLOW,
		NONE
	}

	public enum TKind {
		NORMAL,
		KEY,
		LOCK,
		EXIT
	}

	public static Color getGDXColor(TColor color) {
		switch (color) {
			case RED:
				return new Color(1.0f, 0.0f, 0.0f, 1.0f);
			case GREEN:
				return new Color(0.0f, 1.0f, 0.0f, 1.0f);
			case BLUE:
				return new Color(0.0f, 0.0f, 1.0f, 1.0f);
			case YELLOW:
				return new Color(0.0f, 1.0f, 1.0f, 1.0f);
			default:
				return new Color(0.0f, 0.0f, 0.0f, 1.0f);
		}
	}

}
