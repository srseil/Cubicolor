package com.game.mygame;

import com.badlogic.gdx.graphics.Color;

public enum TileColor {

	RED,	// 242,119,122 ; f2777a
	GREEN,	// 153,204,153 ; 99cc99
	BLUE,	// 102,153,204 ; 6699cc
	YELLOW,	// 255,204,102 ; ffcc66
	NONE;	// 252,250,246 ; fcfaf6 -> colorless Tiles
	//BLACK,	// 45,45,45    ; 2d2d2d
	//BACKGROUND;	// 0.949f, 0.941f, 0.925f ; ...

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
			case NONE:
				return new Color(0.989f, 0.981f, 0.965f, 1.0f);
			/*
			case BLACK:
				return new Color(0.176f, 0.176f, 0.176f, 1.0f);
			case BACKGROUND:
				return new Color(0.949f, 0.941f, 0.925f, 1.0f);
				*/
			default:
				return new Color(0.0f, 0.0f, 0.0f, 1.0f);
		}
	}

	public static String getKeyTileTexturePath(TileColor color) {
		switch (color) {
			case RED:
				return "keytile_red.png";
			case GREEN:
				return "keytile_green.png";
			case BLUE:
				return "keytile_blue.png";
			case YELLOW:
				return "keytile_yellow.png";
			default:
				return "keytile_red.png";
		}
	}

}

